using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AfterburnCAFE.Models;
using AfterburnCAFE.Services;

namespace AfterburnCAFE.ViewModels;

public partial class RunMarkerViewModel : ViewModelBase
{
    private readonly RunMarkerStore _store = new();
    public string[] Activities { get; } = { "Combat site", "Abyssal Deadspace", "Other / custom" };
    public string[] Weathers { get; } = { "Dark", "Electrical", "Exotic", "Firestorm", "Gamma" };
    public string[] Tiers { get; } = { "T0 - Tranquil", "T1 - Calm", "T2 - Agitated", "T3 - Fierce", "T4 - Raging", "T5 - Chaotic", "T6 - Cataclysmic" };
    public string[] SitePresets { get; } =
    {
        "Angel Sanctum", "Angel Haven", "Blood Sanctum", "Blood Haven", "Guristas Sanctum", "Guristas Haven",
        "Sansha Sanctum", "Sansha Haven", "Serpentis Sanctum", "Serpentis Haven", "Drone Horde", "Drone Patrol"
    };
    public ObservableCollection<string> Pilots { get; } = new();
    public ObservableCollection<RunMarker> Entries { get; } = new();
    public event Action? MarkersChanged;

    [ObservableProperty] private string _pilot = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAbyssal), nameof(IsSite))]
    private string _activity = "Combat site";
    public bool IsAbyssal => Activity == "Abyssal Deadspace";
    public bool IsSite => !IsAbyssal;
    [ObservableProperty] private string _weather = "Exotic";
    [ObservableProperty] private string _tier = "T3 - Fierce";
    [ObservableProperty] private string _siteName = "Angel Sanctum";
    [ObservableProperty] private string? _selectedPreset;
    [ObservableProperty] private string _variant = "";
    [ObservableProperty] private string _notes = "";
    [ObservableProperty] private string _status = "Mark entry when you enter; mark exit when you leave.";
    [ObservableProperty] private bool _isRecording;
    [ObservableProperty] private string _activeLabel = "";
    [ObservableProperty] private bool _isBusy;
    private RunMarker? _active;
    public string Clock { get; private set; } = "";

    public RunMarkerViewModel(IEnumerable<string> pilots, string? selectedPilot)
    {
        foreach (var pilot in pilots.Distinct()) Pilots.Add(pilot);
        Pilot = selectedPilot ?? Pilots.FirstOrDefault() ?? "";
        Tick();
    }

    partial void OnSelectedPresetChanged(string? value) { if (value is not null) SiteName = value; }
    partial void OnPilotChanged(string value) => UpdateActive();

    public void Tick()
    {
        Clock = $"EVE time (UTC): {DateTime.UtcNow:HH:mm:ss}";
        OnPropertyChanged(nameof(Clock));
    }

    private void UpdateActive()
    {
        _active = Entries.FirstOrDefault(m => m.EndedUtc is null && m.Pilot.Equals(Pilot.Trim(), StringComparison.OrdinalIgnoreCase));
        IsRecording = _active is not null;
        ActiveLabel = _active?.Display ?? "";
    }

    [RelayCommand]
    public async Task ReloadAsync()
    {
        try
        {
            var entries = await Task.Run(_store.Load);
            Entries.Clear();
            foreach (var entry in entries) Entries.Add(entry);
            UpdateActive();
        }
        catch (Exception ex) { Status = $"Could not load entries: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        var now = DateTime.UtcNow; // Capture click time before disk I/O.
        var label = IsAbyssal ? $"{Weather} {Tier.Split(' ')[0]} Abyssal" : SiteName;
        var pilot = Pilot; var activity = Activity; var variant = Variant; var notes = Notes;
        IsBusy = true;
        try
        {
            var entry = await Task.Run(() => _store.Start(pilot, activity, label, variant, notes, now));
            await ReloadAsync();
            Status = $"Entry saved at {entry.StartedUtc:HH:mm:ss} UTC.";
            MarkersChanged?.Invoke();
        }
        catch (Exception ex) { Status = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task EndAsync()
    {
        var now = DateTime.UtcNow;
        if (_active is null) return;
        var id = _active.Id;
        IsBusy = true;
        try
        {
            var entry = await Task.Run(() => _store.End(id, now));
            await ReloadAsync();
            Status = $"Exit saved at {entry.EndedUtc:HH:mm:ss} UTC.";
            MarkersChanged?.Invoke();
        }
        catch (Exception ex) { Status = ex.Message; }
        finally { IsBusy = false; }
    }
}
