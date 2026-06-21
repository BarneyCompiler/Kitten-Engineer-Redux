using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace KittenEngineerRedux.UI;

internal static class PanelKit
{
    public const float RowHeight = 22f;

    public static readonly float2 MinWindowSize = new(220f, 140f);
    public static readonly float2 MaxWindowSize = new(700f, 1200f);

    private static readonly float4 WindowBgColor = new(0.07f, 0.08f, 0.10f, 0.96f);
    private static readonly float4 BorderColor = new(0.22f, 0.24f, 0.28f, 1f);
    private static readonly float4 HeaderColor = new(0.55f, 0.78f, 0.95f, 1f);
    private static readonly float4 LabelColor = new(0.72f, 0.74f, 0.78f, 1f);
    private static readonly float4 ValueColor = new(0.95f, 0.96f, 0.98f, 1f);
    private static readonly float4 AccentColor = new(0.30f, 0.62f, 0.90f, 1f);
    private static readonly float4 ToggleOffColor = new(0.16f, 0.17f, 0.20f, 1f);
    private static readonly float4 WarningColor = new(0.92f, 0.45f, 0.30f, 1f);
    private static readonly float4 GoodColor = new(0.45f, 0.85f, 0.55f, 1f);

    public static bool BeginWindow(ReadOnlySpan<byte> title, float2 defaultPos, float2 defaultSize)
    {
        ImGui.SetNextWindowPos(defaultPos, ImGuiCond.FirstUseEver, null);
        ImGui.SetNextWindowSize(defaultSize, ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(MinWindowSize, MaxWindowSize, null);

        ImGui.PushStyleColor(ImGuiCol.WindowBg, ToColor(WindowBgColor));
        ImGui.PushStyleColor(ImGuiCol.Border, ToColor(BorderColor));
        ImGui.PushStyleColor(ImGuiCol.TitleBg, ToColor(WindowBgColor));
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, ToColor(WindowBgColor));
        ImGui.PushStyleColor(ImGuiCol.TitleBgCollapsed, ToColor(WindowBgColor));

        return ImGui.Begin(title, ImGuiWindowFlags.None);
    }

    public static void EndWindow()
    {
        ImGui.End();
        ImGui.PopStyleColor(5);
    }

    public static void DrawAccentStrip(ImDrawListPtr dl, float2 contentOrigin, float windowHeight)
    {
        float2 accentMin = contentOrigin + new float2(-ImGui.GetStyle().WindowPadding.X, -2f);
        float2 accentMax = new float2(accentMin.X + 3f, contentOrigin.Y + windowHeight);
        dl.PushClipRect(contentOrigin, contentOrigin + new float2(10000f, windowHeight), false);
        dl.AddRectFilled(in accentMin, in accentMax, ToColor(AccentColor));
        dl.PopClipRect();
    }

    public static float DrawSectionHeader(ImDrawListPtr dl, float2 origin, float width, float y, string text)
    {
        float2 pos = new float2(origin.X, y + 4f);
        dl.AddText(in pos, ToColor(HeaderColor), text);
        float lineY = y + 20f;
        float2 lineStart = new float2(origin.X, lineY);
        float2 lineEnd = new float2(origin.X + width, lineY);
        dl.AddLine(in lineStart, in lineEnd, ToColor(BorderColor));
        return lineY + 10f;
    }

    public static bool DrawCollapsibleSection(float2 origin, float y, ReadOnlySpan<byte> label, out float nextY)
    {
        ImGui.SetCursorScreenPos(new float2(origin.X, y));

        ImGui.PushStyleColor(ImGuiCol.Header, ToColor(ToggleOffColor));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, ToColor(AccentColor));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, ToColor(AccentColor));
        ImGui.PushStyleColor(ImGuiCol.Text, ToColor(HeaderColor));

        bool open = ImGui.CollapsingHeader(label, ImGuiTreeNodeFlags.DefaultOpen);

        ImGui.PopStyleColor(4);

        nextY = ImGui.GetCursorScreenPos().Y + 6f;
        return open;
    }

    public static float DrawRow(ImDrawListPtr dl, float2 origin, float width, float y, string label, string value, bool warning = false)
    {
        float2 labelPos = new float2(origin.X, y);
        dl.AddText(in labelPos, ToColor(LabelColor), label);
        float2 valueSize = ImGui.CalcTextSize(value);
        float2 valuePos = new float2(origin.X + width - valueSize.X, y);
        dl.AddText(in valuePos, ToColor(warning ? WarningColor : ValueColor), value);
        return y + RowHeight;
    }

    public static float DrawTotalRow(ImDrawListPtr dl, float2 origin, float width, float y, string label, string value)
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

    public static float DrawTwoWayToggle(ImDrawListPtr dl, float2 origin, float width, float y,
        string leftLabel, string rightLabel, bool rightSelected, string leftId, string rightId, out bool clickedRight, out bool clickedLeft)
    {
        float toggleWidth = (width - 6f) / 2f;
        float toggleHeight = 24f;

        float2 leftPos = new float2(origin.X, y);
        float2 rightPos = new float2(leftPos.X + toggleWidth + 6f, y);

        clickedLeft = DrawToggleButton(dl, leftPos, toggleWidth, toggleHeight, leftLabel, !rightSelected, leftId);
        clickedRight = DrawToggleButton(dl, rightPos, toggleWidth, toggleHeight, rightLabel, rightSelected, rightId);

        return y + toggleHeight + 8f;
    }

    private static bool DrawToggleButton(ImDrawListPtr dl, float2 pos, float width, float height, string label, bool active, string id)
    {
        ImGui.SetCursorScreenPos(pos);
        bool clicked = ImGui.InvisibleButton(id, new float2(width, height));

        float2 max = pos + new float2(width, height);
        float4 fill = active ? AccentColor : ToggleOffColor;
        dl.AddRectFilled(in pos, in max, ToColor(fill), 4f);

        float2 textSize = ImGui.CalcTextSize(label);
        float2 textPos = pos + new float2((width - textSize.X) / 2f, (height - textSize.Y) / 2f);
        float4 textColor = active ? new float4(1f, 1f, 1f, 1f) : LabelColor;
        dl.AddText(in textPos, ToColor(textColor), label);

        return clicked;
    }

    public static string FormatMass(float kg)
    {
        return kg >= 1000f ? $"{kg / 1000f:F2} t" : $"{kg:F1} kg";
    }

    public static string FormatDuration(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds))
            return "N/A";
        string sign = seconds < 0 ? "-" : "";
        double abs = Math.Abs(seconds);
        if (abs < 60.0)
            return $"{sign}{abs:F0}s";
        if (abs < 3600.0)
        {
            int m = (int)(abs / 60.0);
            int s = (int)(abs % 60.0);
            return s > 0 ? $"{sign}{m}m {s}s" : $"{sign}{m}m";
        }
        if (abs < 86400.0)
        {
            int h = (int)(abs / 3600.0);
            int min = (int)(abs % 3600.0 / 60.0);
            return min > 0 ? $"{sign}{h}h {min}m" : $"{sign}{h}h";
        }
        int d = (int)(abs / 86400.0);
        int hr = (int)(abs % 86400.0 / 3600.0);
        return hr > 0 ? $"{sign}{d}d {hr}h" : $"{sign}{d}d";
    }

    public static ImColor8 ToColor(float4 color) => ImGui.ColorConvertFloat4ToU32(color);

    public static ImColor8 GoodColorU32() => ToColor(GoodColor);
    public static ImColor8 WarningColorU32() => ToColor(WarningColor);
}