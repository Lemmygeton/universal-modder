using RoR2;
using RoR2.Networking;
using UnityEngine;
using UnityEngine.Networking;

namespace Reclaimed.Core.Resources
{
    /// <summary>
    /// Transport for <see cref="ResourceMeter"/> spends: client -> server request and server -> owner rejection.
    /// Raw UNet messages rather than R2API INetMessage because the server must know which connection sent a
    /// request (R2API's message handler does not pass it) to accept spends only from the body's owner.
    /// </summary>
    internal static class ResourceMeterNetwork
    {
        // Outside the ranges used by UNet, RoR2 and R2API.Networking (2048/4096/6144/8192).
        private const short SpendRequestMsg = 21059;
        private const short SpendRejectedMsg = 21060;

        private static readonly NetworkWriter writer = new NetworkWriter();
        private static bool installed;

        internal static void Install()
        {
            if (installed) return;
            installed = true;
            NetworkManagerSystem.onStartServerGlobal += () => NetworkServer.RegisterHandler(SpendRequestMsg, OnSpendRequest);
            NetworkManagerSystem.onStopServerGlobal += () => NetworkServer.UnregisterHandler(SpendRequestMsg);
            NetworkManagerSystem.onStartClientGlobal += client => client.RegisterHandler(SpendRejectedMsg, OnSpendRejected);
        }

        internal static void SendSpend(NetworkInstanceId target, byte meterIndex, uint seq, float amount)
        {
            NetworkConnection conn = ClientScene.readyConnection;
            if (conn == null) return;
            Write(SpendRequestMsg, target, meterIndex, seq, amount);
            conn.SendWriter(writer, QosChannelIndex.defaultReliable.intVal);
        }

        internal static void SendRejection(NetworkConnection owner, NetworkInstanceId target, byte meterIndex, uint seq, float amount)
        {
            if (owner == null) return;
            Write(SpendRejectedMsg, target, meterIndex, seq, amount);
            owner.SendWriter(writer, QosChannelIndex.defaultReliable.intVal);
        }

        private static void Write(short msgType, NetworkInstanceId target, byte meterIndex, uint seq, float amount)
        {
            writer.StartMessage(msgType);
            writer.Write(target);
            writer.Write(meterIndex);
            writer.WritePackedUInt32(seq);
            writer.Write(amount);
            writer.FinishMessage();
        }

        private static ResourceMeter Read(NetworkMessage msg, out uint seq, out float amount)
        {
            NetworkInstanceId target = msg.reader.ReadNetworkId();
            byte meterIndex = msg.reader.ReadByte();
            seq = msg.reader.ReadPackedUInt32();
            amount = msg.reader.ReadSingle();
            GameObject obj = Util.FindNetworkObject(target);
            if (obj == null) return null;
            ResourceMeter[] meters = obj.GetComponents<ResourceMeter>();
            return meterIndex < meters.Length ? meters[meterIndex] : null;
        }

        private static void OnSpendRequest(NetworkMessage msg)
        {
            uint seq;
            float amount;
            ResourceMeter meter = Read(msg, out seq, out amount);
            if (meter == null) return;
            NetworkIdentity identity = meter.GetComponent<NetworkIdentity>();
            if (identity == null || identity.clientAuthorityOwner != msg.conn)
            {
                Log.WarnOnce("meter.owner." + msg.conn.connectionId, "Rejected a ResourceMeter spend for " + meter.name + " from connection " +
                             msg.conn.connectionId + ", which does not own it.");
                SendRejection(msg.conn, meter.netId, (byte)meter.MeterIndex, seq, amount);
                return;
            }
            meter.ServerHandleSpend(seq, amount, msg.conn);
        }

        private static void OnSpendRejected(NetworkMessage msg)
        {
            uint seq;
            float amount;
            ResourceMeter meter = Read(msg, out seq, out amount);
            if (meter != null) meter.ClientHandleRejection(seq, amount);
        }
    }
}
