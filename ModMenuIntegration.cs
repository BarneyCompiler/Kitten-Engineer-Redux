using Brutal.ImGuiApi;
using ModMenu;
using KittenEngineerRedux.Editor;
using KittenEngineerRedux.Flight;

namespace KittenEngineerRedux.UI;

public static class ModMenuIntegration
{
    [ModMenuEntry("Kitten Engineer Redux")]
    public static void DrawMenu()
    {
        bool showHud = FlightHud.Visible;
        if (ImGui.MenuItem("Flight HUD"u8, default, showHud))
            FlightHud.Visible = !showHud;

        bool showEditor = EditorPanel.Visible;
        if (ImGui.MenuItem("Editor Panel"u8, default, showEditor))
            EditorPanel.Visible = !showEditor;
    }
}