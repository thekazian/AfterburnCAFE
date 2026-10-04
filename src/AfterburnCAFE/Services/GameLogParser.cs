using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using AfterburnCAFE.Models;

namespace AfterburnCAFE.Services;

public static class GameLogParser
{
    private static readonly Regex Line = new(@"^\[\s*(?<time>\d{4}\.\d{2}\.\d{2} \d{2}:\d{2}:\d{2})\s*\]\s*\((?<category>[^)]+)\)\s*(?<message>.*)$");
    private static readonly Regex Tags = new(@"<[^>]*>");
    private static readonly Regex Damage = new(@"^(?<amount>[\d,.]+)\s+(?<direction>to|from)\s+(?<rest>.+)$");

    public static IReadOnlyList<CombatRun> Parse(string source, IEnumerable<string> lines)
    {
        var pilot = "Unknown pilot";
        var events = new List<GameEvent>();
        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("Listener:", StringComparison.Ordinal))
                pilot = line.Trim().Substring("Listener:".Length).Trim();
            var match = Line.Match(line);
            if (!match.Success || !DateTime.TryParseExact(match.Groups["time"].Value,
                    "yyyy.MM.dd HH:mm:ss", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var time)) continue;
            var category = match.Groups["category"].Value;
            var message = WebUtility.HtmlDecode(Tags.Replace(match.Groups["message"].Value.Replace("<br>", " "), ""));
            var damage = category == "combat" ? Damage.Match(message) : Match.Empty;
            double amount = 0;
            var direction = "";
            var opponent = "";
            var weapon = "";
            var quality = "";
            if (damage.Success && double.TryParse(damage.Groups["amount"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
            {
                direction = damage.Groups["direction"].Value;
                var parts = damage.Groups["rest"].Value.Split(" - ", StringSplitOptions.None);
                opponent = parts[0];
                if (parts.Length >= 3) weapon = string.Join(" - ", parts.Skip(1).Take(parts.Length - 2));
                if (parts.Length >= 2) quality = parts[^1];
            }
            else if (category == "combat")
            {
                var outgoingMiss = Regex.Match(message, @"^Your .+? misses (?<target>.+?) completely(?: - (?<weapon>.+))?$");
                var incomingMiss = Regex.Match(message, @"^(?<source>.+?) misses you completely$");
                if (outgoingMiss.Success)
                {
                    direction = "to"; opponent = outgoingMiss.Groups["target"].Value;
                    weapon = outgoingMiss.Groups["weapon"].Value; quality = "Miss";
                }
                else if (incomingMiss.Success)
                {
                    direction = "from"; opponent = incomingMiss.Groups["source"].Value; quality = "Miss";
                }
            }
            events.Add(new GameEvent(time, category, message, amount, direction, opponent, weapon, quality));
        }
        var runs = new List<CombatRun>();
        var current = new List<GameEvent>();
        DateTime? lastCombat = null;
        void Finish()
        {
            if (current.Count > 0)
            {
                var last = current.FindLastIndex(e => e.Category == "combat");
                runs.Add(new CombatRun(pilot, source, current.Take(last + 1).ToArray()));
            }
            current.Clear();
            lastCombat = null;
        }
        foreach (var entry in events.OrderBy(e => e.Timestamp))
        {
            if ((lastCombat.HasValue && entry.Timestamp - lastCombat.Value > TimeSpan.FromMinutes(5)) ||
                (entry.Category == "None" && entry.Message.StartsWith("Jumping from ", StringComparison.Ordinal))) Finish();
            if (entry.Category == "combat") lastCombat = entry.Timestamp;
            if (lastCombat.HasValue) current.Add(entry);
        }
        Finish();
        return runs;
    }

    public static IReadOnlyList<CombatRun> Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        IEnumerable<string> ReadLines()
        {
            while (reader.ReadLine() is { } line) yield return line;
        }
        return Parse(path, ReadLines());
    }
}
