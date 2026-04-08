using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;

namespace RestartCombat.Core;

public static class ModBootstrap
{
    private const string HarmonyId = "restartcombat.harmony";
    private const string BuildMarker = "2026-04-08-release-a";

    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        Log.Info($"[RestartCombat] Mod loaded. build={BuildMarker}");

        var harmony = new Harmony(HarmonyId);
        harmony.PatchAll();
    }
}
