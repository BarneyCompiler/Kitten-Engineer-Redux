using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KittenEngineerRedux.Analysis;
using KittenEngineerRedux.UI;

namespace KittenEngineerRedux.Flight;

internal enum HudWidget
{
    Apoapsis,
    Periapsis,
    DeltaV,
    Twr,
    MaxTwr,
    Altitude,
    OrbitalSpeed,
    TimeToApoapsis,
    TimeToPeriapsis,
    VesselMass,
    Thrust,
    VerticalSpeed,
    Acceleration,
}

internal readonly record struct HudWidgetContext(
    OrbitSummary Orbit,
    double TotalDeltaV,
    float ActualTwr,
    float MaximumTwr,
    float Mass,
    float Thrust,
    float AccelerationG);

internal static class HudWidgets
{
    public const int MaxWidgets = 9;

    private const float PickerWidth = 290f;
    private const float ButtonWidth = 30f;

    private static readonly HudWidget[] Defaults = { HudWidget.Apoapsis, HudWidget.Periapsis, HudWidget.DeltaV };
    private static readonly HudWidget[] All = Enum.GetValues<HudWidget>();
    private static readonly List<HudWidget> Selected = new(Defaults);

    public static void Build(HudWidgetContext context, out string[] labels, out string[] values)
    {
        int count = Selected.Count;
        labels = new string[count];
        values = new string[count];
        for (int i = 0; i < count; i++)
        {
            labels[i] = TileLabel(Selected[i]);
            values[i] = Value(Selected[i], context);
        }
    }

    public static void DrawCustomizer()
    {
        if (ImGui.Button("Customize widgets"u8, (float2?)null))
            ImGui.OpenPopup("KER Widget Picker");
        if (!ImGui.BeginPopup("KER Widget Picker"))
            return;
        DrawPicker();
        ImGui.EndPopup();
    }

    private static void DrawPicker()
    {
        ImDrawListPtr dl = ImGui.GetWindowDrawList();
        float2 start = ImGui.GetCursorScreenPos();
        float rowHeight = Math.Max(24f, ImGui.GetFrameHeight());
        float buttonHeight = rowHeight - 4f;
        float lineHeight = ImGui.GetTextLineHeight();
        float right = start.X + PickerWidth;
        float y = start.Y;

        DrawText(dl, start.X, y, PanelKit.HeaderColor, $"ACTIVE ({Selected.Count}/{MaxWidgets})");
        y += lineHeight + 6f;

        int moveUp = -1;
        int moveDown = -1;
        int remove = -1;
        for (int i = 0; i < Selected.Count; i++)
        {
            float textY = y + (rowHeight - lineHeight) * 0.5f;
            DrawText(dl, start.X + 4f, textY, PanelKit.ValueColor, $"{i + 1}. {Name(Selected[i])}");

            float x = right - ButtonWidth * 3f - 8f;
            if (MiniButton(dl, "^", $"##KerWUp{i}", new float2(x, y + 2f), ButtonWidth, buttonHeight) && i > 0)
                moveUp = i;
            x += ButtonWidth + 4f;
            if (MiniButton(dl, "v", $"##KerWDown{i}", new float2(x, y + 2f), ButtonWidth, buttonHeight) && i < Selected.Count - 1)
                moveDown = i;
            x += ButtonWidth + 4f;
            if (MiniButton(dl, "x", $"##KerWRemove{i}", new float2(x, y + 2f), ButtonWidth, buttonHeight))
                remove = i;
            y += rowHeight;
        }

        if (Selected.Count == 0)
        {
            DrawText(dl, start.X + 4f, y + (rowHeight - lineHeight) * 0.5f, PanelKit.LabelColor, "No widgets shown");
            y += rowHeight;
        }

        y += 6f;
        DrawText(dl, start.X, y, PanelKit.HeaderColor, "AVAILABLE");
        y += lineHeight + 6f;

        int add = -1;
        foreach (HudWidget widget in All)
        {
            if (Selected.Contains(widget))
                continue;

            float textY = y + (rowHeight - lineHeight) * 0.5f;
            DrawText(dl, start.X + 4f, textY, PanelKit.LabelColor, Name(widget));
            if (MiniButton(dl, "Add", $"##KerWAdd{(int)widget}", new float2(right - ButtonWidth * 2f, y + 2f), ButtonWidth * 2f, buttonHeight)
                && Selected.Count < MaxWidgets)
                add = (int)widget;
            y += rowHeight;
        }

        y += 6f;
        ImGui.SetCursorScreenPos(new float2(start.X, y));
        bool reset = ImGui.Button("Reset to defaults"u8, (float2?)null);
        y += ImGui.GetFrameHeight();

        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(new float2(PickerWidth, y - start.Y));

        if (reset)
        {
            Selected.Clear();
            Selected.AddRange(Defaults);
        }
        else if (remove >= 0)
        {
            Selected.RemoveAt(remove);
        }
        else if (moveUp > 0)
        {
            (Selected[moveUp - 1], Selected[moveUp]) = (Selected[moveUp], Selected[moveUp - 1]);
        }
        else if (moveDown >= 0)
        {
            (Selected[moveDown + 1], Selected[moveDown]) = (Selected[moveDown], Selected[moveDown + 1]);
        }
        else if (add >= 0)
        {
            Selected.Add((HudWidget)add);
        }
    }

    private static void DrawText(ImDrawListPtr dl, float x, float y, float4 color, string text)
    {
        float2 pos = new float2(x, y);
        dl.AddText(in pos, PanelKit.ToColor(color), text);
    }

    private static bool MiniButton(ImDrawListPtr dl, string label, string id, float2 pos, float width, float height)
    {
        ImGui.SetCursorScreenPos(pos);
        bool clicked = ImGui.InvisibleButton(id, new float2(width, height));
        float2 max = pos + new float2(width, height);
        dl.AddRectFilled(in pos, in max, PanelKit.ToColor(PanelKit.ToggleOffColor), PanelKit.CornerRadius);
        float2 textSize = ImGui.CalcTextSize(label);
        float2 textPos = pos + new float2((width - textSize.X) * 0.5f, (height - textSize.Y) * 0.5f);
        dl.AddText(in textPos, PanelKit.ToColor(PanelKit.ValueColor), label);
        return clicked;
    }

    private static string Name(HudWidget widget) => widget switch
    {
        HudWidget.Apoapsis => "Apoapsis",
        HudWidget.Periapsis => "Periapsis",
        HudWidget.DeltaV => "Delta-v",
        HudWidget.Twr => "TWR (actual)",
        HudWidget.MaxTwr => "TWR (max)",
        HudWidget.Altitude => "Altitude (sea level)",
        HudWidget.OrbitalSpeed => "Orbital speed",
        HudWidget.TimeToApoapsis => "Time to apoapsis",
        HudWidget.TimeToPeriapsis => "Time to periapsis",
        HudWidget.VesselMass => "Vessel mass",
        HudWidget.Thrust => "Thrust (actual)",
        HudWidget.VerticalSpeed => "Vertical speed",
        HudWidget.Acceleration => "Acceleration",
        _ => string.Empty,
    };

    private static string TileLabel(HudWidget widget) => widget switch
    {
        HudWidget.Apoapsis => "APOAPSIS",
        HudWidget.Periapsis => "PERIAPSIS",
        HudWidget.DeltaV => "dV (m/s)",
        HudWidget.Twr => "TWR",
        HudWidget.MaxTwr => "MAX TWR",
        HudWidget.Altitude => "ALTITUDE",
        HudWidget.OrbitalSpeed => "ORBIT SPEED",
        HudWidget.TimeToApoapsis => "TIME TO AP",
        HudWidget.TimeToPeriapsis => "TIME TO PE",
        HudWidget.VesselMass => "MASS",
        HudWidget.Thrust => "THRUST",
        HudWidget.VerticalSpeed => "VERT SPEED",
        HudWidget.Acceleration => "ACCEL",
        _ => string.Empty,
    };

    private static string Value(HudWidget widget, HudWidgetContext c) => widget switch
    {
        HudWidget.Apoapsis => c.Orbit.ApoapsisAltitude.HasValue ? FlightHud.FormatAltitude(c.Orbit.ApoapsisAltitude.Value) : "--",
        HudWidget.Periapsis => FlightHud.FormatAltitude(Math.Max(0.0, c.Orbit.PeriapsisAltitude)),
        HudWidget.DeltaV => $"{c.TotalDeltaV:F0}",
        HudWidget.Twr => $"{c.ActualTwr:F2}",
        HudWidget.MaxTwr => $"{c.MaximumTwr:F2}",
        HudWidget.Altitude => FlightHud.FormatAltitude(c.Orbit.SeaLevelAltitude),
        HudWidget.OrbitalSpeed => $"{c.Orbit.OrbitalSpeed:F1} m/s",
        HudWidget.TimeToApoapsis => c.Orbit.TimeToApoapsis.HasValue ? PanelKit.FormatDuration(c.Orbit.TimeToApoapsis.Value) : "--",
        HudWidget.TimeToPeriapsis => PanelKit.FormatDuration(c.Orbit.TimeToPeriapsis),
        HudWidget.VesselMass => PanelKit.FormatMass(c.Mass),
        HudWidget.Thrust => PanelKit.FormatThrust(c.Thrust),
        HudWidget.VerticalSpeed => $"{c.Orbit.VerticalVelocity:F1} m/s",
        HudWidget.Acceleration => $"{c.AccelerationG:F2} g",
        _ => string.Empty,
    };
}