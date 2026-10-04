using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AfterburnCAFE.Services;
using System.Threading.Tasks;
using System;
using System.Collections.ObjectModel;
using AfterburnCAFE.Models;

namespace AfterburnCAFE.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _greeting = "Combat Analytics For EVE";

    [ObservableProperty]
    private string _logFolderStatus = "Find EVE logs in local and OneDrive Documents.";

    public ObservableCollection<CombatRun> Runs { get; } = new();

    [ObservableProperty]
    private CombatRun? _selectedRun;

    [ObservableProperty]
    private bool _isScanning;

    [RelayCommand]
    private async Task FindLogsAsync()
    {
        IsScanning = true;
        LogFolderStatus = "Scanning EVE game logs...";
        try
        {
            var result = await Task.Run(GameLogDiscovery.Scan);
            Runs.Clear();
            foreach (var run in result.Runs) Runs.Add(run);
            SelectedRun = Runs.Count > 0 ? Runs[0] : null;
            LogFolderStatus = result.Status;
        }
        catch (Exception ex) { LogFolderStatus = $"Scan failed: {ex.Message}"; }
        finally { IsScanning = false; }
    }
}
