using System;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.PauseMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using RestartCombat.Features.Multiplayer;

namespace RestartCombat.Features.Settings;

internal static class RestartCombatSettingsButtonFeature
{
    private const string RestartButtonName = "RestartCombatSettingsButton";
    private static bool _restartInProgress;

    public static void Attach(NPauseMenu pauseMenu)
    {
        var settingsButton = pauseMenu.GetNodeOrNull<NPauseMenuButton>("%ButtonContainer/Settings");
        var giveUpButton = pauseMenu.GetNodeOrNull<NPauseMenuButton>("%ButtonContainer/GiveUp");
        if (settingsButton?.GetParent() is not Control parent)
        {
            Log.Warn("[RestartCombat] Could not find Settings button parent on NPauseMenu.");
            return;
        }

        var existingButton = parent.FindChild(RestartButtonName, recursive: false, owned: false) as NPauseMenuButton;
        if (!ShouldShowRestartButton())
        {
            existingButton?.QueueFree();
            RewireFocus(parent);
            return;
        }

        RestartCombatMultiplayerService.EnsureAttached(RunManager.Instance.NetService, "RestartCombat pause menu");

        if (existingButton != null)
        {
            existingButton.Enable();
            existingButton.Visible = true;
            existingButton.TooltipText = GetRestartTooltip();
            RewireFocus(parent);
            return;
        }

        if (settingsButton.Duplicate((int)(Node.DuplicateFlags.Groups | Node.DuplicateFlags.Scripts | Node.DuplicateFlags.UseInstantiation)) is not NPauseMenuButton button)
        {
            Log.Warn("[RestartCombat] Failed to duplicate pause-menu settings button template as NPauseMenuButton.");
            return;
        }

        button.Name = RestartButtonName;
        button.TooltipText = GetRestartTooltip();

        var label = button.GetNodeOrNull<MegaLabel>("Label");
        label?.SetTextAutoSize("Restart Combat");

        button.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(_ => OnRestartPressed(button)));

        parent.AddChild(button);
        var anchor = (Node?)giveUpButton ?? settingsButton;
        var insertIndex = Math.Min(anchor.GetIndex(), parent.GetChildCount() - 1);
        parent.MoveChild(button, insertIndex);

        RewireFocus(parent);
    }

    public static void DisableForMenuTransition(NPauseMenu pauseMenu)
    {
        var restartButton = pauseMenu.GetNodeOrNull<NPauseMenuButton>($"%ButtonContainer/{RestartButtonName}");
        restartButton?.Disable();
    }

    public static void TriggerNetworkRestart(SerializableRun serializableRun, ulong senderId)
    {
        if (_restartInProgress)
        {
            return;
        }

        _restartInProgress = true;
        TaskHelper.RunSafely(RestartCombatFromSnapshotAsync(serializableRun, fromNetworkMessage: true, senderId));
    }

    private static void OnRestartPressed(NPauseMenuButton button)
    {
        if (_restartInProgress || !ShouldShowRestartButton())
        {
            return;
        }

        _restartInProgress = true;
        button.Disable();
        TaskHelper.RunSafely(RestartCombatAsync());
    }

    private static bool ShouldShowRestartButton()
    {
        if (!RunManager.Instance.IsInProgress)
        {
            return false;
        }

        var runState = RunManager.Instance.DebugOnlyGetState();
        if (runState?.CurrentRoom is not CombatRoom { IsPreFinished: false })
        {
            return false;
        }

        return RunManager.Instance.NetService.Type switch
        {
            NetGameType.Singleplayer => true,
            NetGameType.Host => true,
            _ => false
        };
    }

    private static bool IsHostMultiplayerRun()
    {
        return RunManager.Instance.IsInProgress &&
               RunManager.Instance.NetService.Type == NetGameType.Host;
    }

    private static string GetRestartTooltip()
    {
        return IsHostMultiplayerRun()
            ? "Restart current combat encounter for the whole party"
            : "Restart current combat encounter";
    }

    private static async Task RestartCombatAsync()
    {
        try
        {
            if (!ShouldShowRestartButton())
            {
                Log.Warn("[RestartCombat] Restart combat is only available for singleplayer or the multiplayer host.");
                return;
            }

            var readRunSaveResult = SaveManager.Instance.LoadRunSave();
            if (!readRunSaveResult.Success || readRunSaveResult.SaveData == null)
            {
                Log.Warn("[RestartCombat] Could not load run save for restart.");
                return;
            }

            var serializableRun = readRunSaveResult.SaveData;
            if (IsHostMultiplayerRun())
            {
                RestartCombatMultiplayerService.BroadcastHostRestart(RunManager.Instance.NetService, serializableRun, "Pause menu restart button");
                await Task.Delay(150);
            }

            await RestartCombatFromSnapshotAsync(serializableRun, fromNetworkMessage: false, senderId: 0UL);
        }
        catch (Exception ex)
        {
            Log.Error($"[RestartCombat] Failed to restart combat. {ex}");
        }
        finally
        {
            _restartInProgress = false;
        }
    }

    private static async Task RestartCombatFromSnapshotAsync(SerializableRun serializableRun, bool fromNetworkMessage, ulong senderId)
    {
        try
        {
            var reloadedRunState = RunState.FromSerializable(serializableRun);
            var game = NGame.Instance;
            if (game == null)
            {
                Log.Warn("[RestartCombat] NGame.Instance is null; aborting restart.");
                return;
            }

            PrepareUiForReload(game);
            NCapstoneContainer.Instance?.Close();
            await game.Transition.RoomFadeOut();

            if (RunManager.Instance.NetService.Type == NetGameType.Singleplayer)
            {
                RunManager.Instance.CleanUp();
                game.ReactionContainer.InitializeNetworking(new NetSingleplayerGameService());
                RunManager.Instance.SetUpSavedSinglePlayer(reloadedRunState, serializableRun);
                await game.LoadRun(reloadedRunState, serializableRun.PreFinishedRoom);
                await game.Transition.FadeIn();
                    return;
            }

            var netService = RunManager.Instance.NetService;
            RestartCombatMultiplayerService.EnsureAttached(netService, fromNetworkMessage ? $"network restart from {senderId}" : "local multiplayer restart");

            SoftCleanupPreservingNetwork();

            var lobby = new LoadRunLobby(netService, SharedLoadRunLobbyListener.Instance, serializableRun);
            game.RemoteCursorContainer.Initialize(lobby.InputSynchronizer, serializableRun.Players.Select(player => player.NetId));
            game.ReactionContainer.InitializeNetworking(lobby.NetService);
            RunManager.Instance.SetUpSavedMultiPlayer(reloadedRunState, lobby);
            await game.LoadRun(reloadedRunState, serializableRun.PreFinishedRoom);
            lobby.CleanUp(disconnectSession: false);
            await game.Transition.FadeIn();
        }
        finally
        {
            _restartInProgress = false;
        }
    }

    private static void SoftCleanupPreservingNetwork()
    {
        var runManager = RunManager.Instance;
        var traverse = Traverse.Create(runManager);
        traverse.Property("IsCleaningUp").SetValue(true);

        try
        {
            traverse.Field("_runHistoryWasUploaded").SetValue(false);
            runManager.ActionQueueSet.Reset();
            NAudioManager.Instance?.StopAllLoops();
            NOverlayStack.Instance?.Clear();
            NCapstoneContainer.Instance?.CleanUp();
            NMapScreen.Instance?.CleanUp();
            NModalContainer.Instance?.Clear();
            CombatManager.Instance.Reset(graceful: true);
            runManager.CombatReplayWriter?.Dispose();
            runManager.CombatStateSynchronizer?.Dispose();
            runManager.ActionQueueSynchronizer?.Dispose();
            runManager.PlayerChoiceSynchronizer?.Dispose();
            runManager.EventSynchronizer?.Dispose();
            runManager.RewardSynchronizer?.Dispose();
            runManager.RestSiteSynchronizer?.Dispose();
            runManager.OneOffSynchronizer?.Dispose();
            runManager.FlavorSynchronizer?.Dispose();
            runManager.ChecksumTracker?.Dispose();
            runManager.RunLobby?.Dispose();
        }
        finally
        {
            traverse.Property("IsCleaningUp").SetValue(false);
            LocalContext.NetId = null;
            traverse.Property("State").SetValue(null);
        }
    }

    private static void PrepareUiForReload(NGame game)
    {
        game.GetViewport()?.GuiReleaseFocus();
        NTargetManager.Instance?.CancelTargeting();
        NHoverTipSet.Clear();
        var combatUi = NCombatRoom.Instance?.Ui;
        combatUi?.Disable();
        combatUi?.Hand?.DisableControllerNavigation();
    }

    private static void RewireFocus(Control parent)
    {
        var buttons = parent.GetChildren().OfType<NButton>().Where(b => b.Visible).ToList();
        for (var i = 0; i < buttons.Count; i++)
        {
            var current = buttons[i];
            current.FocusNeighborLeft = current.GetPath();
            current.FocusNeighborRight = current.GetPath();
            current.FocusNeighborTop = (i > 0 ? buttons[i - 1] : current).GetPath();
            current.FocusNeighborBottom = (i < buttons.Count - 1 ? buttons[i + 1] : current).GetPath();
        }
    }
}

internal sealed class SharedLoadRunLobbyListener : ILoadRunLobbyListener
{
    public static SharedLoadRunLobbyListener Instance { get; } = new();

    public void PlayerConnected(ulong playerId)
    {
    }

    public void RemotePlayerDisconnected(ulong playerId)
    {
    }

    public Task<bool> ShouldAllowRunToBegin()
    {
        return Task.FromResult(true);
    }

    public void BeginRun()
    {
    }

    public void PlayerReadyChanged(ulong playerId)
    {
    }

    public void LocalPlayerDisconnected(NetErrorInfo info)
    {
        Log.Warn($"[RestartCombat] Multiplayer restart lobby disconnected: {info.GetReason()}");
    }
}

[HarmonyPatch(typeof(NPlayerHand), "RefreshLayout")]
internal static class NPlayerHandRefreshLayoutSafetyPatch
{
    public static bool Prefix(NPlayerHand __instance)
    {
        try
        {
            if (!GodotObject.IsInstanceValid(__instance) || !__instance.IsInsideTree())
            {
                return false;
            }

            var holders = __instance.GetNodeOrNull<Control>("%CardHolderContainer")
                ?.GetChildren()
                .OfType<NHandCardHolder>();
            if (holders != null && holders.Any(h => !GodotObject.IsInstanceValid(h) || !h.IsInsideTree()))
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}

[HarmonyPatch(typeof(NPauseMenu), nameof(NPauseMenu._Ready))]
internal static class NPauseMenuReadyRestartCombatPatch
{
    public static void Postfix(NPauseMenu __instance)
    {
        try
        {
            RestartCombatSettingsButtonFeature.Attach(__instance);
        }
        catch (Exception ex)
        {
            Log.Error($"[RestartCombat] Failed to attach Restart Combat button to pause menu. {ex}");
        }
    }
}

[HarmonyPatch(typeof(NPauseMenu), nameof(NPauseMenu.OnSubmenuOpened))]
internal static class NPauseMenuOpenedRestartCombatPatch
{
    public static void Postfix(NPauseMenu __instance)
    {
        try
        {
            RestartCombatSettingsButtonFeature.Attach(__instance);
        }
        catch (Exception ex)
        {
            Log.Error($"[RestartCombat] Failed to refresh Restart Combat button visibility. {ex}");
        }
    }
}

[HarmonyPatch(typeof(NPauseMenu), "OnSaveAndQuitButtonPressed")]
internal static class NPauseMenuSaveAndQuitRestartCombatPatch
{
    public static void Prefix(NPauseMenu __instance)
    {
        try
        {
            RestartCombatSettingsButtonFeature.DisableForMenuTransition(__instance);
        }
        catch (Exception ex)
        {
            Log.Error($"[RestartCombat] Failed to disable Restart Combat button during save-and-quit transition. {ex}");
        }
    }
}
