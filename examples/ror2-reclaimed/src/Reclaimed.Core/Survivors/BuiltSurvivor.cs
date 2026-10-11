using System.Collections.Generic;
using Reclaimed.Core.Lore;
using Reclaimed.Core.Resources;
using RoR2;
using UnityEngine;

namespace Reclaimed.Core.Survivors
{
    /// <summary>Everything <see cref="SurvivorBuilder.Build"/> produced; survivors attach skills and components to it.</summary>
    public sealed class BuiltSurvivor
    {
        public SurvivorSpec Spec;
        /// <summary>False when the source could not be loaded or a required step failed; nothing was registered.</summary>
        public bool Ok;
        public SurvivorTokens Tokens;

        public GameObject BodyPrefab;
        public GameObject DisplayPrefab;
        public GameObject MasterPrefab;
        public SurvivorDef SurvivorDef;

        public CharacterBody Body;
        public Transform Model;
        public CharacterModel CharacterModel;
        public ChildLocator ChildLocator;
        public Animator Animator;
        public SkinDef DefaultSkin;

        public SkillLocator SkillLocator;
        public GenericSkill Primary;
        public GenericSkill Secondary;
        public GenericSkill Utility;
        public GenericSkill Special;

        public EntityStateMachine BodyMachine;
        public EntityStateMachine WeaponMachine;
        public readonly Dictionary<string, EntityStateMachine> Machines = new Dictionary<string, EntityStateMachine>();

        public EntityStateMachine Machine(string name)
        {
            EntityStateMachine m;
            return name != null && Machines.TryGetValue(name, out m) ? m : null;
        }

        /// <summary>Adds a component to the body prefab (e.g. AdaptiveArmor); returns the existing one if present.</summary>
        public T AddBodyComponent<T>() where T : Component
        {
            T existing = BodyPrefab.GetComponent<T>();
            return existing != null ? existing : BodyPrefab.AddComponent<T>();
        }

        /// <summary>
        /// Adds a new <see cref="ResourceMeter"/> to the body prefab, also when it already has some
        /// (rage and souls on one body). Meters are addressed and drawn in the order they are added.
        /// </summary>
        public ResourceMeter AddMeter()
        {
            return BodyPrefab.AddComponent<ResourceMeter>();
        }
    }
}
