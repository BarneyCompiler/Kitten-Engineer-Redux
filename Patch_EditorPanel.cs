using HarmonyLib;
using KSA;

namespace KittenEngineerRedux.Editor;

[HarmonyPatch(typeof(VehicleEditor), nameof(VehicleEditor.OnDrawUi), new[] { typeof(IGameViewport) })]
internal static class Patch_EditorPanel
{
    private static void Postfix(VehicleEditor __instance, IGameViewport inViewport)
    {
        EditorPanel.Draw(__instance, inViewport);
    }
}
