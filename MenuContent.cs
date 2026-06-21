using Brutal.ImGuiApi;
using KittenEngineerRedux.Editor;
using KittenEngineerRedux.Flight;

namespace KittenEngineerRedux.UI;

internal static class MenuContent
{
    public static void DrawToggles()
    {
        bool showHud = FlightHud.Visible;
        if (ImGui.MenuItem("Flight HUD"u8, default, showHud))
            FlightHud.Visible = !showHud;

        bool showEditor = EditorPanel.Visible;
        if (ImGui.MenuItem("Editor Panel"u8, default, showEditor))
            EditorPanel.Visible = !showEditor;
    }
}