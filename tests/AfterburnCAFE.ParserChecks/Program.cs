using AfterburnCAFE.Services;
using AfterburnCAFE.Models;

void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

var runs = GameLogParser.Parse("fixture", new[]
{
    "Listener: Test Pilot",
    "invalid line",
    "[ 2026.10.03 12:00:00 ] (combat) <b>1,589</b> to Enemy - Cannon - Hits",
    "[ 2026.10.03 12:00:01 ] (combat) 14 from Enemy - Hits",
    "[ 2026.10.03 12:00:02 ] (combat) Enemy misses you completely",
    "[ 2026.10.03 12:00:03 ] (notify) Ship stopping",
    "[ 2026.10.03 12:05:03 ] (combat) 30 to Other - Missile - Hits",
    "[ 2026.10.03 12:05:04 ] (None) Jumping from A to B",
    "[ 2026.10.03 12:05:05 ] (combat) 20 from Other - Hits"
});
Check(runs.Count == 3, "Gap and jump should split runs");
Check(runs[0].Dealt == 1589 && runs[0].Taken == 14, "Damage direction / markup parsing");
Check(runs[0].Pilot == "Test Pilot", "Pilot header");
Check(runs[0].Events.Count == 3, "Miss retained, trailing notify excluded");
Check(runs[0].Events[0].Weapon == "Cannon", "Weapon parsing");
Check(runs[0].Start.Kind == DateTimeKind.Utc, "UTC timestamps");
Check(runs[0].Events[2].HitQuality == "Miss" && runs[0].Events[2].Direction == "from", "Incoming miss classification");
var miss = GameLogParser.Parse("miss", new[]
{
    "[ 2026.10.03 12:00:00 ] (combat) Your group of Cannon misses Enemy completely - Cannon"
})[0].Events[0];
Check(miss.Direction == "to" && miss.Opponent == "Enemy" && miss.Weapon == "Cannon" && miss.Damage == 0,
    "Outgoing miss contributes to attacks, not damage");
var start = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);
var markerDirectory = Path.Combine(Path.GetTempPath(), "AfterburnCAFE-marker-checks-" + Guid.NewGuid().ToString("N"));
try
{
    var store = new RunMarkerStore(markerDirectory);
    Check(store.Load().Count == 0, "Missing marker directory is empty");
    var marker = store.Start("Test", "Combat site", "Angel Sanctum", "Ring", "test note", start.AddMilliseconds(450));
    Check(store.Load()[0].StartedUtc == start, "Marker timestamps persisted at whole-second UTC precision");
    var reopened = new RunMarkerStore(markerDirectory);
    Check(reopened.Load()[0].EndedUtc is null, "Active run survives restart");
    bool duplicateRejected = false;
    try { store.Start("test", "Combat site", "Angel Haven", "", "", start); }
    catch (InvalidOperationException) { duplicateRejected = true; }
    Check(duplicateRejected, "Same pilot cannot start overlapping active markers");
    var ended = reopened.End(marker.Id, start.AddMinutes(2));
    Check(ended.EndedUtc == start.AddMinutes(2) && store.Load()[0].Notes == "test note", "Exit and metadata roundtrip");
    var annotated = new CombatRun("Test", "test", new[]
    {
        new GameEvent(start.AddSeconds(1), "combat", "test"),
        new GameEvent(start.AddSeconds(10), "combat", "test")
    });
    var other = new CombatRun("Other", "test", annotated.Events);
    RunMarkerStore.Annotate(new[] { annotated, other }, store.Load(), start.AddHours(1));
    Check(annotated.ContextLabel == "Angel Sanctum" && other.ContextLabel is null, "Annotations match pilot and interval");
    var spanning = new CombatRun("Test", "test", new[]
    {
        new GameEvent(start.AddSeconds(-1), "combat", "test"),
        new GameEvent(start.AddSeconds(10), "combat", "test")
    });
    RunMarkerStore.Annotate(new[] { spanning }, store.Load(), start.AddHours(1));
    Check(spanning.ContextLabel is null && spanning.MarkerDetails.Contains("Angel Sanctum"), "Partial overlap does not relabel whole run");
}
finally
{
    if (Directory.Exists(markerDirectory)) Directory.Delete(markerDirectory, recursive: true);
}
GameEvent Event(int seconds, double damage, string direction, string target = "A") =>
    new(start.AddSeconds(seconds), "combat", "fixture", damage, direction, target, "Cannon", "Hits");
var measured = new CombatRun("Test", "metrics", new[]
{
    Event(0, 100, "to"), Event(2, 90, "from"), Event(2, 60, "from"),
    Event(7, 150, "from"), Event(20, 200, "to", "B"), Event(60, 100, "to")
});
var analytics = measured.Analytics;
var quality = new HitQualityProfile(new[]
{
    Event(0, 1, "to") with { HitQuality = "Grazes" },
    Event(1, 1, "to") with { HitQuality = "Glances Off" },
    Event(2, 0, "to") with { HitQuality = "Miss" },
    Event(3, 1, "to") with { HitQuality = "Penetrates" },
    Event(4, 1, "to") with { HitQuality = "Smashes" },
    Event(5, 1, "to") with { HitQuality = "Mystery" }
});
Check(quality.Attacks == 6 && quality.LowRate == 0.5 && quality.Unknown == 1 &&
    Math.Abs(quality.HighRate!.Value - 2.0 / 6) < 0.001, "Aggregates include misses and preserve unknown denominator");
Check(quality.Buckets.Count == 8 && quality.Buckets.Single(b => b.Name == "Wreck").Count == 0 &&
    quality.Report("Outgoing").Contains("Wreck: 0"), "Stable zero-count buckets");
var emptyQuality = new HitQualityProfile(Array.Empty<GameEvent>());
Check(emptyQuality.LowRate is null && emptyQuality.Buckets.All(b => b.Rate is null), "Zero attacks are unavailable, not zero percent");
Check(HitQualityProfile.Normalize("Wrecks") == "Wreck" && HitQualityProfile.Normalize("Hits") == "Hit", "Quality normalization");
Check(analytics.DurationSeconds == 61 && analytics.ActiveSeconds == 28, "Inclusive duration and interval union");
Check(Math.Abs(analytics.OutgoingDps - 400.0 / 28) < 0.001, "Active outgoing DPS denominator");
Check(analytics.PeakIncoming1 == 150 && analytics.PeakIncoming5 == 30 && analytics.PeakIncoming10 == 30,
    "Peak windows sum simultaneous hits and exclude exact window boundary");
Check(analytics.LongestDamageGapSeconds == 39 && analytics.DamageDowntimeSeconds == 38,
    "Outgoing silence and thresholded downtime");
Check(analytics.TargetSwitches == 2 && analytics.LargestTargetShare == 0.5 && analytics.ConcentrationIndex == 0.5,
    "Per-weapon switches and concentration");
var single = new CombatRun("Test", "single", new[] { Event(0, 100, "from") });
Check(single.Analytics.DurationSeconds == 1 && single.Analytics.IncomingDps == 100,
    "Single timestamp avoids division by zero");
var noDamage = new CombatRun("Test", "misses", new[] { miss });
Check(noDamage.Dealt == 0 && noDamage.Analytics.OutgoingDps == 0, "Miss-only encounter");
var later = new CombatRun("Test", "later", measured.Events.Select(e => e with { Timestamp = e.Timestamp.AddHours(1) }).ToArray());
var otherPilot = new CombatRun("Other", "other", measured.Events);
RunAnalytics.ApplyBaselines(new[] { measured, later, otherPilot });
Check(later.Baseline.StartsWith("1 earlier") && measured.Baseline.StartsWith("No earlier") && otherPilot.Baseline.StartsWith("No earlier"),
    "Baseline excludes current/future runs and other pilots");
Check(later.Analytics.QualityComparison.Contains("1 prior runs / 3 prior attacks") &&
    later.Analytics.QualityComparison.Contains("Limited evidence"), "Quality baseline counts and sample caution");
CombatRun QualityRun(int day, bool lowQuality, string pilot = "Quality Pilot") => new(pilot, "quality", 
    Enumerable.Range(0, 60).Select(i => Event(i * (lowQuality ? 2 : 1), 1, "to") with
    {
        Timestamp = start.AddDays(day).AddSeconds(i * (lowQuality ? 2 : 1)),
        HitQuality = lowQuality ? (i < 30 ? "Grazes" : "Hits") : (i < 12 ? "Smashes" : "Hits")
    }).ToArray());
var priorQualityRuns = new[] { QualityRun(0, false), QualityRun(1, false), QualityRun(2, false) };
var weakerQualityRun = QualityRun(3, true);
RunAnalytics.ApplyBaselines(priorQualityRuns.Append(weakerQualityRun).ToArray());
Check(weakerQualityRun.Analytics.QualityComparison.Contains("Observation: more low-quality") &&
    weakerQualityRun.Analytics.QualityComparison.Contains("does not establish the cause"),
    "Diagnostic uses outcome observation and preserves causal uncertainty");
Check(weakerQualityRun.Analytics.QualityComparison.Contains("3 prior runs / 180 prior attacks"),
    "Comparison preserves sample sizes");
Check(GameLogParser.Parse("empty", new[] { "[ 2026.10.03 12:00:00 ] (notify) Ship stopping" }).Count == 0,
    "Noncombat files should not create runs");
var scan = GameLogDiscovery.Scan();
Check(scan.Runs.All(r => r.Events.Count > 0 && r.End >= r.Start), "Real log run bounds");
Check(scan.Runs.All(r => r.Events.All(e => !e.Message.Contains("<color="))), "Real markup stripped");
Check(scan.Runs.All(r => r.Analytics.ActiveSeconds >= 0 && r.Analytics.ActiveSeconds <= r.Analytics.DurationSeconds &&
    double.IsFinite(r.Analytics.OutgoingDps) && r.Analytics.DamageDowntimeSeconds <= r.Analytics.DurationSeconds),
    "Real log analytics are finite and bounded");
Console.WriteLine("Parser checks passed.");
Console.WriteLine(scan.Status);
if (scan.Runs.Count > 0)
{
    Console.WriteLine(scan.Runs[0].Title);
    Console.WriteLine(scan.Runs[0].DamageSummary);
    foreach (var metric in scan.Runs[0].Analytics.Metrics) Console.WriteLine($"{metric.Metric}: {metric.Value}");
    Console.WriteLine(scan.Runs[0].Analytics.OutgoingQuality.Report("Outgoing"));
}
