using HarmonyLib;
using Brutal.ImGuiApi;
using KSA;

namespace KittenEngineerRedux.UI;

[HarmonyPatch(typeof(Program), nameof(Program.DrawProgramMenusHook))]
internal static class Patch_StandaloneMenu
{
    private static void Postfix()
    {
        if (ModMenuIntegration.IsHandledByModMenu)
            return;

        if (!ImGui.BeginMenu("Kitten Engineer Redux"u8))
            return;

        ((IGameViewportLifecycle)Program.MainViewport).SetMenuBarInUse(true);
        MenuContent.DrawToggles();

        ImGui.EndMenu();
    }
}