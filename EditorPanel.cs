using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using KittenEngineerRedux.Analysis;
using KittenEngineerRedux.UI;

namespace KittenEngineerRedux.Editor;

internal static class EditorPanel
{
    private const float DefaultWidth = 300f;
    private const float SidePadding = 16f;

    private static bool _useSeaLevel;

    public static bool Visible { get; set; } = true;

    public static void Draw(VehicleEditor editor, Viewport viewport)
    {
        if (!Visible)
            return;

        PartTree? parts = editor.EditingSpace.Parts;
        if (parts == null)
            return;

        VehicleMassSummary mass = MassAnalyzer.Analyze(parts);

        float ambientPressure = 0f;
        float surfaceGravity = 0f;
        if (_useSeaLevel)
        {
            IParentBody? home = Universe.CurrentSystem?.HomeBody;
            ambientPressure = EnvironmentHelpers.GetSeaLevelPressure(home);
            surfaceGravity = EnvironmentHelpers.ComputeSurfaceGravity(home);
        }

        VehicleBurnAnalysis burn = SequenceAnalyzer.Analyze(parts, mass.WetMass, ambientPressure, surfaceGravity);

        float defaultHeight = 150f + burn.Sequences.Count * PanelKit.RowHeight + PanelKit.RowHeight;
        float2 defaultPos = viewport.Position + new float2(viewport.Size.X - DefaultWidth - SidePadding, 60f);

        bool open = PanelKit.BeginWindow("Kitten Engineer Redux###KerEditorPanel"u8, defaultPos, new float2(DefaultWidth, defaultHeight));
        if (open)
        {
            float contentWidth = ImGui.GetContentRegionAvail().X;
            ImDrawListPtr dl = ImGui.GetWindowDrawList();
            float2 origin = ImGui.GetCursorScreenPos();

            PanelKit.DrawAccentStrip(dl, origin, ImGui.GetWindowSize().Y);

            float y = origin.Y;
            y = PanelKit.DrawSectionHeader(dl, origin, contentWidth, y, "VEHICLE MASS");
            y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Dry Mass", PanelKit.FormatMass(mass.DryMass));
            y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Wet Mass", PanelKit.FormatMass(mass.WetMass));
            y = PanelKit.DrawRow(dl, origin, contentWidth, y, "Propellant", PanelKit.FormatMass(mass.PropellantMass));

            y += 6f;
            y = PanelKit.DrawTwoWayToggle(dl, origin, contentWidth, y, "Vacuum", "Sea Level", _useSeaLevel,
                "##KerEditorToggleVac", "##KerEditorToggleSea", out bool clickedSea, out bool clickedVac);
            if (clickedSea) _useSeaLevel = true;
            if (clickedVac) _useSeaLevel = false;

            y += 4f;
            y = PanelKit.DrawSectionHeader(dl, origin, contentWidth, y, "STAGE DELTA-V");
            foreach (SequenceBurnInfo stage in burn.Sequences)
            {
                string label = $"Stage {stage.SequenceNumber}";
                string value = $"{stage.DeltaV:F0} m/s  TWR {stage.Twr:F2}";
                y = PanelKit.DrawRow(dl, origin, contentWidth, y, label, value);
            }
            y = PanelKit.DrawTotalRow(dl, origin, contentWidth, y, "Total dV", $"{burn.TotalDeltaV:F0} m/s");

            ImGui.Dummy(new float2(contentWidth, y - origin.Y));
        }
        PanelKit.EndWindow();
    }
}