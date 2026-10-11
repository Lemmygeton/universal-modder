using System.Collections.Generic;
using R2API;
using Reclaimed.Core.Effects;
using Reclaimed.Core.Lore;
using RoR2;
using UnityEngine;

namespace Reclaimed.Core.Survivors
{
    /// <summary>
    /// Skins for reclaimed survivors through R2API.Skins. The live game stores skin data in SkinDefParams,
    /// which the compile references predate; R2API.Skins (built for the live game) converts our
    /// <see cref="SkinDefInfo"/> into a SkinDef + SkinDefParams, so this assembly never touches the new type.
    /// The body and display models get the same SkinDef array (their hierarchies are identical clones).
    /// </summary>
    public static class SkinFactory
    {
        private static readonly string[] tintProperties = { "_Color", "_EmColor", "_EmissionColor", "_TintColor" };

        /// <summary>Default skin from the model's current renderers; replaces the boss's skins on the clone.</summary>
        public static SkinDef CreateDefault(BuiltSurvivor survivor, string nameToken, Sprite icon)
        {
            CharacterModel model = survivor.CharacterModel;
            if (model == null)
            {
                Log.Error(survivor.Spec.Name + ": no CharacterModel, default skin skipped.");
                return null;
            }
            SkinDef skin = Create(survivor, "skin" + survivor.Spec.CachedName + "Default", nameToken,
                icon ?? Skins.CreateSkinIcon(survivor.Spec.BodyColor, Color.gray, Color.black, survivor.Spec.BodyColor),
                (CharacterModel.RendererInfo[])model.baseRendererInfos.Clone(), null, null);
            SetSkins(survivor, new[] { skin });
            return skin;
        }

        /// <summary>
        /// A recolor of the default skin: every material that exposes a color property is copied and shifted toward
        /// <paramref name="tint"/>. Appended to the survivor's skins (body and display).
        /// </summary>
        public static SkinDef AddRecolor(BuiltSurvivor survivor, string skinName, string nameToken, string nameText, Color tint,
            Sprite icon = null, UnlockableDef unlockable = null, float strength = 1f)
        {
            if (survivor.CharacterModel == null || survivor.DefaultSkin == null)
            {
                Log.Error(survivor.Spec.Name + ": recolor skin '" + skinName + "' needs the default skin first.");
                return null;
            }
            Tokens.Add(nameToken, nameText);
            var tintedMaterials = new Dictionary<Material, Material>();
            CharacterModel.RendererInfo[] infos = (CharacterModel.RendererInfo[])survivor.CharacterModel.baseRendererInfos.Clone();
            for (int i = 0; i < infos.Length; i++)
            {
                Material source = infos[i].defaultMaterial;
                if (source == null) continue;
                Material tinted;
                if (!tintedMaterials.TryGetValue(source, out tinted))
                {
                    tinted = new Material(source) { name = source.name + "_" + skinName };
                    foreach (string property in tintProperties)
                        if (tinted.HasProperty(property)) tinted.SetColor(property, EffectFactory.Shift(tinted.GetColor(property), tint, strength));
                    tintedMaterials[source] = tinted;
                }
                infos[i].defaultMaterial = tinted;
            }
            SkinDef skin = Create(survivor, skinName, nameToken,
                icon ?? Skins.CreateSkinIcon(tint, Color.Lerp(tint, Color.white, 0.4f), Color.Lerp(tint, Color.black, 0.5f), tint),
                infos, new[] { survivor.DefaultSkin }, unlockable);
            AppendSkin(survivor, skin);
            return skin;
        }

#pragma warning disable CS0612, CS0618 // SkinDefInfo is R2API's compatibility path; see the class summary.
        private static SkinDef Create(BuiltSurvivor survivor, string name, string nameToken, Sprite icon,
            CharacterModel.RendererInfo[] rendererInfos, SkinDef[] baseSkins, UnlockableDef unlockable)
        {
            var info = new SkinDefInfo
            {
                Name = name,
                NameToken = nameToken,
                Icon = icon,
                RootObject = survivor.Model.gameObject,
                RendererInfos = rendererInfos,
                BaseSkins = baseSkins ?? new SkinDef[0],
                UnlockableDef = unlockable,
                MeshReplacements = new SkinDef.MeshReplacement[0],
                GameObjectActivations = new SkinDef.GameObjectActivation[0],
                ProjectileGhostReplacements = new SkinDef.ProjectileGhostReplacement[0],
                MinionSkinReplacements = new SkinDef.MinionSkinReplacement[0],
            };
            return Skins.CreateNewSkinDef(info);
        }
#pragma warning restore CS0612, CS0618

        private static void SetSkins(BuiltSurvivor survivor, SkinDef[] skins)
        {
            Controller(survivor.Model.gameObject).skins = skins;
            if (survivor.DisplayPrefab != null)
            {
                CharacterModel displayModel = survivor.DisplayPrefab.GetComponentInChildren<CharacterModel>(true);
                if (displayModel != null) Controller(displayModel.gameObject).skins = (SkinDef[])skins.Clone();
            }
        }

        private static void AppendSkin(BuiltSurvivor survivor, SkinDef skin)
        {
            ModelSkinController body = Controller(survivor.Model.gameObject);
            var list = new List<SkinDef>(body.skins ?? new SkinDef[0]) { skin };
            SetSkins(survivor, list.ToArray());
        }

        private static ModelSkinController Controller(GameObject model)
        {
            ModelSkinController controller = model.GetComponent<ModelSkinController>();
            if (controller == null) controller = model.AddComponent<ModelSkinController>();
            return controller;
        }
    }
}
