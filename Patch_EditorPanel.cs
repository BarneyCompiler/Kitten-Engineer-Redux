using HarmonyLib;
using KSA;

namespace KittenEngineerRedux.Editor;

[HarmonyPatch(typeof(VehicleEditor), nameof(VehicleEditor.OnDrawUi))]
internal static class Patch_EditorPanel
{
    private static void Postfix(VehicleEditor __instance, Viewport inViewport)
    {
        EditorPanel.Draw(__instance, inViewport);
    }
}