using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AfterburnCAFE.Models;

namespace AfterburnCAFE.Services;

public sealed class RunMarkerStore
{
    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AfterburnCAFE", "markers");
    private readonly string _directory;
    public RunMarkerStore(string? directory = null) => _directory = directory ?? DefaultDirectory;

    public IReadOnlyList<RunMarker> Load()
    {
        if (!Directory.Exists(_directory)) return Array.Empty<RunMarker>();
        return Directory.EnumerateFiles(_directory, "*.json").Select(path =>
        {
            var marker = JsonSerializer.Deserialize<RunMarker>(File.ReadAllText(path))
                ?? throw new InvalidDataException($"Invalid marker: {path}");
            if (string.IsNullOrWhiteSpace(marker.Pilot) || string.IsNullOrWhiteSpace(marker.Label) ||
                marker.StartedUtc.Kind != DateTimeKind.Utc ||
                (marker.EndedUtc.HasValue && (marker.EndedUtc.Value.Kind != DateTimeKind.Utc || marker.EndedUtc < marker.StartedUtc)))
                throw new InvalidDataException($"Invalid marker fields: {path}");
            return marker;
        }).OrderByDescending(m => m.StartedUtc).ToArray();
    }

    public RunMarker Start(string pilot, string activity, string label, string variant, string notes, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(pilot) || string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Choose a pilot and an activity name first.");
        if (Load().Any(m => m.Pilot.Equals(pilot.Trim(), StringComparison.OrdinalIgnoreCase) && m.EndedUtc is null))
            throw new InvalidOperationException("End this pilot's active run before starting another.");
        var marker = new RunMarker(Guid.NewGuid(), pilot.Trim(), activity, label.Trim(), variant.Trim(), notes.Trim(), UtcSecond(now));
        Save(marker);
        return marker;
    }

    public RunMarker End(Guid id, DateTime now)
    {
        var marker = Load().Single(m => m.Id == id);
        if (marker.EndedUtc.HasValue) throw new InvalidOperationException("This run has already ended.");
        var end = UtcSecond(now);
        if (end < marker.StartedUtc) throw new InvalidOperationException("The clock is earlier than the start time. Check your system clock.");
        var updated = marker with { EndedUtc = end };
        Save(updated);
        return updated;
    }

    private static DateTime UtcSecond(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("Marker timestamps must be UTC.");
        return new DateTime(value.Ticks - value.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
    }

    public static void Annotate(IReadOnlyList<CombatRun> runs, IReadOnlyList<RunMarker> markers, DateTime now)
    {
        foreach (var run in runs)
        {
            run.ContextLabel = null;
            run.MarkerDetails = "";
            var matches = markers.Where(m => m.Pilot.Equals(run.Pilot, StringComparison.OrdinalIgnoreCase) &&
                m.StartedUtc <= run.End && (m.EndedUtc ?? now) > run.Start).ToArray();
            if (matches.Length == 1 && matches[0].StartedUtc <= run.Start && (matches[0].EndedUtc ?? now) > run.End)
                run.ContextLabel = matches[0].Label;
            if (matches.Length > 0)
                run.MarkerDetails = "Manual annotations (combat boundaries remain estimated):\n" +
                    string.Join("\n", matches.Select(m => $"{m.Label} | {m.Variant} | entry {m.StartedUtc:HH:mm:ss} UTC | " +
                        (m.EndedUtc.HasValue ? $"exit {m.EndedUtc:HH:mm:ss} UTC" : "exit not marked") +
                        (m.Notes.Length > 0 ? $" | {m.Notes}" : "")));
        }
    }

    private void Save(RunMarker marker)
    {
        Directory.CreateDirectory(_directory);
        var destination = Path.Combine(_directory, $"{marker.Id:D}.json");
        var temporary = Path.Combine(_directory, $"{marker.Id:D}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(marker, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, destination, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
