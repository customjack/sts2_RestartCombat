using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;
using MegaCrit.Sts2.Core.Saves;
using RestartCombat.Features.Settings;

namespace RestartCombat.Features.Multiplayer;

internal struct RestartCombatSnapshotMessage : INetMessage, IPacketSerializable
{
    public SerializableRun SerializableRun;

    public bool ShouldBroadcast => true;

    public NetTransferMode Mode => NetTransferMode.Reliable;

    public LogLevel LogLevel => LogLevel.Info;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(SerializableRun);
    }

    public void Deserialize(PacketReader reader)
    {
        SerializableRun = reader.Read<SerializableRun>();
    }
}

internal static class RestartCombatMultiplayerService
{
    private static readonly object Sync = new();
    private static readonly Dictionary<INetGameService, MessageHandlerDelegate<RestartCombatSnapshotMessage>> SnapshotHandlers = [];
    private static readonly Dictionary<INetGameService, Action<NetErrorInfo>> DisconnectHandlers = [];

    public static void EnsureAttached(INetGameService netService, string source)
    {
        lock (Sync)
        {
            if (SnapshotHandlers.ContainsKey(netService))
            {
                return;
            }

            MessageHandlerDelegate<RestartCombatSnapshotMessage> snapshotHandler = (message, senderId) =>
                HandleSnapshot(netService, message, senderId);
            Action<NetErrorInfo> disconnectHandler = _ => OnDisconnected(netService);

            SnapshotHandlers[netService] = snapshotHandler;
            DisconnectHandlers[netService] = disconnectHandler;

            netService.RegisterMessageHandler(snapshotHandler);
            netService.Disconnected += disconnectHandler;
        }

    }

    public static void BroadcastHostRestart(INetGameService netService, SerializableRun serializableRun, string source)
    {
        if (netService.Type != NetGameType.Host || !netService.IsConnected)
        {
            return;
        }

        EnsureAttached(netService, source);
        netService.SendMessage(new RestartCombatSnapshotMessage
        {
            SerializableRun = serializableRun
        });
    }

    private static void HandleSnapshot(INetGameService netService, RestartCombatSnapshotMessage message, ulong senderId)
    {
        if (senderId == netService.NetId)
        {
            return;
        }

        RestartCombatSettingsButtonFeature.TriggerNetworkRestart(message.SerializableRun, senderId);
    }

    private static void OnDisconnected(INetGameService netService)
    {
        lock (Sync)
        {
            if (SnapshotHandlers.TryGetValue(netService, out var snapshotHandler))
            {
                netService.UnregisterMessageHandler(snapshotHandler);
                SnapshotHandlers.Remove(netService);
            }

            if (DisconnectHandlers.TryGetValue(netService, out var disconnectHandler))
            {
                netService.Disconnected -= disconnectHandler;
                DisconnectHandlers.Remove(netService);
            }
        }
    }
}

[HarmonyPatch(typeof(NetClientGameService))]
internal static class NetClientGameServiceAttachRestartCombatPatch
{
    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyPostfix]
    public static void Postfix(NetClientGameService __instance)
    {
        RestartCombatMultiplayerService.EnsureAttached(__instance, "NetClientGameService::.ctor");
    }
}

[HarmonyPatch(typeof(NetHostGameService))]
internal static class NetHostGameServiceAttachRestartCombatPatch
{
    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyPostfix]
    public static void Postfix(NetHostGameService __instance)
    {
        RestartCombatMultiplayerService.EnsureAttached(__instance, "NetHostGameService::.ctor");
    }
}
