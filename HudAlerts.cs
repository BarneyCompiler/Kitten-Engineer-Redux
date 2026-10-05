using System;
using System.Text;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KittenEngineerRedux.UI;

namespace KittenEngineerRedux.Flight;

internal enum AlertMetric
{
    TotalDeltaV,
    ActualTwr,
    MaximumTwr,
    Altitude,
    PeriapsisAltitude,
    ApoapsisAltitude,
    TimeToPeriapsis,
    TimeToApoapsis,
    VerticalSpeed,
    Acceleration,
    TotalFuel,
    StageFuel,
}

internal sealed class AlertRule
{
    public AlertRule(AlertMetric metric, string name, string unit, bool above, double threshold, double step)
    {
        Metric = metric;
        Name = name;
        Unit = unit;
        DefaultAbove = above;
        DefaultThreshold = threshold;
        Above = above;
        Threshold = threshold;
        Step = step;
        EnableId = $"##KerAlertEnable{(int)metric}";
        DirectionId = $"##KerAlertDirection{(int)metric}";
        InputId = Encoding.UTF8.GetBytes($"##KerAlertValue{(int)metric}");
    }

    public AlertMetric Metric { get; }
    public string Name { get; }
    public string Unit { get; }
    public double Step { get; }
    public bool DefaultAbove { get; }
    public double DefaultThreshold { get; }
    public string EnableId { get; }
    public string DirectionId { get; }
    public byte[] InputId { get; }
    public bool Enabled;
    public bool Above;
    public double Threshold;
    public double LastValue;
    public bool Triggered;
}

internal static class HudAlerts
{
    private const float RowNameWidth = 170f;
    private const float PanelWidth = 430f;

    public static bool Enabled { get; set; } = true;

    public static readonly AlertRule[] Rules =
    {
        new(AlertMetric.TotalDeltaV, "Total dV", "m/s", false, 500.0, 50.0),
        new(AlertMetric.ActualTwr, "TWR (actual)", "", false, 1.0, 0.1),
        new(AlertMetric.MaximumTwr, "TWR (max)", "", false, 1.0, 0.1),
        new(AlertMetric.Altitude, "Altitude", "m", false, 1000.0, 100.0),
        new(AlertMetric.PeriapsisAltitude, "Periapsis", "m", false, 70000.0, 1000.0),
        new(AlertMetric.ApoapsisAltitude, "Apoapsis", "m", false, 70000.0, 1000.0),
        new(AlertMetric.TimeToPeriapsis, "Time to periapsis", "s", false, 30.0, 5.0),
        new(AlertMetric.TimeToApoapsis, "Time to apoapsis", "s", false, 30.0, 5.0),
        new(AlertMetric.VerticalSpeed, "Vertical speed", "m/s", false, -50.0, 5.0),
        new(AlertMetric.Acceleration, "Acceleration", "g", true, 6.0, 0.5),
        new(AlertMetric.TotalFuel, "Total fuel", "%", false, 15.0, 5.0),
        new(AlertMetric.StageFuel, "Active stage fuel", "%", false, 10.0, 5.0),
    };

    private static readonly List<AlertRule> Active = new();

    public static void Evaluate(HudWidgetContext context)
    {
        Active.Clear();
        for (int i = 0; i < Rules.Length; i++)
            Rules[i].Triggered = false;

        if (!Enabled)
            return;

        for (int i = 0; i < Rules.Length; i++)
        {
            AlertRule rule = Rules[i];
            if (!rule.Enabled || !TryGetValue(rule.Metric, context, out double value))
                continue;

            rule.LastValue = value;
            rule.Triggered = rule.Above ? value > rule.Threshold : value < rule.Threshold;
            if (rule.Triggered)
                Active.Add(rule);
        }
    }

    public static bool IsTriggered(AlertMetric metric)
    {
        for (int i = 0; i < Rules.Length; i++)
        {
            if (Rules[i].Metric == metric && Rules[i].Triggered)
                return true;
        }
        return false;
    }

    public static bool TryGetThreshold(AlertMetric metric, out float threshold, out bool above)
    {
        threshold = 0f;
        above = false;
        if (!Enabled)
            return false;

        for (int i = 0; i < Rules.Length; i++)
        {
            AlertRule rule = Rules[i];
            if (rule.Metric != metric || !rule.Enabled)
                continue;
            threshold = (float)rule.Threshold;
            above = rule.Above;
            return true;
        }
        return false;
    }

    public static void ResetRules()
    {
        for (int i = 0; i < Rules.Length; i++)
        {
            Rules[i].Enabled = false;
            Rules[i].Above = Rules[i].DefaultAbove;
            Rules[i].Threshold = Rules[i].DefaultThreshold;
        }
        Enabled = true;
    }

    public static float DrawBanner(ImDrawListPtr dl, float2 origin, float width, float y)
    {
        if (Active.Count == 0)
            return y;

        float rowHeight = PanelKit.RowHeight;
        float lineHeight = ImGui.GetTextLineHeight();
        float height = Active.Count * rowHeight + 8f;
        float4 warning = PanelKit.WarningColor;

        float2 bgMin = new float2(origin.X, y);
        float2 bgMax = new float2(origin.X + width, y + height);
        dl.AddRectFilled(in bgMin, in bgMax, PanelKit.ToColor(new float4(warning.X, warning.Y, warning.Z, 0.18f)), PanelKit.CornerRadius);
        float2 barMax = new float2(origin.X + 3f, y + height);
        dl.AddRectFilled(in bgMin, in barMax, PanelKit.ToColor(warning), PanelKit.CornerRadius);

        float textY = y + 4f + (rowHeight - lineHeight) * 0.5f;
        for (int i = 0; i < Active.Count; i++)
        {
            AlertRule rule = Active[i];
            string unit = rule.Unit.Length > 0 ? $" {rule.Unit}" : string.Empty;
            string text = $"{rule.Name}: {FormatNumber(rule.LastValue)}{unit} ({(rule.Above ? ">" : "<")} {FormatNumber(rule.Threshold)})";
            float2 textPos = new float2(origin.X + 12f, textY + i * rowHeight);
            dl.AddText(in textPos, PanelKit.ToColor(warning), text);
        }
        return y + height + 6f;
    }

    public static void DrawConfigurator()
    {
        if (ImGui.Button("Configure alerts"u8, (float2?)null))
            ImGui.OpenPopup("KER Alert Config");
        if (!ImGui.BeginPopup("KER Alert Config"))
            return;
        DrawRules();
        ImGui.EndPopup();
    }

    private static void DrawRules()
    {
        ImDrawListPtr dl = ImGui.GetWindowDrawList();
        float2 start = ImGui.GetCursorScreenPos();
        float frameHeight = ImGui.GetFrameHeight();
        float rowHeight = Math.Max(28f, frameHeight + 4f);
        float buttonHeight = rowHeight - 4f;
        float lineHeight = ImGui.GetTextLineHeight();
        float y = start.Y;

        if (PanelKit.DrawMiniButton(dl, Enabled ? "Alerts: ON" : "Alerts: OFF", "##KerAlertMaster",
            new float2(start.X, y + 2f), 110f, buttonHeight, Enabled))
            Enabled = !Enabled;
        y += rowHeight + 6f;

        for (int i = 0; i < Rules.Length; i++)
        {
            AlertRule rule = Rules[i];
            float nameX = start.X + 54f;
            float directionX = nameX + RowNameWidth;
            float inputX = directionX + 36f;

            if (PanelKit.DrawMiniButton(dl, rule.Enabled ? "ON" : "OFF", rule.EnableId,
                new float2(start.X, y + 2f), 44f, buttonHeight, rule.Enabled))
                rule.Enabled = !rule.Enabled;

            float2 namePos = new float2(nameX, y + (rowHeight - lineHeight) * 0.5f);
            dl.AddText(in namePos, PanelKit.ToColor(rule.Enabled ? PanelKit.ValueColor : PanelKit.LabelColor), rule.Name);

            if (PanelKit.DrawMiniButton(dl, rule.Above ? ">" : "<", rule.DirectionId,
                new float2(directionX, y + 2f), 30f, buttonHeight))
                rule.Above = !rule.Above;

            ImGui.SetCursorScreenPos(new float2(inputX, y + (rowHeight - frameHeight) * 0.5f));
            ImGui.SetNextItemWidth(110f);
            ImGui.InputDouble(rule.InputId, ref rule.Threshold, rule.Step, rule.Step * 10.0, default(ImString), ImGuiInputTextFlags.CharsDecimal);

            if (rule.Unit.Length > 0)
            {
                float2 unitPos = new float2(inputX + 118f, y + (rowHeight - lineHeight) * 0.5f);
                dl.AddText(in unitPos, PanelKit.ToColor(PanelKit.LabelColor), rule.Unit);
            }
            y += rowHeight;
        }

        y += 6f;
        if (PanelKit.DrawMiniButton(dl, "Reset alerts", "##KerAlertReset", new float2(start.X, y), 120f, buttonHeight))
            ResetRules();
        y += rowHeight;

        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(new float2(PanelWidth, y - start.Y));
    }

    private static bool TryGetValue(AlertMetric metric, HudWidgetContext c, out double value)
    {
        value = 0.0;
        switch (metric)
        {
            case AlertMetric.TotalDeltaV:
                value = c.TotalDeltaV;
                return true;
            case AlertMetric.ActualTwr:
                value = c.ActualTwr;
                return true;
            case AlertMetric.MaximumTwr:
                value = c.MaximumTwr;
                return true;
            case AlertMetric.Altitude:
                value = c.Orbit.SeaLevelAltitude;
                return true;
            case AlertMetric.PeriapsisAltitude:
                value = Math.Max(0.0, c.Orbit.PeriapsisAltitude);
                return true;
            case AlertMetric.ApoapsisAltitude:
                if (!c.Orbit.ApoapsisAltitude.HasValue)
                    return false;
                value = c.Orbit.ApoapsisAltitude.Value;
                return true;
            case AlertMetric.TimeToPeriapsis:
                value = c.Orbit.TimeToPeriapsis;
                return !double.IsNaN(value) && !double.IsInfinity(value);
            case AlertMetric.TimeToApoapsis:
                if (!c.Orbit.TimeToApoapsis.HasValue)
                    return false;
                value = c.Orbit.TimeToApoapsis.Value;
                return !double.IsNaN(value) && !double.IsInfinity(value);
            case AlertMetric.VerticalSpeed:
                value = c.Orbit.VerticalVelocity;
                return true;
            case AlertMetric.Acceleration:
                value = c.AccelerationG;
                return true;
            case AlertMetric.TotalFuel:
                if (float.IsNaN(c.TotalFuelFraction))
                    return false;
                value = c.TotalFuelFraction * 100.0;
                return true;
            case AlertMetric.StageFuel:
                if (float.IsNaN(c.ActiveStageFuelFraction))
                    return false;
                value = c.ActiveStageFuelFraction * 100.0;
                return true;
            default:
                return false;
        }
    }

    private static string FormatNumber(double value)
    {
        double abs = Math.Abs(value);
        if (abs >= 10000.0)
            return $"{value / 1000.0:F1}k";
        if (abs >= 100.0)
            return $"{value:F0}";
        return $"{value:F2}";
    }
}