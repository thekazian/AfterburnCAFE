using System;
using System.Collections.Generic;
using System.Linq;

namespace AfterburnCAFE.Models;

public record MetricRow(string Metric, string Value, string Note);

public sealed class RunAnalytics
{
    public const double ActivityAllowanceSeconds = 10;
    public double DurationSeconds { get; }
    public double ActiveSeconds { get; }
    public double OutgoingDps { get; }
    public double IncomingDps { get; }
    public double LongestDamageGapSeconds { get; }
    public double DamageDowntimeSeconds { get; }
    public double PeakIncoming1 { get; }
    public double PeakIncoming5 { get; }
    public double PeakIncoming10 { get; }
    public double LargestTargetShare { get; }
    public double ConcentrationIndex { get; }
    public int TargetSwitches { get; }
    public IReadOnlyList<MetricRow> Metrics { get; }
    public string WeaponReport { get; }
    public string TargetReport { get; }
    public string SourceReport { get; }
    private readonly string _qualityReport;
    public string QualityComparison { get; private set; } = "No earlier quality samples for this pilot.";
    public string QualityReport => _qualityReport + "\n\nPersonal comparison\n" + QualityComparison;
    public HitQualityProfile OutgoingQuality { get; }
    public HitQualityProfile IncomingQuality { get; }
    public string EngagementReport { get; }
    public string CoverageReport { get; }

    public RunAnalytics(CombatRun run)
    {
        // Log timestamps have whole-second precision. Include the final event's second.
        DurationSeconds = Math.Max(1, (run.End - run.Start).TotalSeconds + 1);
        var attacks = run.Events.Where(e => e.Direction is "to" or "from").OrderBy(e => e.Timestamp).ToArray();
        var outgoing = attacks.Where(e => e.Direction == "to").ToArray();
        OutgoingQuality = new HitQualityProfile(outgoing);
        IncomingQuality = new HitQualityProfile(attacks.Where(e => e.Direction == "from"));
        var incoming = attacks.Where(e => e.Direction == "from" && e.Damage > 0).ToArray();
        var hits = outgoing.Where(e => e.Damage > 0).ToArray();
        var end = run.Start.AddSeconds(DurationSeconds);
        var activeEnd = run.Start;
        double active = 0;
        foreach (var entry in attacks)
        {
            var intervalStart = entry.Timestamp > activeEnd ? entry.Timestamp : activeEnd;
            var intervalEnd = entry.Timestamp.AddSeconds(ActivityAllowanceSeconds);
            if (intervalEnd > end) intervalEnd = end;
            if (intervalEnd > intervalStart) active += (intervalEnd - intervalStart).TotalSeconds;
            if (intervalEnd > activeEnd) activeEnd = intervalEnd;
        }
        ActiveSeconds = active;
        OutgoingDps = ActiveSeconds > 0 ? run.Dealt / ActiveSeconds : 0;
        IncomingDps = ActiveSeconds > 0 ? run.Taken / ActiveSeconds : 0;

        // Measure seconds with no damage, including leading/trailing silence.
        var previousEnd = run.Start;
        double longest = 0, downtime = 0;
        foreach (var time in hits.Select(e => e.Timestamp).Distinct().OrderBy(t => t))
        {
            var silence = Math.Max(0, (time - previousEnd).TotalSeconds);
            longest = Math.Max(longest, silence);
            downtime += Math.Max(0, silence - ActivityAllowanceSeconds);
            previousEnd = time.AddSeconds(1);
        }
        var tail = Math.Max(0, (end - previousEnd).TotalSeconds);
        LongestDamageGapSeconds = Math.Max(longest, tail);
        DamageDowntimeSeconds = downtime + Math.Max(0, tail - ActivityAllowanceSeconds);
        PeakIncoming1 = Peak(incoming, 1);
        PeakIncoming5 = Peak(incoming, 5);
        PeakIncoming10 = Peak(incoming, 10);

        var targets = hits.GroupBy(e => e.Opponent).OrderByDescending(g => g.Sum(e => e.Damage)).ToArray();
        var shares = targets.Select(g => run.Dealt > 0 ? g.Sum(e => e.Damage) / run.Dealt : 0).ToArray();
        LargestTargetShare = shares.FirstOrDefault();
        ConcentrationIndex = shares.Sum(s => s * s);
        var switches = new List<string>();
        int totalSwitches = 0;
        foreach (var weapon in outgoing.GroupBy(e => WeaponName(e)))
        {
            string? lastTarget = null;
            DateTime? lastTime = null;
            int count = 0, ambiguous = 0;
            foreach (var shot in weapon)
            {
                if (lastTarget is not null && lastTarget != shot.Opponent)
                {
                    if (lastTime == shot.Timestamp) ambiguous++;
                    else count++;
                }
                lastTarget = shot.Opponent;
                lastTime = shot.Timestamp;
            }
            totalSwitches += count;
            switches.Add($"{weapon.Key}: {count} observed name changes ({ambiguous} same-second changes excluded)");
        }
        TargetSwitches = totalSwitches;

        Metrics = new[]
        {
            new MetricRow("Encounter duration", Seconds(DurationSeconds), "First to last combat event, including final second; estimated encounter boundaries."),
            new MetricRow("Active combat time (estimate)", Seconds(ActiveSeconds), "Union of 10-second windows after recognized attacks, including misses; clipped to encounter."),
            new MetricRow("Damage dealt / taken", $"{run.Dealt:N0} / {run.Taken:N0}", "Logged damage only."),
            new MetricRow("Average outgoing / incoming DPS", $"{OutgoingDps:N1} / {IncomingDps:N1}", "Damage divided by estimated active combat time."),
            new MetricRow("Encounter outgoing / incoming DPS", $"{run.Dealt / DurationSeconds:N1} / {run.Taken / DurationSeconds:N1}", "Damage divided by full observed encounter duration."),
            new MetricRow("Peak incoming DPS (1s / 5s / 10s)", $"{PeakIncoming1:N1} / {PeakIncoming5:N1} / {PeakIncoming10:N1}", "Rolling windows; simultaneous hits summed. Full window divisor, even for short runs."),
            new MetricRow("Longest outgoing damage gap", Seconds(LongestDamageGapSeconds), "Longest interval with no successful outgoing damage, including leading/trailing gaps."),
            new MetricRow("Total damage downtime (estimate)", Seconds(DamageDowntimeSeconds), "Sum of no-damage gap portions beyond a 10-second allowance; normal weapon cycles may contribute."),
            new MetricRow("Target switches (estimate)", TargetSwitches.ToString("N0"), "Opponent-name changes per logged weapon; same-second changes excluded. Identical names cannot be distinguished."),
            new MetricRow("Damage concentration", $"Largest name share {LargestTargetShare:P1}; index {ConcentrationIndex:N3}", "Sum of squared damage shares by opponent name (0-1); repeated NPC names are combined."),
            new MetricRow("Outgoing low / high quality", $"{HitQualityProfile.Percent(OutgoingQuality.LowRate)} / {HitQualityProfile.Percent(OutgoingQuality.HighRate)}", $"{OutgoingQuality.Attacks} recognized attacks. Low = miss/graze/glance; high = penetrate/smash/wreck."),
            new MetricRow("Incoming low / high quality", $"{HitQualityProfile.Percent(IncomingQuality.LowRate)} / {HitQualityProfile.Percent(IncomingQuality.HighRate)}", $"{IncomingQuality.Attacks} recognized attacks. Unknown quality retained in denominator.")
        };
        WeaponReport = string.Join("\n\n", outgoing.GroupBy(WeaponName).OrderByDescending(g => g.Sum(e => e.Damage))
            .Select(g => $"{g.Key}\n{g.Sum(e => e.Damage):N0} damage | {Share(g.Sum(e => e.Damage), run.Dealt)} of outgoing | {g.Count()} attacks"));
        // Only classify a drone name actually observed in the user's logs. Other names remain unclassified.
        var observedDroneDamage = hits.Where(e => e.Weapon == "Warrior II").Sum(e => e.Damage);
        WeaponReport += $"\n\nRecognized drones (Warrior II): {observedDroneDamage:N0} damage ({Share(observedDroneDamage, run.Dealt)}). Other weapon names are not classified yet; this is a lower bound on drone contribution. Ammo is shown only when included in the logged weapon name.";
        TargetReport = string.Join("\n\n", targets.Select(g => $"{g.Key}\n{g.Sum(e => e.Damage):N0} damage | {Share(g.Sum(e => e.Damage), run.Dealt)} of outgoing"));
        SourceReport = string.Join("\n\n", incoming.GroupBy(e => e.Opponent).OrderByDescending(g => g.Sum(e => e.Damage))
            .Select(g => $"{g.Key}\n{g.Sum(e => e.Damage):N0} damage | {Share(g.Sum(e => e.Damage), run.Taken)} of incoming | peak 5s {Peak(g.ToArray(), 5):N1} DPS"));
        _qualityReport = OutgoingQuality.Report("Outgoing") + "\n\n" + IncomingQuality.Report("Incoming") +
            "\n\nOutgoing by weapon\n" + string.Join("\n\n", outgoing.GroupBy(WeaponName)
                .Select(g => new HitQualityProfile(g).Report(g.Key))) +
            "\n\nHit-quality rates describe logged attack outcomes, not a universal application score. Weapon mechanics and opponent mix may differ. Range, transversal, ammo and defensive piloting are possible investigation topics; these logs do not establish the cause.";
        EngagementReport = string.Join("\n\n", outgoing.Where(e => e.Opponent.Length > 0).GroupBy(e => e.Opponent)
            .OrderBy(g => g.Min(e => e.Timestamp)).Select(g =>
                $"{g.Key}\nFirst {g.Min(e => e.Timestamp):HH:mm:ss} / last {g.Max(e => e.Timestamp):HH:mm:ss} UTC | observed span {(g.Max(e => e.Timestamp) - g.Min(e => e.Timestamp)).TotalSeconds:N0}s | {g.Count()} attacks"));
        EngagementReport += "\n\nThese are engagement spans by logged name, not confirmed time-to-kill. Multiple NPCs with the same name are merged; no unique NPC IDs or confirmed kill events have been extracted.\n\n" + string.Join("\n", switches);
        CoverageReport = $"Recognized attacks: {attacks.Length} / {run.Events.Count(e => e.Category == "combat")} combat lines. Other combat messages remain in the timeline.\n\nDamage types: unavailable. The parsed damage lines do not expose EM / thermal / kinetic / explosive amounts. Weapon or ammo names alone are insufficient to reconstruct the actual damage split.\n\nTime-to-kill: unavailable without reliable NPC identity and kill evidence.\n\nSite, filament tier, ship and fit: unidentified. Run boundaries and active time are estimates.\n\nLogs use one-second timestamps. They do not establish turret/drone cycle timing, true time on target, or why application stopped.";
    }

    private static string WeaponName(GameEvent e) => e.Weapon.Length > 0 ? e.Weapon : "Unspecified weapon";
    private static string Seconds(double seconds) => $"{seconds:N1}s ({seconds / 60:N1} min)";
    private static string Share(double amount, double total) => total > 0 ? (amount / total).ToString("P1") : "0.0%";

    public static double Peak(IReadOnlyList<GameEvent> entries, int windowSeconds)
    {
        if (windowSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(windowSeconds));
        var sorted = entries.OrderBy(e => e.Timestamp).ToArray();
        int left = 0;
        double sum = 0, peak = 0;
        for (int right = 0; right < sorted.Length; right++)
        {
            sum += sorted[right].Damage;
            while ((sorted[right].Timestamp - sorted[left].Timestamp).TotalSeconds >= windowSeconds)
                sum -= sorted[left++].Damage;
            peak = Math.Max(peak, sum / windowSeconds);
        }
        return peak;
    }

    public static void ApplyBaselines(IReadOnlyList<CombatRun> runs)
    {
        foreach (var run in runs)
        {
            run.Baseline = "No earlier runs for this pilot.";
            run.Analytics.QualityComparison = "No earlier quality samples for this pilot.";
            var earlier = runs.Where(r => r.Pilot == run.Pilot && r.Start < run.Start && r.Analytics.ActiveSeconds > 0).ToArray();
            if (earlier.Length == 0) continue;
            static double Median(IEnumerable<double> values)
            {
                var data = values.OrderBy(v => v).ToArray();
                return (data[(data.Length - 1) / 2] + data[data.Length / 2]) / 2;
            }
            static string Compare(string label, double current, double baseline) =>
                $"{label}: current {current:N1} | prior median {baseline:N1}" +
                (baseline > 0 ? $" | {(current / baseline - 1) * 100:+0.0;-0.0;0.0}%" : " | percentage unavailable");
            run.Baseline = $"{earlier.Length} earlier estimated runs for {run.Pilot}. Exploratory comparison: site, tier, ship and fit are not matched.\n\n" +
                Compare("Duration (s)", run.Analytics.DurationSeconds, Median(earlier.Select(r => r.Analytics.DurationSeconds))) + "\n" +
                Compare("Outgoing active DPS", run.Analytics.OutgoingDps, Median(earlier.Select(r => r.Analytics.OutgoingDps))) + "\n" +
                Compare("Incoming active DPS", run.Analytics.IncomingDps, Median(earlier.Select(r => r.Analytics.IncomingDps))) + "\n" +
                Compare("Peak incoming 5s DPS", run.Analytics.PeakIncoming5, Median(earlier.Select(r => r.Analytics.PeakIncoming5))) + "\n" +
                Compare("Damage downtime (s)", run.Analytics.DamageDowntimeSeconds, Median(earlier.Select(r => r.Analytics.DamageDowntimeSeconds)));
            run.Analytics.QualityComparison = QualityBaseline(run, earlier);
            run.Baseline += "\n\nHit quality\n" + run.Analytics.QualityComparison;
        }
    }

    private static string QualityBaseline(CombatRun run, IReadOnlyList<CombatRun> earlier)
    {
        var sections = new List<string>();
        foreach (var outgoing in new[] { true, false })
        {
            var current = outgoing ? run.Analytics.OutgoingQuality : run.Analytics.IncomingQuality;
            var profiles = earlier.Select(r => outgoing ? r.Analytics.OutgoingQuality : r.Analytics.IncomingQuality)
                .Where(p => p.Attacks > 0).ToArray();
            var label = outgoing ? "Outgoing" : "Incoming";
            if (current.Attacks == 0 || profiles.Length == 0)
            {
                sections.Add($"{label}: comparison unavailable (no current or prior attacks).");
                continue;
            }
            var low = profiles.Average(p => p.LowRate!.Value);
            var high = profiles.Average(p => p.HighRate!.Value);
            var miss = profiles.Average(p => p.MissRate!.Value);
            static string Delta(string label, double current, double average) =>
                $"{label}: {current:P1} | personal average {average:P1} | {(current - average) * 100:+0.0;-0.0;0.0} percentage points";
            var text = $"{label}: current {current.Attacks} attacks; {profiles.Length} prior runs / {profiles.Sum(p => p.Attacks)} prior attacks. Average gives each run equal weight.\n" +
                Delta("Low quality", current.LowRate!.Value, low) + "\n" +
                Delta("High quality", current.HighRate!.Value, high) + "\n" +
                Delta("Miss", current.MissRate!.Value, miss);
            var enough = current.Attacks >= 30 && profiles.Length >= 3 && profiles.All(p => p.Attacks >= 30) &&
                current.Unknown == 0 && profiles.All(p => p.Unknown == 0);
            if (!enough) text += "\nLimited evidence: diagnostic flags require 30 attacks per run, three prior runs, and no unknown qualities. Rates remain descriptive.";
            else if (outgoing && current.LowRate.Value - low >= 0.05 && high - current.HighRate.Value >= 0.02)
            {
                text += "\nObservation: more low-quality and fewer high-quality outgoing outcomes than your earlier average.";
                var damageAverage = earlier.Average(r => r.Dealt);
                var durationAverage = earlier.Average(r => r.Analytics.DurationSeconds);
                if (damageAverage > 0 && Math.Abs(run.Dealt / damageAverage - 1) <= 0.1 &&
                    run.Analytics.DurationSeconds > durationAverage * 1.1)
                    text += " Damage dealt is within 10% of your prior average, while duration is over 10% longer. This pattern warrants investigating application; it does not establish the cause.";
            }
            else if (!outgoing && miss - current.MissRate.Value >= 0.05 &&
                run.Taken > earlier.Average(r => r.Taken) * 1.2)
                text += "\nObservation: fewer incoming misses and over 20% more total incoming damage than your earlier average. Investigate exposure duration and opponent mix as well as piloting; this does not prove you were easier to hit.";
            if (outgoing && current.LowRate.Value < low && current.HighRate.Value > high)
                text += "\nDirection: lower low-quality and higher high-quality rates; favorable observed outcomes, subject to weapon and target mix.";
            sections.Add(text);
        }
        return "Exploratory: same pilot, earlier runs only; site, tier, ship, fit and weapon mix are not matched. Thresholds are product heuristics, not statistical significance.\n\n" + string.Join("\n\n", sections);
    }
}
