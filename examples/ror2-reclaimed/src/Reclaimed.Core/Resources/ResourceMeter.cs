using System;
using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace Reclaimed.Core.Resources
{
    /// <summary>
    /// A networked resource bar (rage, souls, heat, ...) attached to a survivor body prefab.
    /// The server owns the value: it applies regen/decay and every gain/spend, and replicates the value, the regen
    /// and decay timers and the last spend request it processed with manual <see cref="OnSerialize"/> (no UNet
    /// weaver in mods). The body's authority spends through <see cref="TrySpendAuthority"/>: the spend is predicted
    /// locally, numbered and sent to the server, which accepts it only from the connection that owns the body.
    /// Predictions stay applied on top of every server value until the server acknowledges them, and a refused
    /// spend raises <see cref="OnSpendRejected"/> on the authority. Gains are server-only (on hit, on kill, ...).
    /// A body may carry several meters (<see cref="Survivors.BuiltSurvivor.AddMeter"/>); they are addressed by
    /// component order, which is identical on every peer because it comes from the prefab.
    /// </summary>
    /// <example>
    /// <code>
    /// ResourceMeter meter = survivor.AddMeter();
    /// meter.displayNameToken = "RECLAIMED_MITHRIX_METER_NAME";
    /// meter.color = new Color(0.55f, 0.75f, 1f);
    /// meter.maxValue = 100f; meter.decayPerSecond = 4f; meter.decayDelay = 3f; meter.segments = 4;
    /// // in a skill state (authority):
    /// if (meter.TrySpendAuthority(25f)) { ... }
    /// </code>
    /// </example>
    public class ResourceMeter : NetworkBehaviour
    {
        private struct PendingSpend
        {
            public uint Seq;
            public float Amount;
            public float Time;
        }

        private const uint ValueBit = 1u;
        private const uint MaxBit = 2u;
        // Stands in for "never" in the replicated timers (infinity is not worth sending).
        private const float NeverSeconds = 1e6f;
        // A prediction the server never answered (lost message, target not found) stops counting after this.
        private const float PendingTimeout = 2f;

        [Header("Display")]
        public string displayNameToken = "";
        public Color color = Color.white;
        [Tooltip("Tick marks dividing the bar; 0 or 1 for none.")]
        public int segments;
        public bool showOnHud = true;

        [Header("Values")]
        public float maxValue = 100f;
        public float startValue;
        [Tooltip("Per second toward max, after regenDelay seconds without spending.")]
        public float regenPerSecond;
        public float regenDelay;
        [Tooltip("Per second toward zero, after decayDelay seconds without gaining.")]
        public float decayPerSecond;
        public float decayDelay;

        // Displayed value: the server's value, plus pending predictions and local regen/decay on clients.
        private float current;
        // Last value the server confirmed (equal to current on the server).
        private float confirmed;
        private float max;
        private float lastGainTime = float.NegativeInfinity;
        private float lastSpendTime = float.NegativeInfinity;
        private bool initialized;

        // Client: spends predicted but not yet acknowledged. Server: last request sequence processed from the owner.
        private readonly List<PendingSpend> pending = new List<PendingSpend>();
        private uint nextSeq;
        private uint ackSeq;

        /// <summary>(meter, oldValue, newValue). Raised on every peer whenever the displayed value changes,
        /// including local predictions and their corrections; use it for presentation.</summary>
        public event Action<ResourceMeter, float, float> OnChanged;
        /// <summary>Raised when a server-confirmed value reaches max (on clients: when an update says so).</summary>
        public event Action<ResourceMeter> OnFull;
        /// <summary>Raised when a server-confirmed value reaches zero (on clients: when an update says so).</summary>
        public event Action<ResourceMeter> OnEmpty;
        /// <summary>(meter, amount). Raised on the authority when the server refused a spend that
        /// <see cref="TrySpendAuthority"/> predicted; the value is already corrected when it fires.</summary>
        public event Action<ResourceMeter, float> OnSpendRejected;

        public float Current { get { EnsureInit(); return current; } }
        public float Max { get { EnsureInit(); return max; } }
        public float Fraction { get { EnsureInit(); return max > 0f ? Mathf.Clamp01(current / max) : 0f; } }
        public bool IsFull { get { return Current >= Max - 0.0001f; } }
        public CharacterBody Body { get; private set; }

        /// <summary>Index among the ResourceMeters on this object (stable across peers).</summary>
        public int MeterIndex
        {
            get
            {
                ResourceMeter[] all = GetComponents<ResourceMeter>();
                return Array.IndexOf(all, this);
            }
        }

        private void Awake()
        {
            Body = GetComponent<CharacterBody>();
            EnsureInit();
        }

        private void EnsureInit()
        {
            if (initialized) return;
            initialized = true;
            max = Mathf.Max(0f, maxValue);
            current = confirmed = Mathf.Clamp(startValue, 0f, max);
        }

        private void FixedUpdate()
        {
            // Clients run the same simulation (with the server's timers) so the bar moves between updates.
            float next = Simulate(current, Time.fixedTime, Time.fixedDeltaTime);
            if (Mathf.Approximately(next, current)) return;
            if (NetworkServer.active) SetServerValue(next);
            else SetDisplayed(next);
        }

        private float Simulate(float value, float t, float dt)
        {
            if (regenPerSecond > 0f && t - lastSpendTime >= regenDelay && value < max) value = Mathf.Min(max, value + regenPerSecond * dt);
            if (decayPerSecond > 0f && t - lastGainTime >= decayDelay && value > 0f) value = Mathf.Max(0f, value - decayPerSecond * dt);
            return value;
        }

        // ---- server API ----

        /// <summary>Server only. Adds and clamps to max. Returns the amount actually added.</summary>
        public float Gain(float amount)
        {
            if (!NetworkServer.active || !IsSane(amount)) return 0f;
            EnsureInit();
            float before = current;
            lastGainTime = Time.fixedTime;
            SetServerValue(Mathf.Min(max, current + amount));
            return current - before;
        }

        /// <summary>Server only. Spends if there is enough; returns false (and spends nothing) otherwise.</summary>
        public bool Spend(float amount)
        {
            if (!NetworkServer.active || !IsSane(amount)) return false;
            EnsureInit();
            if (current + 0.0001f < amount) return false;
            lastSpendTime = Time.fixedTime;
            SetServerValue(Mathf.Max(0f, current - amount));
            return true;
        }

        /// <summary>Server only. Spends up to <paramref name="amount"/>; returns what was spent.</summary>
        public float Drain(float amount)
        {
            if (!NetworkServer.active || !IsSane(amount)) return 0f;
            EnsureInit();
            float spent = Mathf.Min(current, amount);
            lastSpendTime = Time.fixedTime;
            SetServerValue(current - spent);
            return spent;
        }

        /// <summary>Server only. Sets the value (clamped to [0, max]) without touching the regen/decay timers.</summary>
        public void SetValue(float value)
        {
            if (!NetworkServer.active || float.IsNaN(value) || float.IsInfinity(value)) return;
            EnsureInit();
            SetServerValue(Mathf.Clamp(value, 0f, max));
        }

        /// <summary>Server only.</summary>
        public void SetMax(float newMax, bool keepFraction = false)
        {
            if (!NetworkServer.active || !IsSane(newMax)) return;
            EnsureInit();
            float fraction = Fraction;
            max = newMax;
            SetDirtyBit(MaxBit);
            SetServerValue(keepFraction ? fraction * max : Mathf.Min(current, max));
        }

        // ---- authority API ----

        /// <summary>
        /// Called by the body's authority (e.g. in a skill state). Returns false if the displayed value is too low.
        /// On the server (host, AI bodies) it spends for real. On a client it predicts the spend and sends it; the
        /// server can still refuse (raising <see cref="OnSpendRejected"/> here), so a cast that must never happen
        /// unpaid has to be checked again on the server.
        /// </summary>
        public bool TrySpendAuthority(float amount)
        {
            if (!IsSane(amount)) return false;
            EnsureInit();
            if (NetworkServer.active) return Spend(amount);
            if (!hasAuthority)
            {
                Log.WarnOnce("meter.authority." + GetType().Name, "ResourceMeter.TrySpendAuthority called without authority on " + name + "; ignored.");
                return false;
            }
            if (current + 0.0001f < amount) return false;
            if (!NetworkClient.active || netId.IsEmpty()) return false;

            var p = new PendingSpend { Seq = ++nextSeq, Amount = amount, Time = Time.fixedTime };
            pending.Add(p);
            lastSpendTime = p.Time;
            SetDisplayed(Mathf.Max(0f, current - amount));
            ResourceMeterNetwork.SendSpend(netId, (byte)MeterIndex, p.Seq, amount);
            return true;
        }

        // ---- network handlers ----

        /// <summary>Server: a spend request from <paramref name="sender"/>, already checked to own this body.</summary>
        internal void ServerHandleSpend(uint seq, float amount, NetworkConnection sender)
        {
            EnsureInit();
            if (seq > ackSeq) ackSeq = seq;
            // The acknowledgement travels with the value, so send it even when nothing changes.
            SetDirtyBit(ValueBit);
            if (Spend(amount)) return;
            ResourceMeterNetwork.SendRejection(sender, netId, (byte)MeterIndex, seq, amount);
        }

        /// <summary>Client: the server refused spend <paramref name="seq"/>.</summary>
        internal void ClientHandleRejection(uint seq, float amount)
        {
            if (pending.RemoveAll(p => p.Seq == seq) > 0) SetDisplayed(Predict());
            Action<ResourceMeter, float> rejected = OnSpendRejected;
            if (rejected != null) rejected(this, amount);
        }

        // The confirmed value with every unacknowledged spend re-applied.
        private float Predict()
        {
            float value = confirmed;
            for (int i = 0; i < pending.Count; i++) value = Mathf.Max(0f, value - pending[i].Amount);
            return value;
        }

        private static bool IsSane(float amount)
        {
            return amount >= 0f && !float.IsNaN(amount) && !float.IsInfinity(amount);
        }

        private void SetServerValue(float value)
        {
            SetDirtyBit(ValueBit);
            float oldConfirmed = confirmed;
            confirmed = value;
            SetDisplayed(value);
            RaiseConfirmed(oldConfirmed, value);
        }

        private void SetDisplayed(float value)
        {
            float old = current;
            current = value;
            if (Mathf.Approximately(old, value)) return;
            Action<ResourceMeter, float, float> changed = OnChanged;
            if (changed != null) changed(this, old, value);
        }

        private void RaiseConfirmed(float old, float value)
        {
            if (value >= max - 0.0001f && old < max - 0.0001f && OnFull != null) OnFull(this);
            if (value <= 0.0001f && old > 0.0001f && OnEmpty != null) OnEmpty(this);
        }

        // ---- replication ----

        public override float GetNetworkSendInterval()
        {
            return 0.1f;
        }

        public override bool OnSerialize(NetworkWriter writer, bool initialState)
        {
            EnsureInit();
            if (initialState)
            {
                writer.Write(max);
                WriteValue(writer);
                return true;
            }
            uint bits = syncVarDirtyBits;
            writer.WritePackedUInt32(bits);
            if ((bits & MaxBit) != 0) writer.Write(max);
            if ((bits & ValueBit) != 0) WriteValue(writer);
            return bits != 0;
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            EnsureInit();
            if (initialState)
            {
                max = reader.ReadSingle();
                ReadValue(reader);
                return;
            }
            uint bits = reader.ReadPackedUInt32();
            if ((bits & MaxBit) != 0) max = reader.ReadSingle();
            if ((bits & ValueBit) != 0) ReadValue(reader);
            else if ((bits & MaxBit) != 0) SetDisplayed(Mathf.Min(current, max));
        }

        private void WriteValue(NetworkWriter writer)
        {
            float now = Time.fixedTime;
            writer.Write(confirmed);
            writer.Write(Mathf.Min(now - lastGainTime, NeverSeconds));
            writer.Write(Mathf.Min(now - lastSpendTime, NeverSeconds));
            writer.WritePackedUInt32(ackSeq);
        }

        private void ReadValue(NetworkReader reader)
        {
            float value = reader.ReadSingle();
            float sinceGain = reader.ReadSingle();
            float sinceSpend = reader.ReadSingle();
            uint ack = reader.ReadPackedUInt32();

            float now = Time.fixedTime;
            lastGainTime = sinceGain >= NeverSeconds ? float.NegativeInfinity : now - sinceGain;
            lastSpendTime = sinceSpend >= NeverSeconds ? float.NegativeInfinity : now - sinceSpend;
            pending.RemoveAll(p => p.Seq <= ack || now - p.Time > PendingTimeout);
            for (int i = 0; i < pending.Count; i++) lastSpendTime = Mathf.Max(lastSpendTime, pending[i].Time);

            float oldConfirmed = confirmed;
            confirmed = value;
            RaiseConfirmed(oldConfirmed, value);
            SetDisplayed(Predict());
        }
    }
}
