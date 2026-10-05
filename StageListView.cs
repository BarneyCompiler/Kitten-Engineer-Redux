using Brutal.ImGuiApi;
using Brutal.Numerics;
using KittenEngineerRedux.Analysis;

namespace KittenEngineerRedux.UI;

internal static class StageListView
{
    private const float DetailIndent = 12f;
    private const float FuelBarHeight = 3f;
    private const float LowFuelFraction = 0.2f;

    private static readonly HashSet<int> Expanded = new();
    private static readonly float[] Columns = { 0.16f, 0.30f, 0.27f, 0.27f };

    public static float Draw(ImDrawListPtr dl, float2 origin, float width, float y,
        VehicleBurnAnalysis burn, bool showFeedDiagnostics, int highlightSequence = -1)
    {
        if (burn.Sequences.Count == 0)
            return PanelKit.DrawRow(dl, origin, width, y, "Stages", "None");

        float rowHeight = PanelKit.RowHeight;
        float fullRowHeight = rowHeight + FuelBarHeight + 2f;

        y = PanelKit.DrawTableRow(dl, origin, width, y, ["STG", "dV (m/s)", "BURN", "TWR"], Columns, PanelKit.HeaderColor);
        float2 ruleStart = new float2(origin.X, y - 2f);
        float2 ruleEnd = new float2(origin.X + width, y - 2f);
        dl.AddLine(in ruleStart, in ruleEnd, PanelKit.ToColor(PanelKit.BorderColor));

        foreach (SequenceBurnInfo stage in burn.Sequences)
        {
            int id = stage.SequenceNumber;
            bool expanded = Expanded.Contains(id);
            float rowTop = y;

            ImGui.SetCursorScreenPos(new float2(origin.X, rowTop));
            if (ImGui.InvisibleButton($"##KerStageRow{id}", new float2(width, fullRowHeight)))
            {
                if (!Expanded.Remove(id))
                    Expanded.Add(id);
            }

            if (id == highlightSequence)
                PanelKit.DrawRowBackground(dl, origin, width, rowTop, fullRowHeight, PanelKit.HighlightColor);

            bool warn = showFeedDiagnostics && stage.EngineCount > 0 && stage.HasFeedWarning;
            string marker = expanded ? "v" : ">";
            y = PanelKit.DrawTableRow(dl, origin, width, y,
                [$"{marker} {id}", $"{stage.DeltaV:F0}", PanelKit.FormatDuration(stage.BurnTime), $"{stage.InitialTwr:F2}"],
                Columns, warn ? PanelKit.WarningColor : PanelKit.ValueColor);

            if (stage.FuelCapacity > 0f)
                DrawFuelBar(dl, origin, width, y, stage.FuelRemaining / stage.FuelCapacity);
            y = rowTop + fullRowHeight;

            if (!expanded)
                continue;

            float2 inner = new float2(origin.X + DetailIndent, origin.Y);
            float innerWidth = width - DetailIndent;
            if (stage.FuelCapacity > 0f)
            {
                float fraction = stage.FuelRemaining / stage.FuelCapacity;
                y = PanelKit.DrawRow(dl, inner, innerWidth, y, "Fuel",
                    $"{PanelKit.FormatMass(stage.FuelRemaining)} / {PanelKit.FormatMass(stage.FuelCapacity)} ({fraction * 100f:F0}%)",
                    fraction < LowFuelFraction);
            }
            y = PanelKit.DrawRow(dl, inner, innerWidth, y, "Cumulative dV", $"{stage.CumulativeDeltaV:F0} m/s");
            y = PanelKit.DrawRow(dl, inner, innerWidth, y, "Isp", $"{stage.Isp:F0} s");
            y = PanelKit.DrawRow(dl, inner, innerWidth, y, "Thrust / engines",
                $"{PanelKit.FormatThrust(stage.Thrust)} / {stage.EngineCount}");
            y = PanelKit.DrawRow(dl, inner, innerWidth, y, "TWR initial / max",
                $"{stage.InitialTwr:F2} / {stage.MaxTwr:F2}");
            y = PanelKit.DrawRow(dl, inner, innerWidth, y, "Wet / dry mass",
                $"{PanelKit.FormatMass(stage.WetMass)} / {PanelKit.FormatMass(stage.DryMass)}");
            y = PanelKit.DrawRow(dl, inner, innerWidth, y, "Parts remaining", stage.PartCount.ToString());
            if (showFeedDiagnostics && stage.EngineCount > 0)
                y = PanelKit.DrawRow(dl, inner, innerWidth, y, "Feed diagnostics", stage.FeedSummary, stage.HasFeedWarning);
            y += 4f;
        }

        y = PanelKit.DrawTotalRow(dl, origin, width, y, "Total dV", $"{burn.TotalDeltaV:F0} m/s");

        float2 hintPos = new float2(origin.X, y);
        dl.AddText(in hintPos, PanelKit.ToColor(PanelKit.LabelColor), "Click a stage for details");
        return y + ImGui.GetTextLineHeight() + 6f;
    }

    private static void DrawFuelBar(ImDrawListPtr dl, float2 origin, float width, float y, float fraction)
    {
        float clamped = Math.Clamp(float.IsNaN(fraction) ? 0f : fraction, 0f, 1f);
        float2 trackMin = new float2(origin.X, y + 1f);
        float2 trackMax = new float2(origin.X + width, y + 1f + FuelBarHeight);
        dl.AddRectFilled(in trackMin, in trackMax, PanelKit.ToColor(PanelKit.GraphBgColor));
        if (clamped > 0.002f)
        {
            float2 fillMax = new float2(origin.X + width * clamped, y + 1f + FuelBarHeight);
            dl.AddRectFilled(in trackMin, in fillMax,
                PanelKit.ToColor(clamped < LowFuelFraction ? PanelKit.WarningColor : PanelKit.GoodColor));
        }
    }
}