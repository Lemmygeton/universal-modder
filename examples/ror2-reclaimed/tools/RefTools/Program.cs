// RefTools: reference-assembly helpers for building Reclaimed without the game installed.
//
//   shim <RoR2.dll>
//       Patch RiskOfRain2.GameLibs 1.3.1 (the only GameLibs on nuget.org) up to the current DamageTypeCombo
//       API: adds the DamageSource enum, DamageTypeCombo.damageSource and the GenericPrimary..GenericSpecial
//       presets. Compile-check only; ship builds use the real current GameLibs or the game's own DLLs.
//
//   apicheck <MMHOOK_RoR2.dll> <latest-ref-dll-dir> <our.dll>...
//       Binary-compatibility check against the CURRENT game. MMHOOK_RoR2 is generated from the live
//       Assembly-CSharp/RoR2.dll, so every hookable method has an orig_ delegate carrying its exact signature.
//       Every method/ctor/property our DLLs call on a RoR2 type is checked against it; fields, and methods
//       MMHOOK cannot hook (property accessors, event add/remove, interface members), are checked against
//       member references made by assemblies built for the current game (R2API, RoR2BepInExPack).
//       Exit code 1 if anything is missing or has a different signature.
//
//   dump-refs <dll>...
//       Print the RoR2 member references an assembly makes.
using Mono.Cecil;
using Mono.Cecil.Rocks;

static class Program
{
    static int Main(string[] args)
    {
        if (args.Length >= 2 && args[0] == "dump-refs") return DumpRefs(args.Skip(1));
        if (args.Length == 2 && args[0] == "shim") return Shim.Run(args[1]);
        if (args.Length >= 4 && args[0] == "apicheck") return ApiCheck.Run(args[1], args[2], args.Skip(3).ToArray());
        Console.Error.WriteLine("usage:\n  shim <RoR2.dll>\n  apicheck <MMHOOK_RoR2.dll> <latest-ref-dir> <our.dll>...\n  dump-refs <dll>...");
        return 2;
    }

    internal static bool IsGameScope(string scope) =>
        scope == "RoR2" || scope == "RoR2.dll" || scope == "Assembly-CSharp" || scope == "Assembly-CSharp.dll";

    static int DumpRefs(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            using var m = ModuleDefinition.ReadModule(path);
            foreach (var mr in m.GetMemberReferences())
            {
                if (!IsGameScope(mr.DeclaringType.Scope?.Name ?? "")) continue;
                Console.WriteLine($"{(mr is FieldReference ? "F" : "M")} {mr.FullName}");
            }
        }
        return 0;
    }
}

static class Shim
{
    public static int Run(string path)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path))!;
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(dir);
        foreach (var sib in new[] { "../unity", "../bepinex" }) resolver.AddSearchDirectory(Path.Combine(dir, sib));
        var bytes = File.ReadAllBytes(path);
        using var asm = AssemblyDefinition.ReadAssembly(new MemoryStream(bytes), new ReaderParameters { AssemblyResolver = resolver });
        var mod = asm.MainModule;
        var combo = mod.GetType("RoR2.DamageTypeCombo") ?? throw new Exception("RoR2.DamageTypeCombo not found: GameLibs older than 1.3.1?");
        if (mod.GetType("RoR2.DamageSource") != null)
        {
            Console.WriteLine("shim: RoR2.DamageSource already present, nothing to do");
            return 0;
        }
        // Resolve System.Enum / FlagsAttribute from the assembly's own corlib (netstandard), not this tool's runtime.
        var corlib = mod.TypeSystem.CoreLibrary;
        var enumType = new TypeReference("System", "Enum", mod, corlib);
        var flagsAttr = new TypeReference("System", "FlagsAttribute", mod, corlib);
        var flagsCtor = new MethodReference(".ctor", mod.TypeSystem.Void, flagsAttr) { HasThis = true };

        // GameLibs 1.3.1 has DamageTypeCombo { damageType, damageTypeExtended }. The current game adds a
        // DamageSource tag (which skill slot / equipment / hazard dealt the hit) used by on-hit items.
        var source = new TypeDefinition("RoR2", "DamageSource", TypeAttributes.Public | TypeAttributes.Sealed, enumType);
        source.CustomAttributes.Add(new CustomAttribute(flagsCtor));
        source.Fields.Add(new FieldDefinition("value__", FieldAttributes.Public | FieldAttributes.SpecialName | FieldAttributes.RTSpecialName, mod.TypeSystem.Byte));
        foreach (var (n, v) in new[] { ("NoneSpecified", 0), ("Primary", 1), ("Secondary", 2), ("Utility", 4), ("Special", 8),
                                       ("Equipment", 16), ("Hazard", 32), ("DOT", 64) })
            source.Fields.Add(new FieldDefinition(n, FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault, source) { Constant = (byte)v });
        mod.Types.Add(source);

        combo.Fields.Add(new FieldDefinition("damageSource", FieldAttributes.Public, source) { Offset = 12 });
        foreach (var n in new[] { "GenericPrimary", "GenericSecondary", "GenericUtility", "GenericSpecial" })
            combo.Fields.Add(new FieldDefinition(n, FieldAttributes.Public | FieldAttributes.Static, combo));
        var extended = combo.Fields.First(f => f.Name == "damageTypeExtended").FieldType;
        var ctor = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, mod.TypeSystem.Void);
        ctor.Parameters.Add(new ParameterDefinition("damageType", ParameterAttributes.None, combo));
        ctor.Parameters.Add(new ParameterDefinition("damageTypeExtended", ParameterAttributes.None, extended));
        ctor.Parameters.Add(new ParameterDefinition("damageSource", ParameterAttributes.None, source));
        var il = ctor.Body.GetILProcessor();
        il.Emit(Mono.Cecil.Cil.OpCodes.Ldnull);
        il.Emit(Mono.Cecil.Cil.OpCodes.Throw);
        combo.Methods.Add(ctor);

        File.Copy(path, Path.ChangeExtension(path, ".orig.dll"), true);
        asm.Write(path);
        Console.WriteLine($"shim: added RoR2.DamageSource + DamageTypeCombo.damageSource/GenericPrimary..Special to {path}");
        return 0;
    }
}

static class ApiCheck
{
    public static int Run(string mmhookPath, string latestDir, string[] ours)
    {
        using var mm = ModuleDefinition.ReadModule(mmhookPath);
        // "RoR2.CharacterBody" -> list of (name, params, return) from orig_ delegates in On.RoR2.CharacterBody
        var sigs = new Dictionary<string, List<(string name, string[] ps, string ret, bool isStatic)>>();
        foreach (var t in mm.GetTypes())
        {
            if (!t.FullName.StartsWith("On.", StringComparison.Ordinal) || !t.IsClass || t.IsNested) continue;
            var target = t.FullName.Substring(3);
            var list = new List<(string, string[], string, bool)>();
            foreach (var d in t.NestedTypes.Where(n => n.Name.StartsWith("orig_", StringComparison.Ordinal)))
            {
                var invoke = d.Methods.FirstOrDefault(m => m.Name == "Invoke");
                if (invoke == null) continue;
                var ps = invoke.Parameters.Select(p => Norm(p.ParameterType)).ToList();
                // instance methods take `self` first; its type is the target type (possibly by ref for structs)
                var isStatic = !(ps.Count > 0 && invoke.Parameters[0].Name == "self");
                if (!isStatic) ps.RemoveAt(0);
                list.Add((d.Name.Substring(5), ps.ToArray(), Norm(invoke.ReturnType), isStatic));
            }
            sigs[target] = list;
            foreach (var nt in t.NestedTypes.Where(n => !n.Name.StartsWith("orig_") && !n.Name.StartsWith("hook_")))
                CollectNested(nt, target, sigs);
        }

        // Field and method facts from assemblies compiled against the current game.
        var latestFields = new Dictionary<string, string>();
        var latestMethods = new HashSet<string>();
        foreach (var dll in Directory.EnumerateFiles(latestDir, "*.dll", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(dll).StartsWith("MMHOOK")) continue;
            try
            {
                using var m = ModuleDefinition.ReadModule(dll);
                foreach (var fr in m.GetMemberReferences().OfType<FieldReference>())
                    if (Program.IsGameScope(fr.DeclaringType.Scope?.Name ?? ""))
                        latestFields[Key(fr.DeclaringType) + "::" + fr.Name] = Norm(fr.FieldType);
                foreach (var mref in m.GetMemberReferences().OfType<MethodReference>())
                    if (Program.IsGameScope(mref.DeclaringType.Scope?.Name ?? "") && !(mref is GenericInstanceMethod))
                        latestMethods.Add(MethodKey(mref));
            }
            catch { }
        }

        int bad = 0, ok = 0, okByRef = 0, unverified = 0;
        var unverifiedList = new SortedSet<string>();
        foreach (var path in ours)
        {
            using var m = ModuleDefinition.ReadModule(path);
            foreach (var mr in m.GetMemberReferences())
            {
                if (!Program.IsGameScope(mr.DeclaringType.Scope?.Name ?? "")) continue;
                var owner = Key(mr.DeclaringType);
                if (mr is FieldReference fr)
                {
                    if (latestFields.TryGetValue(owner + "::" + fr.Name, out var lt))
                    {
                        if (lt == Norm(fr.FieldType)) ok++;
                        else { bad++; Console.WriteLine($"MISMATCH field {owner}::{fr.Name}: ours {Norm(fr.FieldType)}, game {lt}  [{Path.GetFileName(path)}]"); }
                    }
                    else { unverified++; unverifiedList.Add($"field  {owner}::{fr.Name} : {Norm(fr.FieldType)}"); }
                    continue;
                }
                if (mr is not MethodReference meth) continue;
                if (mr.DeclaringType is GenericInstanceType || meth is GenericInstanceMethod || meth.HasGenericParameters)
                { unverified++; unverifiedList.Add($"generic {owner}::{meth.Name}"); continue; }
                bool referencedByLatest = latestMethods.Contains(MethodKey(meth));
                if (!sigs.TryGetValue(owner, out var list))
                {
                    if (referencedByLatest) { okByRef++; continue; }
                    unverified++; unverifiedList.Add($"type   {owner} (no hookable methods in MMHOOK: interface/enum/abstract or removed)  ::{meth.Name}"); continue;
                }
                var name = meth.Name == ".ctor" ? "ctor" : meth.Name;
                var ps = meth.Parameters.Select(p => Norm(p.ParameterType)).ToArray();
                var cands = list.Where(c => c.name == name || c.name.StartsWith(name + "_", StringComparison.Ordinal)).ToList();
                if (cands.Any(c => c.ps.SequenceEqual(ps) && (name == "ctor" || c.ret == Norm(meth.ReturnType)))) { ok++; continue; }
                if (cands.Count == 0 && referencedByLatest) { okByRef++; continue; }
                if (cands.Count == 0)
                {
                    // abstract / extern / interface members are not hookable; only flag when the type has no such name at all
                    unverified++; unverifiedList.Add($"method {owner}::{name}({string.Join(", ", ps)}) not hookable (abstract/extern/inherited?)");
                    continue;
                }
                bad++;
                Console.WriteLine($"MISMATCH {owner}::{name}({string.Join(", ", ps)}) : {Norm(meth.ReturnType)}  [{Path.GetFileName(path)}]");
                foreach (var c in cands) Console.WriteLine($"         game has {c.name}({string.Join(", ", c.ps)}) : {c.ret}");
            }
        }
        Console.WriteLine($"apicheck: {ok + okByRef} verified ({okByRef} via current-game references), {bad} broken, {unverified} not verifiable from MMHOOK");
        foreach (var u in unverifiedList) Console.WriteLine("  ? " + u);
        return bad == 0 ? 0 : 1;
    }

    static void CollectNested(TypeDefinition t, string outer, Dictionary<string, List<(string, string[], string, bool)>> sigs)
    {
        var target = outer + "/" + t.Name;
        var list = new List<(string, string[], string, bool)>();
        foreach (var d in t.NestedTypes.Where(n => n.Name.StartsWith("orig_", StringComparison.Ordinal)))
        {
            var invoke = d.Methods.FirstOrDefault(m => m.Name == "Invoke");
            if (invoke == null) continue;
            var ps = invoke.Parameters.Select(p => Norm(p.ParameterType)).ToList();
            var isStatic = !(ps.Count > 0 && invoke.Parameters[0].Name == "self");
            if (!isStatic) ps.RemoveAt(0);
            list.Add((d.Name.Substring(5), ps.ToArray(), Norm(invoke.ReturnType), isStatic));
        }
        sigs[target] = list;
        foreach (var nt in t.NestedTypes.Where(n => !n.Name.StartsWith("orig_") && !n.Name.StartsWith("hook_")))
            CollectNested(nt, target, sigs);
    }

    static string Key(TypeReference t) => t.FullName.Replace('/', '/');

    static string MethodKey(MethodReference m) =>
        $"{Key(m.DeclaringType)}::{m.Name}({string.Join(",", m.Parameters.Select(p => Norm(p.ParameterType)))}):{Norm(m.ReturnType)}";

    // Normalise a type name so MMHOOK's view and ours compare equal.
    static string Norm(TypeReference t)
    {
        if (t is ByReferenceType br) return Norm(br.ElementType) + "&";
        if (t is RequiredModifierType rq) return Norm(rq.ElementType);
        if (t is OptionalModifierType om) return Norm(om.ElementType);
        return t.FullName;
    }
}
