using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game.PeerInput;
using MegaCrit.Sts2.Core.Runs;
using System;
using System.Collections.Generic;

namespace RestartCombat.Features.Multiplayer;

/// <summary>
/// Harmony patches to address critical crashes in the core game's multiplayer and context management.
/// </summary>
public static class MultiplayerStabilityPatches
{
    private static readonly AccessTools.FieldRef<HoveredModelTracker, IPlayerCollection> PlayerCollectionField =
        AccessTools.FieldRefAccess<HoveredModelTracker, IPlayerCollection>("_playerCollection");

    private static readonly AccessTools.FieldRef<HoveredModelTracker, List<AbstractModel>> HoveredModelsField =
        AccessTools.FieldRefAccess<HoveredModelTracker, List<AbstractModel>>("_hoveredModels");

    /// <summary>
    /// Patch for HoveredModelTracker.OnPlayerStateChanged to prevent ArgumentOutOfRangeException 
    /// if the playerSlotIndex is -1 or out of bounds for the _hoveredModels list.
    /// </summary>
    [HarmonyPatch(typeof(HoveredModelTracker), "OnPlayerStateChanged", new[] { typeof(ulong) })]
    [HarmonyPrefix]
    public static bool OnPlayerStateChangedPrefix(HoveredModelTracker __instance, ulong playerId)
    {
        try
        {
            var playerCollection = PlayerCollectionField(__instance);
            var player = playerCollection.GetPlayer(playerId);
            if (player == null)
            {
                return false; // Skip original
            }

            int playerSlotIndex = playerCollection.GetPlayerSlotIndex(player);
            var hoveredModels = HoveredModelsField(__instance);

            // CRITICAL FIX: The core game lacks the >= 0 check, which causes crashes with index -1.
            if (playerSlotIndex < 0 || playerSlotIndex >= hoveredModels.Count)
            {
                Log.Warn($"[RestartCombat] Skipping OnPlayerStateChanged for playerId {playerId} because index {playerSlotIndex} is out of bounds (Count: {hoveredModels.Count}).");
                return false; // Skip original to prevent ArgumentOutOfRangeException
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[RestartCombat] Error in HoveredModelTracker.OnPlayerStateChanged prefix: {ex}");
        }

        return true; // Proceed with original method
    }

    /// <summary>
    /// Patch for LocalContext.GetMe(IPlayerCollection) to return null instead of throwing 
    /// InvalidOperationException if the local player is not found in the collection.
    /// This prevents crashes during mod-induced run reloads.
    /// </summary>
    [HarmonyPatch(typeof(LocalContext), nameof(LocalContext.GetMe), new[] { typeof(IPlayerCollection) })]
    [HarmonyPrefix]
    public static bool GetMePrefix(IPlayerCollection playerCollection, ref Player? __result)
    {
        if (LocalContext.NetId.HasValue && playerCollection != null)
        {
            __result = playerCollection.GetPlayer(LocalContext.NetId.Value);
            return false; // Skip original to avoid the "Local player not found" throw.
        }
        return true; // Let original handle null/no netid
    }
}
