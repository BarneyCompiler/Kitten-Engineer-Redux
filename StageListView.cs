using Brutal.ImGuiApi;
using Brutal.Numerics;
using KittenEngineerRedux.Analysis;

namespace KittenEngineerRedux.UI;

internal static class StageListView
{
    private static readonly HashSet<int> Expanded = new();
    private static readonly float[] Columns = { 0.16f, 0.30f, 0.27f, 0.27f };
    private const float DetailIndent = 12f;

    public static float Draw(ImDrawListPtr dl, float2 origin, float width, float y,
        VehicleBurnAnalysis burn, bool showFeedDiagnostics, int highlightSequence = -1)
    {
        if (burn.Sequences.Count == 0)
            return PanelKit.DrawRow(dl, origin, width, y, "Stages", "None");

        float rowHeight = PanelKit.RowHeight;

        y = PanelKit.DrawTableRow(dl, origin, width, y, ["STG", "dV (m/s)", "BURN", "TWR"], Columns, PanelKit.HeaderColor);
        float2 ruleStart = new float2(origin.X, y - 2f);
        float2 ruleEnd = new float2(origin.X + width, y - 2f);
        dl.AddLine(in ruleStart, in ruleEnd, PanelKit.ToColor(PanelKit.BorderColor));

        foreach (SequenceBurnInfo stage in burn.Sequences)
        {
            int id = stage.SequenceNumber;
            bool expanded = Expanded.Contains(id);

            ImGui.SetCursorScreenPos(new float2(origin.X, y));
            if (ImGui.InvisibleButton($"##KerStageRow{id}", new float2(width, rowHeight)))
            {
                if (!Expanded.Remove(id))
                    Expanded.Add(id);
            }

            if (id == highlightSequence)
                PanelKit.DrawRowBackground(dl, origin, width, y, rowHeight, PanelKit.HighlightColor);

            bool warn = showFeedDiagnostics && stage.EngineCount > 0 && stage.HasFeedWarning;
            string marker = expanded ? "v" : ">";
            y = PanelKit.DrawTableRow(dl, origin, width, y,
                [$"{marker} {id}", $"{stage.DeltaV:F0}", PanelKit.FormatDuration(stage.BurnTime), $"{stage.InitialTwr:F2}"],
                Columns, warn ? PanelKit.WarningColor : PanelKit.ValueColor);

            if (!expanded)
                continue;

            float2 inner = new float2(origin.X + DetailIndent, origin.Y);
            float innerWidth = width - DetailIndent;
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
}