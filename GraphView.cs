using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace KittenEngineerRedux.UI;

internal static class GraphView
{
    private static readonly float[] TimeSteps = { 1f, 2f, 5f, 10f, 15f, 30f, 60f, 120f, 300f, 600f };

    public static float Draw(ImDrawListPtr dl, float2 origin, float width, float y,
        string title, string unit, ReadOnlySpan<float> samples, float sampleInterval,
        float? thresholdValue, bool thresholdAbove, bool zeroBaseline)
    {
        const float pad = 10f;
        float side = Math.Clamp(width, 220f, 640f);
        float x0 = origin.X + Math.Max(0f, (width - side) * 0.5f);
        float lineHeight = ImGui.GetTextLineHeight();
        ImColor8 labelColor = PanelKit.ToColor(PanelKit.LabelColor);
        ImColor8 valueColor = PanelKit.ToColor(PanelKit.ValueColor);
        ImColor8 gridColor = PanelKit.ToColor(PanelKit.GraphGridColor);

        float2 boxMin = new float2(x0, y);
        float2 boxMax = new float2(x0 + side, y + side);
        dl.AddRectFilled(in boxMin, in boxMax, PanelKit.ToColor(PanelKit.GraphBgColor), PanelKit.CornerRadius);

        ImGui.PushFont(ImGui.GetFont(), ImGui.GetFontSize() * 1.15f);
        float2 titlePos = new float2(x0 + pad, y + 8f);
        dl.AddText(in titlePos, PanelKit.ToColor(PanelKit.HeaderColor), title);
        ImGui.PopFont();
        float titleHeight = lineHeight * 1.15f;

        if (samples.Length < 2)
        {
            string waiting = "Collecting samples";
            float2 waitingSize = ImGui.CalcTextSize(waiting);
            float2 waitingPos = new float2(x0 + (side - waitingSize.X) * 0.5f, y + (side - waitingSize.Y) * 0.5f);
            dl.AddText(in waitingPos, labelColor, waiting);
            return y + side + 8f;
        }

        float min = samples[0];
        float max = samples[0];
        float sum = 0f;
        int minIndex = 0;
        int maxIndex = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            float v = samples[i];
            sum += v;
            if (v < min) { min = v; minIndex = i; }
            if (v > max) { max = v; maxIndex = i; }
        }
        float average = sum / samples.Length;
        float latest = samples[samples.Length - 1];

        float statsY = y + 8f + titleHeight + 6f;
        float statColumn = (side - pad * 2f) / 4f;
        DrawStat(dl, x0 + pad, statsY, "NOW", FormatPrecise(latest), labelColor, valueColor, lineHeight);
        DrawStat(dl, x0 + pad + statColumn, statsY, "MIN", FormatPrecise(min), labelColor, valueColor, lineHeight);
        DrawStat(dl, x0 + pad + statColumn * 2f, statsY, "AVG", FormatPrecise(average), labelColor, valueColor, lineHeight);
        DrawStat(dl, x0 + pad + statColumn * 3f, statsY, "MAX", FormatPrecise(max), labelColor, valueColor, lineHeight);

        float plotTop = statsY + lineHeight * 2f + 14f;
        float plotBottom = y + side - (lineHeight + 14f);
        float plotHeight = Math.Max(30f, plotBottom - plotTop);

        float rangeMin = min;
        float rangeMax = max;
        if (thresholdValue.HasValue)
        {
            rangeMin = Math.Min(rangeMin, thresholdValue.Value);
            rangeMax = Math.Max(rangeMax, thresholdValue.Value);
        }
        if (zeroBaseline && rangeMin >= 0f && rangeMin < rangeMax * 0.5f)
            rangeMin = 0f;

        int maxTicks = Math.Clamp((int)(plotHeight / (lineHeight * 2.4f)), 3, 8);
        NiceAxis(rangeMin, rangeMax, maxTicks, out float axisMin, out float axisMax, out float step);
        float axisRange = Math.Max(axisMax - axisMin, 1e-6f);
        int tickCount = Math.Clamp((int)MathF.Round(axisRange / step), 1, 20);

        float labelWidth = 0f;
        for (int i = 0; i <= tickCount; i++)
            labelWidth = Math.Max(labelWidth, ImGui.CalcTextSize(FormatTick(axisMin + i * step, step)).X);

        float plotLeft = x0 + labelWidth + 16f;
        float plotRight = x0 + side - 14f;
        float plotWidth = Math.Max(30f, plotRight - plotLeft);

        float YOf(float value) => plotBottom - (value - axisMin) / axisRange * plotHeight;

        for (int i = 0; i <= tickCount; i++)
        {
            float value = axisMin + i * step;
            float yy = YOf(value);
            float2 gridStart = new float2(plotLeft, yy);
            float2 gridEnd = new float2(plotRight, yy);
            dl.AddLine(in gridStart, in gridEnd, gridColor);

            string tickLabel = FormatTick(value, step);
            float2 tickSize = ImGui.CalcTextSize(tickLabel);
            float2 tickPos = new float2(plotLeft - 8f - tickSize.X, yy - tickSize.Y * 0.5f);
            dl.AddText(in tickPos, labelColor, tickLabel);
        }

        float span = (samples.Length - 1) * sampleInterval;
        int maxDivisions = Math.Clamp((int)(plotWidth / 70f), 2, 8);
        float timeStep = TimeSteps[TimeSteps.Length - 1];
        for (int i = 0; i < TimeSteps.Length; i++)
        {
            if (span / TimeSteps[i] <= maxDivisions)
            {
                timeStep = TimeSteps[i];
                break;
            }
        }
        for (float t = 0f; t <= span + 0.001f; t += timeStep)
        {
            float xx = plotRight - t / span * plotWidth;
            float2 gridStart = new float2(xx, plotTop);
            float2 gridEnd = new float2(xx, plotBottom);
            dl.AddLine(in gridStart, in gridEnd, gridColor);

            string tickLabel = FormatTime(t);
            float2 tickSize = ImGui.CalcTextSize(tickLabel);
            float tickX = Math.Clamp(xx - tickSize.X * 0.5f, x0 + 2f, x0 + side - tickSize.X - 2f);
            float2 tickPos = new float2(tickX, plotBottom + 5f);
            dl.AddText(in tickPos, labelColor, tickLabel);
        }

        ImColor8 axisColor = PanelKit.ToColor(PanelKit.BorderColor);
        float2 axisOrigin = new float2(plotLeft, plotBottom);
        float2 axisTop = new float2(plotLeft, plotTop);
        float2 axisRight = new float2(plotRight, plotBottom);
        dl.AddLine(in axisOrigin, in axisTop, axisColor);
        dl.AddLine(in axisOrigin, in axisRight, axisColor);

        if (thresholdValue.HasValue)
        {
            float thresholdY = YOf(thresholdValue.Value);
            ImColor8 warningColor = PanelKit.ToColor(PanelKit.WarningColor);
            for (float dx = plotLeft; dx < plotRight; dx += 10f)
            {
                float2 dashStart = new float2(dx, thresholdY);
                float2 dashEnd = new float2(Math.Min(dx + 5f, plotRight), thresholdY);
                dl.AddLine(in dashStart, in dashEnd, warningColor);
            }
            string thresholdLabel = $"alert {(thresholdAbove ? ">" : "<")} {FormatPrecise(thresholdValue.Value)}";
            float2 thresholdSize = ImGui.CalcTextSize(thresholdLabel);
            float labelY = thresholdY - thresholdSize.Y - 2f;
            if (labelY < plotTop)
                labelY = thresholdY + 2f;
            float2 thresholdPos = new float2(plotRight - thresholdSize.X - 4f, labelY);
            dl.AddText(in thresholdPos, warningColor, thresholdLabel);
        }

        float4 lineBase = PanelKit.GraphLineColor;
        ImColor8 lineColor = PanelKit.ToColor(lineBase);
        ImColor8 fillColor = PanelKit.ToColor(new float4(lineBase.X, lineBase.Y, lineBase.Z, 0.16f));
        int pointCount = Math.Min(samples.Length, Math.Max(2, (int)plotWidth));
        float2 previous = default;
        for (int k = 0; k < pointCount; k++)
        {
            int index = (int)MathF.Round(k * (samples.Length - 1) / (float)(pointCount - 1));
            float px = plotLeft + plotWidth * k / (pointCount - 1);
            float py = YOf(samples[index]);
            float2 point = new float2(px, py);
            if (k > 0)
            {
                float2 fillMin = new float2(previous.X, py);
                float2 fillMax = new float2(px, plotBottom);
                dl.AddRectFilled(in fillMin, in fillMax, fillColor);
                for (int pass = -1; pass <= 1; pass++)
                {
                    float2 passStart = new float2(previous.X, previous.Y + pass);
                    float2 passEnd = new float2(point.X, point.Y + pass);
                    dl.AddLine(in passStart, in passEnd, lineColor);
                }
            }
            previous = point;
        }

        if (max - min > 1e-6f)
        {
            DrawExtremum(dl, samples, maxIndex, "max", plotLeft, plotRight, plotTop, plotBottom, plotWidth, YOf(samples[maxIndex]), true, labelColor, valueColor, lineHeight);
            DrawExtremum(dl, samples, minIndex, "min", plotLeft, plotRight, plotTop, plotBottom, plotWidth, YOf(samples[minIndex]), false, labelColor, valueColor, lineHeight);
        }

        float2 markerMin = new float2(previous.X - 4f, previous.Y - 4f);
        float2 markerMax = new float2(previous.X + 4f, previous.Y + 4f);
        dl.AddRectFilled(in markerMin, in markerMax, valueColor);

        float2 mouse = ImGui.GetMousePos();
        if (mouse.X >= plotLeft && mouse.X <= plotRight && mouse.Y >= plotTop && mouse.Y <= plotBottom)
        {
            int hoverIndex = Math.Clamp((int)MathF.Round((mouse.X - plotLeft) / plotWidth * (samples.Length - 1)), 0, samples.Length - 1);
            float hoverValue = samples[hoverIndex];
            float hoverX = plotLeft + plotWidth * hoverIndex / (samples.Length - 1);
            float hoverY = YOf(hoverValue);
            float age = (samples.Length - 1 - hoverIndex) * sampleInterval;

            ImColor8 crossColor = PanelKit.ToColor(new float4(1f, 1f, 1f, 0.35f));
            float2 crossTop = new float2(hoverX, plotTop);
            float2 crossBottom = new float2(hoverX, plotBottom);
            dl.AddLine(in crossTop, in crossBottom, crossColor);

            float2 hoverMin = new float2(hoverX - 4f, hoverY - 4f);
            float2 hoverMax = new float2(hoverX + 4f, hoverY + 4f);
            dl.AddRectFilled(in hoverMin, in hoverMax, valueColor);

            string line1 = unit.Length > 0 ? $"{FormatPrecise(hoverValue)} {unit}" : FormatPrecise(hoverValue);
            string line2 = age < 0.01f ? "now" : $"{FormatTime(age).Replace("-", string.Empty)} ago";
            float2 size1 = ImGui.CalcTextSize(line1);
            float2 size2 = ImGui.CalcTextSize(line2);
            float boxWidth = Math.Max(size1.X, size2.X) + 16f;
            float boxHeight = lineHeight * 2f + 12f;
            float boxX = hoverX + 14f;
            if (boxX + boxWidth > x0 + side - 4f)
                boxX = hoverX - 14f - boxWidth;
            float boxY = Math.Clamp(hoverY - boxHeight - 10f, y + 4f, y + side - boxHeight - 4f);

            float2 borderMin = new float2(boxX - 1f, boxY - 1f);
            float2 borderMax = new float2(boxX + boxWidth + 1f, boxY + boxHeight + 1f);
            dl.AddRectFilled(in borderMin, in borderMax, PanelKit.ToColor(PanelKit.AccentColor), PanelKit.CornerRadius);
            float2 tipMin = new float2(boxX, boxY);
            float2 tipMax = new float2(boxX + boxWidth, boxY + boxHeight);
            dl.AddRectFilled(in tipMin, in tipMax, PanelKit.ToColor(PanelKit.WindowBgColor), PanelKit.CornerRadius);
            float2 line1Pos = new float2(boxX + 8f, boxY + 5f);
            dl.AddText(in line1Pos, valueColor, line1);
            float2 line2Pos = new float2(boxX + 8f, boxY + 5f + lineHeight + 2f);
            dl.AddText(in line2Pos, labelColor, line2);
        }

        return y + side + 8f;
    }

    private static void DrawStat(ImDrawListPtr dl, float x, float y, string label, string value,
        ImColor8 labelColor, ImColor8 valueColor, float lineHeight)
    {
        float2 labelPos = new float2(x, y);
        dl.AddText(in labelPos, labelColor, label);
        float2 valuePos = new float2(x, y + lineHeight + 1f);
        dl.AddText(in valuePos, valueColor, value);
    }

    private static void DrawExtremum(ImDrawListPtr dl, ReadOnlySpan<float> samples, int index, string name,
        float plotLeft, float plotRight, float plotTop, float plotBottom, float plotWidth, float pointY,
        bool above, ImColor8 labelColor, ImColor8 valueColor, float lineHeight)
    {
        float pointX = plotLeft + plotWidth * index / (samples.Length - 1);
        float2 markerMin = new float2(pointX - 3f, pointY - 3f);
        float2 markerMax = new float2(pointX + 3f, pointY + 3f);
        dl.AddRectFilled(in markerMin, in markerMax, valueColor);

        string text = $"{name} {FormatPrecise(samples[index])}";
        float2 size = ImGui.CalcTextSize(text);
        float textX = Math.Clamp(pointX - size.X * 0.5f, plotLeft + 2f, plotRight - size.X - 2f);
        float textY = above ? pointY - lineHeight - 6f : pointY + 6f;
        textY = Math.Clamp(textY, plotTop + 2f, plotBottom - lineHeight - 2f);
        float2 textPos = new float2(textX, textY);
        dl.AddText(in textPos, labelColor, text);
    }

    private static string FormatTime(float seconds)
    {
        if (seconds < 0.01f)
            return "now";
        if (seconds < 60f)
            return $"-{seconds:0.#}s";
        return $"-{seconds / 60f:0.#}m";
    }

    public static string FormatTick(float value, float step)
    {
        float abs = Math.Abs(value);
        if (abs >= 1_000_000f)
            return $"{value / 1_000_000f:0.##}M";
        if (abs >= 1000f)
            return $"{value / 1000f:0.##}k";
        if (step >= 1f)
            return $"{value:F0}";
        if (step >= 0.1f)
            return $"{value:F1}";
        if (step >= 0.01f)
            return $"{value:F2}";
        return $"{value:F3}";
    }

    public static string FormatPrecise(float value)
    {
        float abs = Math.Abs(value);
        if (abs >= 1_000_000f)
            return $"{value / 1_000_000f:F2}M";
        if (abs >= 10_000f)
            return $"{value / 1000f:F1}k";
        if (abs >= 100f)
            return $"{value:F0}";
        if (abs >= 1f)
            return $"{value:F2}";
        return $"{value:F3}";
    }

    private static void NiceAxis(float min, float max, int maxTicks, out float axisMin, out float axisMax, out float step)
    {
        float range = max - min;
        if (range < 1e-6f)
        {
            float pad = Math.Max(Math.Abs(max) * 0.1f, 1f);
            min -= pad;
            max += pad;
            range = max - min;
        }

        float rough = range / Math.Max(1, maxTicks - 1);
        float magnitude = MathF.Pow(10f, MathF.Floor(MathF.Log10(rough)));
        float normalized = rough / magnitude;
        float nice = normalized <= 1f ? 1f : normalized <= 2f ? 2f : normalized <= 5f ? 5f : 10f;
        step = nice * magnitude;
        axisMin = MathF.Floor(min / step) * step;
        axisMax = MathF.Ceiling(max / step) * step;
    }
}