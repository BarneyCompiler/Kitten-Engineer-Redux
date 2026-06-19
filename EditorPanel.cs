using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using KittenEngineerRedux.Analysis;

namespace KittenEngineerRedux.Editor;

internal static class EditorPanel
{
    private const float RowHeight = 22f;
    private const float SidePadding = 16f;
    private const float DefaultWidth = 300f;

    private static readonly float4 WindowBgColor = new(0.07f, 0.08f, 0.10f, 0.96f);
    private static readonly float4 BorderColor = new(0.22f, 0.24f, 0.28f, 1f);
    private static readonly float4 HeaderColor = new(0.55f, 0.78f, 0.95f, 1f);
    private static readonly float4 LabelColor = new(0.72f, 0.74f, 0.78f, 1f);
    private static readonly float4 ValueColor = new(0.95f, 0.96f, 0.98f, 1f);
    private static readonly float4 AccentColor = new(0.30f, 0.62f, 0.90f, 1f);
    private static readonly float4 ToggleOffColor = new(0.16f, 0.17f, 0.20f, 1f);

    private static bool _useSeaLevel;

    public static void Draw(VehicleEditor editor, Viewport viewport)
    {
        PartTree? parts = editor.EditingSpace.Parts;
        if (parts == null)
            return;

        VehicleMassSummary mass = MassAnalyzer.Analyze(parts);

        float ambientPressure = 0f;
        float surfaceGravity = 0f;
        if (_useSeaLevel)
        {
            IParentBody? home = Universe.CurrentSystem.HomeBody;
            ambientPressure = EnvironmentHelpers.GetSeaLevelPressure(home);
            surfaceGravity = EnvironmentHelpers.ComputeSurfaceGravity(home);
        }

        VehicleBurnAnalysis burn = SequenceAnalyzer.Analyze(parts, mass.WetMass, ambientPressure, surfaceGravity);

        float defaultHeight = 150f + burn.Sequences.Count * RowHeight + RowHeight;
        float2 defaultPos = viewport.Position + new float2(viewport.Size.X - DefaultWidth - SidePadding, 60f);

        ImGui.SetNextWindowPos(defaultPos, ImGuiCond.FirstUseEver, null);
        ImGui.SetNextWindowSize(new float2(DefaultWidth, defaultHeight), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new float2(220f, 140f), new float2(700f, 1000f), null);

        ImGui.PushStyleColor(ImGuiCol.WindowBg, ToColor(WindowBgColor));
        ImGui.PushStyleColor(ImGuiCol.Border, ToColor(BorderColor));
        ImGui.PushStyleColor(ImGuiCol.TitleBg, ToColor(WindowBgColor));
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, ToColor(WindowBgColor));
        ImGui.PushStyleColor(ImGuiCol.TitleBgCollapsed, ToColor(WindowBgColor));

        bool open = ImGui.Begin("Kitten Engineer Redux###KerEditorPanel"u8, ImGuiWindowFlags.None);
        if (open)
        {
            float contentWidth = ImGui.GetContentRegionAvail().X;
            ImDrawListPtr dl = ImGui.GetWindowDrawList();
            float2 contentOrigin = ImGui.GetCursorScreenPos();

            float2 accentMin = contentOrigin + new float2(-ImGui.GetStyle().WindowPadding.X, -2f);
            float2 accentMax = new float2(accentMin.X + 3f, contentOrigin.Y + 100000f);
            dl.PushClipRect(contentOrigin, contentOrigin + new float2(contentWidth, ImGui.GetWindowSize().Y), false);
            dl.AddRectFilled(in accentMin, in accentMax, ToColor(AccentColor));
            dl.PopClipRect();

            float y = contentOrigin.Y;
            y = DrawSectionHeader(dl, contentOrigin, contentWidth, y, "VEHICLE MASS");
            y = DrawRow(dl, contentOrigin, contentWidth, y, "Dry Mass", FormatMass(mass.DryMass));
            y = DrawRow(dl, contentOrigin, contentWidth, y, "Wet Mass", FormatMass(mass.WetMass));
            y = DrawRow(dl, contentOrigin, contentWidth, y, "Propellant", FormatMass(mass.PropellantMass));

            y += 6f;
            y = DrawConditionToggle(dl, contentOrigin, contentWidth, y);

            y += 4f;
            y = DrawSectionHeader(dl, contentOrigin, contentWidth, y, "STAGE DELTA-V");
            foreach (SequenceBurnInfo stage in burn.Sequences)
            {
                string label = $"Stage {stage.SequenceNumber}";
                string value = $"{stage.DeltaV:F0} m/s  TWR {stage.Twr:F2}";
                y = DrawRow(dl, contentOrigin, contentWidth, y, label, value);
            }
            y = DrawTotalRow(dl, contentOrigin, contentWidth, y, "Total dV", $"{burn.TotalDeltaV:F0} m/s");

            ImGui.Dummy(new float2(contentWidth, y - contentOrigin.Y));
        }
        ImGui.End();
        ImGui.PopStyleColor(5);
    }

    private static float DrawSectionHeader(ImDrawListPtr dl, float2 origin, float width, float y, string text)
    {
        float2 pos = new float2(origin.X, y + 4f);
        dl.AddText(in pos, ToColor(HeaderColor), text);
        float lineY = y + 20f;
        float2 lineStart = new float2(origin.X, lineY);
        float2 lineEnd = new float2(origin.X + width, lineY);
        dl.AddLine(in lineStart, in lineEnd, ToColor(BorderColor));
        return lineY + 10f;
    }

    private static float DrawRow(ImDrawListPtr dl, float2 origin, float width, float y, string label, string value)
    {
        float2 labelPos = new float2(origin.X, y);
        dl.AddText(in labelPos, ToColor(LabelColor), label);
        float2 valueSize = ImGui.CalcTextSize(value);
        float2 valuePos = new float2(origin.X + width - valueSize.X, y);
        dl.AddText(in valuePos, ToColor(ValueColor), value);
        return y + RowHeight;
    }

    private static float DrawTotalRow(ImDrawListPtr dl, float2 origin, float width, float y, string label, string value)
    {
        float lineY = y - 4f;
        float2 lineStart = new float2(origin.X, lineY);
        float2 lineEnd = new float2(origin.X + width, lineY);
        dl.AddLine(in lineStart, in lineEnd, ToColor(BorderColor));
        float2 labelPos = new float2(origin.X, y + 2f);
        dl.AddText(in labelPos, ToColor(HeaderColor), label);
        float2 valueSize = ImGui.CalcTextSize(value);
        float2 valuePos = new float2(origin.X + width - valueSize.X, y + 2f);
        dl.AddText(in valuePos, ToColor(AccentColor), value);
        return y + RowHeight + 2f;
    }

    private static float DrawConditionToggle(ImDrawListPtr dl, float2 origin, float width, float y)
    {
        float toggleWidth = (width - 6f) / 2f;
        float toggleHeight = 24f;

        float2 vacPos = new float2(origin.X, y);
        float2 seaPos = new float2(vacPos.X + toggleWidth + 6f, y);

        DrawToggleButton(dl, vacPos, toggleWidth, toggleHeight, "Vacuum", !_useSeaLevel, "##KerToggleVac");
        DrawToggleButton(dl, seaPos, toggleWidth, toggleHeight, "Sea Level", _useSeaLevel, "##KerToggleSea");

        return y + toggleHeight + 8f;
    }

    private static void DrawToggleButton(ImDrawListPtr dl, float2 pos, float width, float height, string label, bool active, string id)
    {
        ImGui.SetCursorScreenPos(pos);
        if (ImGui.InvisibleButton(id, new float2(width, height)))
            _useSeaLevel = label == "Sea Level";

        float2 max = pos + new float2(width, height);
        float4 fill = active ? AccentColor : ToggleOffColor;
        dl.AddRectFilled(in pos, in max, ToColor(fill), 4f);

        float2 textSize = ImGui.CalcTextSize(label);
        float2 textPos = pos + new float2((width - textSize.X) / 2f, (height - textSize.Y) / 2f);
        float4 textColor = active ? new float4(1f, 1f, 1f, 1f) : LabelColor;
        dl.AddText(in textPos, ToColor(textColor), label);
    }

    private static string FormatMass(float kg)
    {
        return kg >= 1000f ? $"{kg / 1000f:F2} t" : $"{kg:F1} kg";
    }

    private static ImColor8 ToColor(float4 color) => ImGui.ColorConvertFloat4ToU32(color);
}