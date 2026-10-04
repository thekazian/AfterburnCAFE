using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AfterburnCAFE.Models;

namespace AfterburnCAFE.Services;

public static class GameLogDiscovery
{
    public static (IReadOnlyList<CombatRun> Runs, string Status) Scan()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var documents = new List<string>
        {
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Path.Combine(profile, "Documents"),
            Path.Combine(profile, "OneDrive", "Documents")
        };
        foreach (var variable in new[] { "OneDrive", "OneDriveConsumer", "OneDriveCommercial" })
        {
            var root = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrWhiteSpace(root))
                documents.Add(Path.Combine(root, "Documents"));
        }
        var results = new List<string>();
        var runs = new List<CombatRun>();
        var files = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var folder in documents.Where(d => !string.IsNullOrWhiteSpace(d))
                     .Select(d => Path.Combine(d, "EVE", "logs", "Gamelogs"))
                     .Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal))
        {
            try
            {
                foreach (var path in Directory.EnumerateFiles(folder, "*.txt")) files.Add(path);
            }
            catch (DirectoryNotFoundException) { }
            catch (UnauthorizedAccessException) { results.Add($"{folder}\nAccess denied"); }
            catch (IOException ex) { results.Add($"{folder}\nCould not read folder: {ex.Message}"); }
        }
        foreach (var path in files)
        {
            try { runs.AddRange(GameLogParser.Read(path)); }
            catch (UnauthorizedAccessException) { results.Add($"Access denied: {path}"); }
            catch (IOException ex) { results.Add($"{path}: {ex.Message}"); }
        }
        var status = files.Count == 0 ? "No EVE logs found in local or OneDrive Documents."
            : $"{files.Count} logs scanned | {runs.Count} estimated combat runs | UTC timestamps";
        if (results.Count > 0) status += "\n" + string.Join("\n", results);
        RunAnalytics.ApplyBaselines(runs);
        try { RunMarkerStore.Annotate(runs, new RunMarkerStore().Load(), DateTime.UtcNow); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { status += $"\nCould not load manual markers: {ex.Message}"; }
        return (runs.OrderByDescending(r => r.Start).ToArray(), status);
    }
}
