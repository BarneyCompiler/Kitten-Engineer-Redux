using HarmonyLib;
using KSA;

namespace KittenEngineerRedux;

[HarmonyPatch(typeof(PlanetRenderer), nameof(PlanetRenderer.RenderClutterDepthNormalPrePass))]
internal static class Patch_PlanetRendererGroundClutter
{
    private static bool Prefix(PlanetRenderer __instance)
    {
        return __instance.GroundClutterRenderer is not null;
    }
}
