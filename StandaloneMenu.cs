using HarmonyLib;
using Brutal.ImGuiApi;
using KSA;

namespace KittenEngineerRedux.UI;

[HarmonyPatch(typeof(Program), nameof(Program.DrawProgramMenusHook))]
internal static class Patch_StandaloneMenu
{
    private static void Postfix()
    {
        if (!ImGui.BeginMenu("Kitten Engineer Redux"u8))
            return;

        Program.MainViewport.MenuBarInUse = true;
        MenuContent.DrawToggles();

        ImGui.EndMenu();
    }
}