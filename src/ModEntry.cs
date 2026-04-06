using MegaCrit.Sts2.Core.Modding;
using RestartCombat.Core;

namespace RestartCombat;

[ModInitializer("OnModLoaded")]
public static class ModEntry
{
    public static void OnModLoaded()
    {
        ModBootstrap.Initialize();
    }
}
