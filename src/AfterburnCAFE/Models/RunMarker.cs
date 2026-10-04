using System;

namespace AfterburnCAFE.Models;

public record RunMarker(Guid Id, string Pilot, string Activity, string Label, string Variant,
    string Notes, DateTime StartedUtc, DateTime? EndedUtc = null)
{
    public string Display => $"{Label}\n{Pilot} | {StartedUtc:yyyy-MM-dd HH:mm:ss} UTC\n" +
        (EndedUtc.HasValue ? $"Ended {EndedUtc:HH:mm:ss} UTC" : "In progress");
}
