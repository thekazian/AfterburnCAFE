using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System;
using System.Linq;
using AfterburnCAFE.Services;

namespace AfterburnCAFE.Views;

public partial class MainWindow : Window
{
    private RunMarkerWindow? _markerWindow;
    public MainWindow()
    {
        InitializeComponent();
        Closed += (_, _) => _markerWindow?.Close();
        Opened += async (_, _) =>
        {
            if (DataContext is ViewModels.MainViewModel model)
                await model.FindLogsCommand.ExecuteAsync(null);
        };
    }

    private void OpenRunMarker_Click(object? sender, RoutedEventArgs e)
    {
        if (_markerWindow is not null) { _markerWindow.Activate(); return; }
        if (DataContext is not ViewModels.MainViewModel model) return;
        var markerModel = new ViewModels.RunMarkerViewModel(model.Runs.Select(r => r.Pilot), model.SelectedRun?.Pilot);
        markerModel.MarkersChanged += () =>
        {
            if (!model.FindLogsCommand.IsRunning) _ = model.FindLogsCommand.ExecuteAsync(null);
        };
        _markerWindow = new RunMarkerWindow { DataContext = markerModel };
        _markerWindow.Closed += (_, _) => _markerWindow = null;
        _markerWindow.Show();
    }

    private async void CopyMarkdown_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.MainViewModel { SelectedRun: { } run }) return;
        var status = this.FindControl<TextBlock>("CopyStatus");
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null)
            {
                if (status is not null) status.Text = "Clipboard unavailable";
                return;
            }
            var tab = this.GetVisualDescendants().OfType<TabControl>().FirstOrDefault()?.SelectedItem as TabItem;
            var title = tab?.Header?.ToString() ?? "Metrics";
            await clipboard.SetTextAsync(RunMarkdown.Export(run, title));
            if (status is not null) status.Text = $"Copied {title}";
        }
        catch (Exception)
        {
            if (status is not null) status.Text = "Could not copy. Try again.";
        }
    }
}
