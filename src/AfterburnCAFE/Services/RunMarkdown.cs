using System.Linq;
using System.Text;
using AfterburnCAFE.Models;

namespace AfterburnCAFE.Services;

public static class RunMarkdown
{
    private static string Escape(string text) => text.Replace("\\", "\\\\").Replace("|", "\\|")
        .Replace("*", "\\*").Replace("_", "\\_").Replace("[", "\\[").Replace("]", "\\]")
        .Replace("<", "&lt;").Replace(">", "&gt;").Replace("`", "\\`")
        .Replace("#", "\\#").Replace("\r", "").Replace("\n", "<br>");

    public static string Export(CombatRun run, string tab)
    {
        var output = new StringBuilder();
        output.AppendLine($"# {Escape(run.Title)}").AppendLine();
        output.AppendLine($"**Pilot:** {Escape(run.Pilot)}  ");
        output.AppendLine($"**Time:** {Escape(run.TimeRange)}  ");
        output.AppendLine($"**Source:** {Escape(run.Source)}").AppendLine();
        output.AppendLine(Escape(run.BoundaryNote)).AppendLine();
        if (run.MarkerDetails.Length > 0) output.AppendLine(Escape(run.MarkerDetails)).AppendLine();
        output.AppendLine(Escape(run.DamageSummary)).AppendLine();
        output.AppendLine(Escape(run.ActivitySummary)).AppendLine();
        output.AppendLine($"## {Escape(tab)}").AppendLine();
        if (tab == "Metrics")
        {
            output.AppendLine("| Metric | Value | Note |").AppendLine("| --- | --- | --- |");
            foreach (var row in run.Analytics.Metrics)
                output.AppendLine($"| {Escape(row.Metric)} | {Escape(row.Value)} | {Escape(row.Note)} |");
        }
        else if (tab == "Timeline")
        {
            output.AppendLine("| Time (UTC) | Category | Message |").AppendLine("| --- | --- | --- |");
            foreach (var entry in run.Events)
                output.AppendLine($"| {entry.Timestamp:yyyy-MM-dd HH:mm:ss} | {Escape(entry.Category)} | {Escape(entry.Message)} |");
        }
        else
        {
            var text = tab switch
            {
                "Targets" => run.Analytics.TargetReport,
                "Incoming sources" => run.Analytics.SourceReport,
                "Weapons" => run.Analytics.WeaponReport,
                "Hit quality" => run.Analytics.QualityReport,
                "Engagements" => run.Analytics.EngagementReport,
                "Personal baseline" => run.Baseline,
                "Data coverage" => run.Analytics.CoverageReport,
                _ => "No content available."
            };
            output.AppendLine(string.Join("\n", text.Split('\n').Select(line => Escape(line) + (line.Length > 0 ? "  " : ""))));
        }
        return output.ToString();
    }
}
