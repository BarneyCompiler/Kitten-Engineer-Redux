using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace KittenEngineerRedux.UI;

internal static class PanelKit
{
    public static float RowHeight => Math.Max(22f, ImGui.GetTextLineHeight() + 6f);

    public static readonly float2 MinWindowSize = new(240f, 140f);
    public static readonly float2 MaxWindowSize = new(1600f, 4000f);

    public static float4 WindowBgColor = new(0.07f, 0.08f, 0.10f, 0.96f);
    public static float4 BorderColor = new(0.22f, 0.24f, 0.28f, 1f);
    public static float4 HeaderColor = new(0.55f, 0.78f, 0.95f, 1f);
    public static float4 LabelColor = new(0.72f, 0.74f, 0.78f, 1f);
    public static float4 ValueColor = new(0.95f, 0.96f, 0.98f, 1f);
    public static float4 AccentColor = new(0.30f, 0.62f, 0.90f, 1f);
    public static float4 ToggleOffColor = new(0.16f, 0.17f, 0.20f, 1f);
    public static float4 WarningColor = new(0.92f, 0.45f, 0.30f, 1f);
    public static float4 GoodColor = new(0.45f, 0.85f, 0.55f, 1f);
    public static float4 RowSeparatorColor = new(0.22f, 0.24f, 0.28f, 0.35f);
    public static float4 HighlightColor = new(0.30f, 0.62f, 0.90f, 0.22f);
    public static float4 GraphBgColor = new(0.04f, 0.05f, 0.07f, 1f);
    public static float4 GraphLineColor = new(0.45f, 0.85f, 0.55f, 1f);
    public static float4 GraphGridColor = new(0.22f, 0.24f, 0.28f, 0.6f);
    public static float CornerRadius = 4f;
    public static float TextScale = 1f;

    private static float _titleBarHeight = 24f;

    public static bool BeginWindow(ReadOnlySpan<byte> title, float2 defaultPos, float2 defaultSize)
    {
        ImGui.SetNextWindowPos(defaultPos, ImGuiCond.FirstUseEver, null);
        ImGui.SetNextWindowSize(defaultSize, ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(MinWindowSize, MaxWindowSize, (ImGuiSizeCallback?)null);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, CornerRadius);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, CornerRadius);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, ToColor(WindowBgColor));
        ImGui.PushStyleColor(ImGuiCol.Border, ToColor(BorderColor));
        ImGui.PushStyleColor(ImGuiCol.TitleBg, ToColor(WindowBgColor));
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, ToColor(WindowBgColor));
        ImGui.PushStyleColor(ImGuiCol.TitleBgCollapsed, ToColor(WindowBgColor));

        bool open = ImGui.Begin(title, ImGuiWindowFlags.None);
        _titleBarHeight = ImGui.GetFrameHeight();
        ImGui.PushFont(ImGui.GetFont(), ImGui.GetFontSize() * TextScale);
        return open;
    }

    public static void EndWindow()
    {
        ImGui.PopFont();
        ImGui.End();
        ImGui.PopStyleColor(5);
        ImGui.PopStyleVar(2);
    }

    public static void EndContent(float2 origin, float width, float y)
    {
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new float2(width, Math.Max(0f, y - origin.Y)));
    }

    public static void DrawAccentStrip(ImDrawListPtr dl, float2 contentOrigin, float windowHeight)
    {
        float2 winPos = ImGui.GetWindowPos();
        float2 winSize = ImGui.GetWindowSize();
        float2 stripMin = new float2(winPos.X, winPos.Y + _titleBarHeight);
        float2 stripMax = new float2(winPos.X + 3f, winPos.Y + winSize.Y - CornerRadius);
        dl.PushClipRect(stripMin, stripMax, false);
        dl.AddRectFilled(in stripMin, in stripMax, ToColor(AccentColor));
        dl.PopClipRect();
    }

    public static float DrawSectionHeader(ImDrawListPtr dl, float2 origin, float width, float y, string text)
    {
        float lineHeight = ImGui.GetTextLineHeight();
        float2 pos = new float2(origin.X, y + 4f);
        dl.AddText(in pos, ToColor(HeaderColor), text);
        float lineY = y + 4f + lineHeight + 3f;
        float2 lineStart = new float2(origin.X, lineY);
        float2 lineEnd = new float2(origin.X + width, lineY);
        dl.AddLine(in lineStart, in lineEnd, ToColor(BorderColor));
        return lineY + 8f;
    }

    public static bool DrawCollapsibleSection(
        float2 origin, float y, ReadOnlySpan<byte> label, out float nextY, bool defaultOpen = false)
    {
        ImGui.SetCursorScreenPos(new float2(origin.X, y));

        ImGui.PushStyleColor(ImGuiCol.Header, ToColor(ToggleOffColor));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, ToColor(AccentColor));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, ToColor(AccentColor));
        ImGui.PushStyleColor(ImGuiCol.Text, ToColor(HeaderColor));

        bool open = ImGui.CollapsingHeader(label,
            defaultOpen ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None);

        ImGui.PopStyleColor(4);

        nextY = ImGui.GetCursorScreenPos().Y + 6f;
        return open;
    }

    public static float DrawRow(ImDrawListPtr dl, float2 origin, float width, float y, string label, string value, bool warning = false)
    {
        float lineHeight = ImGui.GetTextLineHeight();
        float rowHeight = RowHeight;
        float availableWidth = Math.Max(0f, width - 8f);
        float2 labelSize = ImGui.CalcTextSize(label);
        float2 valueSize = ImGui.CalcTextSize(value);
        float gap = ImGui.GetStyle().ItemSpacing.X;
        ImColor8 valueColor = ToColor(warning ? WarningColor : ValueColor);

        float height;
        if (labelSize.X + valueSize.X + gap <= availableWidth)
        {
            float textY = y + (rowHeight - lineHeight) * 0.5f;
            float2 labelPos = new float2(origin.X, textY);
            dl.AddText(in labelPos, ToColor(LabelColor), label);
            float2 valuePos = new float2(origin.X + width - valueSize.X, textY);
            dl.AddText(in valuePos, valueColor, value);
            height = rowHeight;
        }
        else
        {
            float2 labelPos = new float2(origin.X, y + 2f);
            dl.AddText(in labelPos, ToColor(LabelColor), label);
            float valueX = Math.Max(origin.X, origin.X + width - valueSize.X);
            float2 valuePos = new float2(valueX, y + 2f + lineHeight + 2f);
            dl.AddText(in valuePos, valueColor, value);
            height = lineHeight * 2f + 8f;
        }

        float2 sepStart = new float2(origin.X, y + height - 1f);
        float2 sepEnd = new float2(origin.X + width, y + height - 1f);
        dl.AddLine(in sepStart, in sepEnd, ToColor(RowSeparatorColor));
        return y + height;
    }

    public static float DrawGauge(ImDrawListPtr dl, float2 origin, float width, float y,
        string label, float fraction, string valueText, bool warning)
    {
        float lineHeight = ImGui.GetTextLineHeight();
        float clamped = Math.Clamp(float.IsNaN(fraction) ? 0f : fraction, 0f, 1f);

        float2 labelPos = new float2(origin.X, y + 2f);
        dl.AddText(in labelPos, ToColor(LabelColor), label);
        float2 valueSize = ImGui.CalcTextSize(valueText);
        float2 valuePos = new float2(Math.Max(origin.X, origin.X + width - valueSize.X), y + 2f);
        dl.AddText(in valuePos, ToColor(warning ? WarningColor : ValueColor), valueText);

        float barTop = y + 2f + lineHeight + 3f;
        float barHeight = 8f;
        float2 trackMin = new float2(origin.X, barTop);
        float2 trackMax = new float2(origin.X + width, barTop + barHeight);
        dl.AddRectFilled(in trackMin, in trackMax, ToColor(GraphBgColor), CornerRadius);
        if (clamped > 0.002f)
        {
            float2 fillMax = new float2(origin.X + width * clamped, barTop + barHeight);
            dl.AddRectFilled(in trackMin, in fillMax, ToColor(warning ? WarningColor : GoodColor), CornerRadius);
        }
        return barTop + barHeight + 8f;
    }

    public static bool DrawMiniButton(ImDrawListPtr dl, string label, string id, float2 pos, float width, float height, bool active = false)
    {
        ImGui.SetCursorScreenPos(pos);
        bool clicked = ImGui.InvisibleButton(id, new float2(width, height));
        float2 max = new float2(pos.X + width, pos.Y + height);
        dl.AddRectFilled(in pos, in max, ToColor(active ? AccentColor : ToggleOffColor), CornerRadius);
        float2 textSize = ImGui.CalcTextSize(label);
        float2 textPos = new float2(pos.X + (width - textSize.X) * 0.5f, pos.Y + (height - textSize.Y) * 0.5f);
        dl.AddText(in textPos, ToColor(ValueColor), label);
        return clicked;
    }

    public static float DrawTableRow(ImDrawListPtr dl, float2 origin, float width, float y,
        ReadOnlySpan<string> cells, float[] columnFractions, float4 textColor)
    {
        float rowHeight = RowHeight;
        float lineHeight = ImGui.GetTextLineHeight();
        float textY = y + (rowHeight - lineHeight) * 0.5f;
        ImColor8 color = ToColor(textColor);
        float x = origin.X;
        for (int i = 0; i < cells.Length && i < columnFractions.Length; i++)
        {
            float cellWidth = width * columnFractions[i];
            float2 size = ImGui.CalcTextSize(cells[i]);
            float textX = i == 0 ? x : Math.Max(x, x + cellWidth - size.X);
            float2 pos = new float2(textX, textY);
            dl.AddText(in pos, color, cells[i]);
            x += cellWidth;
        }
        return y + rowHeight;
    }

    public static void DrawRowBackground(ImDrawListPtr dl, float2 origin, float width, float y, float height, float4 color)
    {
        float2 bgMin = new float2(origin.X - 2f, y);
        float2 bgMax = new float2(origin.X + width + 2f, y + height);
        dl.AddRectFilled(in bgMin, in bgMax, ToColor(color), CornerRadius);
    }

    public static float DrawStatTiles(ImDrawListPtr dl, float2 origin, float width, float y,
        ReadOnlySpan<string> labels, ReadOnlySpan<string> values, ReadOnlySpan<bool> warnings = default)
    {
        const float gap = 6f;
        int count = Math.Min(labels.Length, values.Length);
        if (count == 0)
            return y;

        float minTileWidth = 96f * Math.Max(1f, TextScale);
        int perRow = Math.Min(count, Math.Max(1, (int)((width + gap) / (minTileWidth + gap))));
        int rows = (count + perRow - 1) / perRow;
        float tileWidth = (width - gap * (perRow - 1)) / perRow;
        float lineHeight = ImGui.GetTextLineHeight();
        float bigSize = ImGui.GetFontSize() * 1.3f;
        float tileHeight = 5f + lineHeight + 3f + lineHeight * 1.3f + 7f;

        for (int i = 0; i < count; i++)
        {
            bool warning = i < warnings.Length && warnings[i];
            int col = i % perRow;
            int row = i / perRow;
            float2 tileMin = new float2(origin.X + col * (tileWidth + gap), y + row * (tileHeight + gap));
            float2 tileMax = tileMin + new float2(tileWidth, tileHeight);
            dl.AddRectFilled(in tileMin, in tileMax, ToColor(ToggleOffColor), CornerRadius);

            if (warning)
            {
                ImColor8 warnColor = ToColor(WarningColor);
                float2 topRight = new float2(tileMax.X, tileMin.Y);
                float2 bottomLeft = new float2(tileMin.X, tileMax.Y);
                dl.AddLine(in tileMin, in topRight, warnColor);
                dl.AddLine(in topRight, in tileMax, warnColor);
                dl.AddLine(in tileMax, in bottomLeft, warnColor);
                dl.AddLine(in bottomLeft, in tileMin, warnColor);
            }

            float2 labelPos = tileMin + new float2(8f, 5f);
            dl.AddText(in labelPos, ToColor(LabelColor), labels[i]);

            ImGui.PushFont(ImGui.GetFont(), bigSize);
            float2 valueSize = ImGui.CalcTextSize(values[i]);
            bool fits = valueSize.X <= tileWidth - 12f;
            if (!fits)
                ImGui.PopFont();

            float2 valuePos = tileMin + new float2(8f, 5f + lineHeight + 3f);
            dl.AddText(in valuePos, ToColor(warning ? WarningColor : ValueColor), values[i]);

            if (fits)
                ImGui.PopFont();
        }
        return y + rows * (tileHeight + gap) + 2f;
    }

    public static float DrawChips(ImDrawListPtr dl, float2 origin, float width, float y,
        ReadOnlySpan<string> labels, int selected, string idPrefix, out int clicked)
    {
        clicked = -1;
        float chipHeight = ImGui.GetTextLineHeight() + 8f;
        float x = 0f;
        float rowY = y;
        for (int i = 0; i < labels.Length; i++)
        {
            float2 textSize = ImGui.CalcTextSize(labels[i]);
            float chipWidth = textSize.X + 16f;
            if (x > 0f && x + chipWidth > width)
            {
                x = 0f;
                rowY += chipHeight + 4f;
            }

            float2 pos = new float2(origin.X + x, rowY);
            ImGui.SetCursorScreenPos(pos);
            if (ImGui.InvisibleButton(idPrefix + i, new float2(chipWidth, chipHeight)))
                clicked = i;

            float2 max = pos + new float2(chipWidth, chipHeight);
            bool active = i == selected;
            dl.AddRectFilled(in pos, in max, ToColor(active ? AccentColor : ToggleOffColor), CornerRadius);
            float2 textPos = pos + new float2(8f, 4f);
            dl.AddText(in textPos, ToColor(active ? new float4(1f, 1f, 1f, 1f) : LabelColor), labels[i]);
            x += chipWidth + 4f;
        }
        return rowY + chipHeight + 8f;
    }

    public static float DrawSliderRow(
        ImDrawListPtr dl, float2 origin, float width, float y, string label, ReadOnlySpan<byte> id,
        ref float value, float min, float max, string format, out bool changed)
    {
        float rowHeight = Math.Max(RowHeight, ImGui.GetFrameHeight());
        float lineHeight = ImGui.GetTextLineHeight();
        float labelWidth = Math.Min(ImGui.CalcTextSize(label).X + 8f, width * 0.42f);
        float sliderWidth = Math.Max(80f, width - labelWidth - 8f);
        float2 labelPos = new float2(origin.X, y + (rowHeight - lineHeight) * 0.5f);
        dl.AddText(in labelPos, ToColor(LabelColor), label);

        float sliderHeight = ImGui.GetFrameHeight();
        ImGui.SetCursorScreenPos(new float2(
            origin.X + width - sliderWidth,
            y + (rowHeight - sliderHeight) * 0.5f));
        ImGui.SetNextItemWidth(sliderWidth);
        changed = ImGui.SliderFloat(id, ref value, min, max, format);
        return y + rowHeight;
    }

    public static float DrawInputDoubleRow(
        ImDrawListPtr dl, float2 origin, float width, float y, string label, ReadOnlySpan<byte> id,
        ref double value, double step, double fastStep)
    {
        float rowHeight = Math.Max(RowHeight, ImGui.GetFrameHeight());
        float lineHeight = ImGui.GetTextLineHeight();
        float inputWidth = Math.Clamp(width * 0.48f, 110f, 170f);
        float2 labelPos = new float2(origin.X, y + (rowHeight - lineHeight) * 0.5f);
        dl.AddText(in labelPos, ToColor(LabelColor), label);
        float inputHeight = ImGui.GetFrameHeight();
        ImGui.SetCursorScreenPos(new float2(origin.X + width - inputWidth, y + (rowHeight - inputHeight) * 0.5f));
        ImGui.SetNextItemWidth(inputWidth);
        ImGui.InputDouble(id, ref value, step, fastStep, default(ImString), ImGuiInputTextFlags.CharsDecimal);
        return y + rowHeight;
    }

    public static float DrawTotalRow(ImDrawListPtr dl, float2 origin, float width, float y, string label, string value)
    {
        float rowHeight = RowHeight;
        float lineHeight = ImGui.GetTextLineHeight();
        float lineY = y - 2f;
        float2 lineStart = new float2(origin.X, lineY);
        float2 lineEnd = new float2(origin.X + width, lineY);
        dl.AddLine(in lineStart, in lineEnd, ToColor(BorderColor));
        float textY = y + (rowHeight - lineHeight) * 0.5f + 2f;
        float2 labelPos = new float2(origin.X, textY);
        dl.AddText(in labelPos, ToColor(HeaderColor), label);
        float2 valueSize = ImGui.CalcTextSize(value);
        float2 valuePos = new float2(origin.X + width - valueSize.X, textY);
        dl.AddText(in valuePos, ToColor(AccentColor), value);
        return y + rowHeight + 4f;
    }

    public static float DrawTwoWayToggle(ImDrawListPtr dl, float2 origin, float width, float y,
        string leftLabel, string rightLabel, bool rightSelected, string leftId, string rightId, out bool clickedRight, out bool clickedLeft)
    {
        float toggleWidth = (width - 6f) / 2f;
        float toggleHeight = Math.Max(24f, ImGui.GetTextLineHeight() + 10f);

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
        dl.AddRectFilled(in pos, in max, ToColor(fill), CornerRadius);

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

    public static string FormatPressure(float pascals)
    {
        return pascals >= 1000f ? $"{pascals / 1000f:F1} kPa" : $"{pascals:F0} Pa";
    }

    public static string FormatThrust(float newtons)
    {
        if (newtons >= 1_000_000f)
            return $"{newtons / 1_000_000f:F2} MN";
        return newtons >= 1000f ? $"{newtons / 1000f:F1} kN" : $"{newtons:F0} N";
    }

    public static void DrawAppearanceSettings()
    {
        ImGui.Text("Panel colors"u8);
        ImGui.ColorEdit4("Background"u8, ref WindowBgColor, ImGuiColorEditFlags.NoInputs);
        ImGui.ColorEdit4("Accent"u8, ref AccentColor, ImGuiColorEditFlags.NoInputs);
        ImGui.ColorEdit4("Border"u8, ref BorderColor, ImGuiColorEditFlags.NoInputs);
        ImGui.ColorEdit4("Tile and button background"u8, ref ToggleOffColor, ImGuiColorEditFlags.NoInputs);
        ImGui.ColorEdit4("Row separator"u8, ref RowSeparatorColor, ImGuiColorEditFlags.NoInputs);
        ImGui.ColorEdit4("Active stage highlight"u8, ref HighlightColor, ImGuiColorEditFlags.NoInputs);

        ImGui.Text("Text colors"u8);
        ImGui.ColorEdit4("Header text"u8, ref HeaderColor, ImGuiColorEditFlags.NoInputs);
        ImGui.ColorEdit4("Label text"u8, ref LabelColor, ImGuiColorEditFlags.NoInputs);
        ImGui.ColorEdit4("Value text"u8, ref ValueColor, ImGuiColorEditFlags.NoInputs);
        ImGui.ColorEdit4("Warning and alert"u8, ref WarningColor, ImGuiColorEditFlags.NoInputs);
        ImGui.ColorEdit4("Good and fuel gauge"u8, ref GoodColor, ImGuiColorEditFlags.NoInputs);

        ImGui.Text("Graph colors"u8);
        ImGui.ColorEdit4("Graph background"u8, ref GraphBgColor, ImGuiColorEditFlags.NoInputs);
        ImGui.ColorEdit4("Graph line"u8, ref GraphLineColor, ImGuiColorEditFlags.NoInputs);
        ImGui.ColorEdit4("Graph grid"u8, ref GraphGridColor, ImGuiColorEditFlags.NoInputs);

        ImGui.Text("Layout"u8);
        ImGui.SliderFloat("Rounded corners"u8, ref CornerRadius, 0f, 12f, "%.0f px");
        ImGui.SliderFloat("Text scale"u8, ref TextScale, 0.75f, 1.5f, "%.2f x");
        if (ImGui.Button("Reset appearance"u8, (float2?)null))
            ResetAppearance();
    }

    private static void ResetAppearance()
    {
        WindowBgColor = new float4(0.07f, 0.08f, 0.10f, 0.96f);
        BorderColor = new float4(0.22f, 0.24f, 0.28f, 1f);
        HeaderColor = new float4(0.55f, 0.78f, 0.95f, 1f);
        LabelColor = new float4(0.72f, 0.74f, 0.78f, 1f);
        ValueColor = new float4(0.95f, 0.96f, 0.98f, 1f);
        AccentColor = new float4(0.30f, 0.62f, 0.90f, 1f);
        ToggleOffColor = new float4(0.16f, 0.17f, 0.20f, 1f);
        WarningColor = new float4(0.92f, 0.45f, 0.30f, 1f);
        GoodColor = new float4(0.45f, 0.85f, 0.55f, 1f);
        RowSeparatorColor = new float4(0.22f, 0.24f, 0.28f, 0.35f);
        HighlightColor = new float4(0.30f, 0.62f, 0.90f, 0.22f);
        GraphBgColor = new float4(0.04f, 0.05f, 0.07f, 1f);
        GraphLineColor = new float4(0.45f, 0.85f, 0.55f, 1f);
        GraphGridColor = new float4(0.22f, 0.24f, 0.28f, 0.6f);
        CornerRadius = 4f;
        TextScale = 1f;
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