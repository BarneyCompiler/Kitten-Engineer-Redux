using HarmonyLib;
using KSA;

namespace KittenEngineerRedux.Flight;

[HarmonyPatch(typeof(Vehicle), nameof(Vehicle.OnDrawUi))]
internal static class Patch_FlightHud
{
    private static void Postfix(Vehicle __instance, Viewport inViewport)
    {
        if (inViewport != Program.MainViewport)
            return;
        if (Program.ControlledVehicle != __instance)
            return;

        FlightHud.Draw(__instance, inViewport);
    }
}