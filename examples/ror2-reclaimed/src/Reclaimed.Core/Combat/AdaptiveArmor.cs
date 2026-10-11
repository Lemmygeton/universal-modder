using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace Reclaimed.Core.Combat
{
    /// <summary>
    /// Damage-adaptive armor: taking damage grants armor proportional to the fraction of max health lost, up
    /// to a cap; it decays after a delay. Server-authoritative, replicated with manual serialization, and fed
    /// into stats through RecalculateStatsAPI (armorAdd). Generic: Mithrix uses it, any body can.
    /// </summary>
    /// <example>
    /// <code>
    /// var armor = bodyPrefab.AddComponent&lt;AdaptiveArmor&gt;();
    /// armor.armorPerHealthFraction = 300f; // losing 10% max HP grants 30 armor
    /// armor.maxArmor = 150f; armor.decayDelay = 2f; armor.decayPerSecond = 40f;
    /// </code>
    /// </example>
    [DisallowMultipleComponent]
    public class AdaptiveArmor : NetworkBehaviour
    {
        private const uint ArmorBit = 1u;
        private const float StatRefreshStep = 2f;

        [Tooltip("Armor gained per 1.0 of max combined health (health + shield) lost in a single hit.")]
        public float armorPerHealthFraction = 300f;
        public float maxArmor = 150f;
        [Tooltip("Hits below this fraction of combined max health are ignored (stops DoT ticks from farming armor).")]
        public float minimumHealthFraction = 0.01f;
        public float decayDelay = 2f;
        public float decayPerSecond = 40f;

        private float armor;
        private float lastHitTime = float.NegativeInfinity;
        private float lastStatArmor;
        private CharacterBody body;

        public float CurrentArmor { get { return armor; } }
        /// <summary>0..1 of <see cref="maxArmor"/>, for HUD/VFX intensity.</summary>
        public float Ratio { get { return maxArmor > 0f ? Mathf.Clamp01(armor / maxArmor) : 0f; } }

        private static bool installed;

        internal static void Install()
        {
            if (installed) return;
            installed = true;
            RecalculateStatsAPI.GetStatCoefficients += (sender, args) =>
            {
                AdaptiveArmor component = sender != null ? sender.GetComponent<AdaptiveArmor>() : null;
                if (component != null && component.enabled) args.armorAdd += component.armor;
            };
            GlobalEventManager.onServerDamageDealt += report =>
            {
                if (report == null || report.victimBody == null) return;
                AdaptiveArmor component = report.victimBody.GetComponent<AdaptiveArmor>();
                if (component != null && component.enabled) component.OnDamagedServer(report);
            };
        }

        private void Awake()
        {
            body = GetComponent<CharacterBody>();
        }

        private void OnDamagedServer(DamageReport report)
        {
            if (!NetworkServer.active || report.damageDealt <= 0f) return;
            HealthComponent health = report.victimBody.healthComponent;
            if (health == null) return;
            float fraction = report.damageDealt / Mathf.Max(1f, health.fullCombinedHealth);
            if (fraction < minimumHealthFraction) return;
            lastHitTime = Time.fixedTime;
            SetArmor(Mathf.Min(maxArmor, armor + fraction * armorPerHealthFraction));
        }

        private void FixedUpdate()
        {
            if (!NetworkServer.active || armor <= 0f || decayPerSecond <= 0f) return;
            if (Time.fixedTime - lastHitTime < decayDelay) return;
            SetArmor(Mathf.Max(0f, armor - decayPerSecond * Time.fixedDeltaTime));
        }

        /// <summary>Server only: clears all adaptive armor (e.g. a skill that consumes it).</summary>
        public void ResetArmor()
        {
            if (NetworkServer.active) SetArmor(0f);
        }

        private void SetArmor(float value)
        {
            armor = value;
            SetDirtyBit(ArmorBit);
            RefreshStats(false);
        }

        // Stats are recalculated in coarse steps so constant decay does not dirty stats every tick.
        private void RefreshStats(bool force)
        {
            if (body == null) return;
            if (!force && Mathf.Abs(armor - lastStatArmor) < StatRefreshStep && !(armor <= 0f && lastStatArmor > 0f)) return;
            lastStatArmor = armor;
            body.MarkAllStatsDirty();
        }

        public override float GetNetworkSendInterval()
        {
            return 0.2f;
        }

        public override bool OnSerialize(NetworkWriter writer, bool initialState)
        {
            if (initialState)
            {
                writer.Write(armor);
                return true;
            }
            uint bits = syncVarDirtyBits;
            writer.WritePackedUInt32(bits);
            if ((bits & ArmorBit) != 0) writer.Write(armor);
            return bits != 0;
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            if (initialState)
            {
                armor = reader.ReadSingle();
                RefreshStats(true);
                return;
            }
            uint bits = reader.ReadPackedUInt32();
            if ((bits & ArmorBit) != 0)
            {
                armor = reader.ReadSingle();
                RefreshStats(false);
            }
        }
    }
}
