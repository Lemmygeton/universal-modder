using System.Collections.Generic;
using R2API;
using Reclaimed.Core.Assets;
using Reclaimed.Core.Content;
using RoR2;
using RoR2.Projectile;
using UnityEngine;

namespace Reclaimed.Core.Effects
{
    /// <summary>Recolor palettes for reclaimed VFX.</summary>
    public enum Palette
    {
        None,
        /// <summary>Pale lunar blue-white (Mithrix).</summary>
        Lunar,
        /// <summary>Void violet-magenta (Voidling).</summary>
        Void,
        /// <summary>Cyan-green circuitry (Solus Wing, Solus Heart).</summary>
        Digital,
        /// <summary>Spectral teal-green (Anima Harvester).</summary>
        Soul,
        /// <summary>Faded gold-white (Echo of Providence).</summary>
        Memory,
    }

    /// <summary>
    /// Clones vanilla effects, projectiles and ghosts by GUID under a new name and optionally recolors them.
    /// The vanilla prefab is never touched: materials are copied before tinting (and shared between clones
    /// that use the same source material and color).
    /// </summary>
    /// <example>
    /// <code>
    /// GameObject slam = EffectFactory.CloneEffect(MithrixKeys.BrotherSlamImpact, "ReclaimedMithrixSlamImpact", Palette.Lunar);
    /// GameObject orb = EffectFactory.CloneProjectile(VoidlingKeys.Missile, "ReclaimedVoidlingMissile", Palette.Void);
    /// </code>
    /// </example>
    public static class EffectFactory
    {
        private static readonly string[] colorProperties = { "_TintColor", "_Color", "_MainColor", "_EmColor", "_EmissionColor", "_TintColor2", "_RimColor" };
        private static readonly Dictionary<string, Material> materialCache = new Dictionary<string, Material>();

        public static Color ColorOf(Palette palette)
        {
            switch (palette)
            {
                case Palette.Lunar: return new Color(0.62f, 0.8f, 1f);
                case Palette.Void: return new Color(0.78f, 0.25f, 1f);
                case Palette.Digital: return new Color(0.2f, 1f, 0.82f);
                case Palette.Soul: return new Color(0.35f, 0.95f, 0.62f);
                case Palette.Memory: return new Color(1f, 0.86f, 0.52f);
                default: return Color.white;
            }
        }

        /// <summary>Clones an effect prefab, recolors it and registers it as an effect.</summary>
        public static GameObject CloneEffect(string guid, string newName, Palette palette = Palette.None, bool register = true)
        {
            GameObject source = GameAssets.Load<GameObject>(guid, newName);
            return source == null ? null : CloneEffect(source, newName, palette, register);
        }

        public static GameObject CloneEffect(GameObject source, string newName, Palette palette = Palette.None, bool register = true)
        {
            if (source == null) return null;
            GameObject clone = source.InstantiateClone(newName, false);
            if (palette != Palette.None) Tint(clone, ColorOf(palette));
            if (register) ReclaimedContent.AddEffect(clone);
            return clone;
        }

        /// <summary>
        /// Clones a projectile (networked) and, when <paramref name="cloneGhost"/>, its ghost too, recoloring
        /// both. Registers the projectile.
        /// </summary>
        public static GameObject CloneProjectile(string guid, string newName, Palette palette = Palette.None, bool cloneGhost = true,
            bool register = true)
        {
            GameObject source = GameAssets.Load<GameObject>(guid, newName);
            if (source == null) return null;
            GameObject clone = source.InstantiateClone(newName, true);
            ProjectileController controller = clone.GetComponent<ProjectileController>();
            if (controller == null)
            {
                Log.Error("CloneProjectile: '" + source.name + "' has no ProjectileController.");
                return clone;
            }
            if (cloneGhost && controller.ghostPrefab != null)
                controller.ghostPrefab = CloneGhost(controller.ghostPrefab, newName + "Ghost", palette);
            if (palette != Palette.None) Tint(clone, ColorOf(palette));
            if (register) ReclaimedContent.AddProjectile(clone);
            return clone;
        }

        /// <summary>Clones a projectile ghost (not networked, not registered as content).</summary>
        public static GameObject CloneGhost(string guid, string newName, Palette palette = Palette.None)
        {
            GameObject source = GameAssets.Load<GameObject>(guid, newName);
            return source == null ? null : CloneGhost(source, newName, palette);
        }

        public static GameObject CloneGhost(GameObject source, string newName, Palette palette = Palette.None)
        {
            if (source == null) return null;
            GameObject clone = source.InstantiateClone(newName, false);
            if (palette != Palette.None) Tint(clone, ColorOf(palette));
            return clone;
        }

        /// <summary>
        /// Recolors renderers, particle systems, trails, lines and lights under <paramref name="root"/> toward
        /// <paramref name="color"/>'s hue, keeping each original color's brightness and alpha.
        /// </summary>
        public static void Tint(GameObject root, Color color, float strength = 1f)
        {
            if (root == null) return;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material tinted = TintMaterial(materials[i], color, strength);
                    if (tinted != materials[i])
                    {
                        materials[i] = tinted;
                        changed = true;
                    }
                }
                if (changed) renderer.sharedMaterials = materials;
            }
            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = ps.main;
                main.startColor = Shift(main.startColor, color, strength);
                ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
                if (col.enabled) col.color = Shift(col.color, color, strength);
            }
            foreach (TrailRenderer trail in root.GetComponentsInChildren<TrailRenderer>(true))
            {
                trail.startColor = Shift(trail.startColor, color, strength);
                trail.endColor = Shift(trail.endColor, color, strength);
            }
            foreach (LineRenderer line in root.GetComponentsInChildren<LineRenderer>(true))
            {
                line.startColor = Shift(line.startColor, color, strength);
                line.endColor = Shift(line.endColor, color, strength);
            }
            foreach (Light light in root.GetComponentsInChildren<Light>(true)) light.color = Shift(light.color, color, strength);
        }

        private static Material TintMaterial(Material source, Color color, float strength)
        {
            if (source == null) return null;
            string key = source.GetInstanceID() + "|" + ColorUtility.ToHtmlStringRGBA(color) + "|" + strength.ToString("0.###");
            Material cached;
            if (materialCache.TryGetValue(key, out cached) && cached != null) return cached;
            Material copy = null;
            foreach (string property in colorProperties)
            {
                if (!source.HasProperty(property)) continue;
                if (copy == null)
                {
                    copy = new Material(source);
                    copy.name = source.name + "_Reclaimed";
                }
                copy.SetColor(property, Shift(source.GetColor(property), color, strength));
            }
            Material result = copy ?? source;
            materialCache[key] = result;
            return result;
        }

        /// <summary>Moves a color's hue (and some saturation) to the target's, keeping value and alpha.</summary>
        public static Color Shift(Color original, Color target, float strength = 1f)
        {
            float h, s, v, th, ts, tv;
            Color.RGBToHSV(original, out h, out s, out v);
            Color.RGBToHSV(target, out th, out ts, out tv);
            // HDR colors (v > 1) keep their intensity; HSVToRGB(hdr: true) preserves it.
            Color shifted = Color.HSVToRGB(th, Mathf.Max(s * 0.5f, ts), v, true);
            shifted.a = original.a;
            return Color.Lerp(original, shifted, strength);
        }

        private static ParticleSystem.MinMaxGradient Shift(ParticleSystem.MinMaxGradient g, Color color, float strength)
        {
            switch (g.mode)
            {
                case ParticleSystemGradientMode.Color:
                    return new ParticleSystem.MinMaxGradient(Shift(g.color, color, strength));
                case ParticleSystemGradientMode.TwoColors:
                    return new ParticleSystem.MinMaxGradient(Shift(g.colorMin, color, strength), Shift(g.colorMax, color, strength));
                case ParticleSystemGradientMode.Gradient:
                    return new ParticleSystem.MinMaxGradient(Shift(g.gradient, color, strength));
                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(Shift(g.gradientMin, color, strength), Shift(g.gradientMax, color, strength));
                default:
                    return g;
            }
        }

        private static Gradient Shift(Gradient gradient, Color color, float strength)
        {
            if (gradient == null) return null;
            GradientColorKey[] keys = gradient.colorKeys;
            for (int i = 0; i < keys.Length; i++) keys[i].color = Shift(keys[i].color, color, strength);
            var result = new Gradient { mode = gradient.mode };
            result.SetKeys(keys, gradient.alphaKeys);
            return result;
        }
    }
}
