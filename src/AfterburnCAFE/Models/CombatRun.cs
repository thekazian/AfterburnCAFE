using System;
using System.Collections.Generic;
using System.Linq;

namespace AfterburnCAFE.Models;

public record GameEvent(DateTime Timestamp, string Category, string Message, double Damage = 0,
    string Direction = "", string Opponent = "", string Weapon = "", string HitQuality = "")
{
    public string Display => $"{Timestamp:HH:mm:ss} [{Category}] {Message}";
}

public record CombatRun(string Pilot, string Source, IReadOnlyList<GameEvent> Events)
{
    private RunAnalytics? _analytics;
    public RunAnalytics Analytics => _analytics ??= new RunAnalytics(this);
    public string Baseline { get; set; } = "No earlier runs for this pilot.";
    public DateTime Start => Events[0].Timestamp;
    public DateTime End => Events[^1].Timestamp;
    public string? ContextLabel { get; set; }
    public string MarkerDetails { get; set; } = "";
    public string Title => $"{ContextLabel ?? "Combat run"} - {Start:MMM dd, HH:mm}";
    public string Subtitle => $"{Pilot} | {(End - Start).TotalMinutes:N1} min";
    public string TimeRange => $"{Start:yyyy-MM-dd HH:mm:ss} - {End:HH:mm:ss} UTC";
    public string BoundaryNote => ContextLabel is null
        ? "Estimated boundaries: system jump or 5-minute combat gap. Site / filament unidentified for the full interval."
        : "Manually labeled activity. Combat boundaries remain estimated: system jump or 5-minute combat gap.";
    public double Dealt => Events.Where(e => e.Direction == "to").Sum(e => e.Damage);
    public double Taken => Events.Where(e => e.Direction == "from").Sum(e => e.Damage);
    public string DamageSummary => $"Damage dealt: {Dealt:N0}     Damage taken: {Taken:N0}";
    public string ActivitySummary => $"{Events.Count(e => e.Category == "combat"):N0} combat events | {(End - Start).TotalMinutes:N1} minutes from first to last combat event";
    public string Opponents => string.Join("\n", Events.Where(e => e.Opponent.Length > 0)
        .GroupBy(e => e.Opponent).OrderByDescending(g => g.Sum(e => e.Damage))
        .Select(g => $"{g.Key}: dealt {g.Where(e => e.Direction == "to").Sum(e => e.Damage):N0} / taken {g.Where(e => e.Direction == "from").Sum(e => e.Damage):N0}"));
    public string Weapons => string.Join("\n", Events.Where(e => e.Weapon.Length > 0 && e.Direction == "to")
        .GroupBy(e => e.Weapon).OrderByDescending(g => g.Sum(e => e.Damage))
        .Select(g => $"{g.Key}: {g.Sum(e => e.Damage):N0} damage"));
}
