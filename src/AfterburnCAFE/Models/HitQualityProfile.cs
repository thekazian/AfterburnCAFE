using System;
using System.Collections.Generic;
using System.Linq;

namespace AfterburnCAFE.Models;

public record QualityBucket(string Name, int Count, double? Rate);

public sealed class HitQualityProfile
{
    private static readonly string[] Names = { "Miss", "Graze", "Glance Off", "Hit", "Penetrate", "Smash", "Wreck", "Unknown" };
    public int Attacks { get; }
    public int Unknown { get; }
    public double? LowRate { get; }
    public double? HighRate { get; }
    public double? MissRate { get; }
    public IReadOnlyList<QualityBucket> Buckets { get; }

    public HitQualityProfile(IEnumerable<GameEvent> events)
    {
        var labels = events.Select(e => Normalize(e.HitQuality)).ToArray();
        Attacks = labels.Length;
        Buckets = Names.Select(name => new QualityBucket(name, labels.Count(l => l == name),
            Attacks > 0 ? (double)labels.Count(l => l == name) / Attacks : null)).ToArray();
        Unknown = labels.Count(l => l == "Unknown");
        LowRate = Rate("Miss", "Graze", "Glance Off");
        HighRate = Rate("Penetrate", "Smash", "Wreck");
        MissRate = Rate("Miss");
    }

    private double? Rate(params string[] names) => Attacks > 0
        ? (double)Buckets.Where(b => names.Contains(b.Name)).Sum(b => b.Count) / Attacks : null;

    public static string Normalize(string label) => label.Trim().ToLowerInvariant() switch
    {
        "miss" or "misses" => "Miss",
        "graze" or "grazes" => "Graze",
        "glance off" or "glances off" => "Glance Off",
        "hit" or "hits" => "Hit",
        "penetrate" or "penetrates" => "Penetrate",
        "smash" or "smashes" => "Smash",
        "wreck" or "wrecks" or "wrecking" => "Wreck",
        _ => "Unknown"
    };

    public static string Percent(double? rate) => rate.HasValue ? rate.Value.ToString("P1") : "N/A (no attacks)";

    public string Report(string title) => $"{title}: {Attacks:N0} recognized attacks\n" +
        $"Low quality (Miss + Graze + Glance Off): {Percent(LowRate)}\n" +
        $"High quality (Penetrate + Smash + Wreck): {Percent(HighRate)}\n" +
        string.Join("\n", Buckets.Select(b => $"{b.Name}: {b.Count:N0} ({Percent(b.Rate)})")) +
        (Attacks is > 0 and < 30 ? "\nSmall sample: fewer than 30 attacks; interpret cautiously." : "") +
        (Unknown > 0 ? "\nUnknown quality stays in the denominator; aggregate rates are incomplete." : "");
}
