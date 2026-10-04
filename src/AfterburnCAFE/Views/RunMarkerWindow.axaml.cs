using Avalonia.Controls;
using Avalonia.Threading;
using AfterburnCAFE.ViewModels;
using System;

namespace AfterburnCAFE.Views;

public partial class RunMarkerWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    public RunMarkerWindow()
    {
        InitializeComponent();
        _timer.Tick += (_, _) => (DataContext as RunMarkerViewModel)?.Tick();
        Opened += async (_, _) =>
        {
            _timer.Start();
            if (DataContext is RunMarkerViewModel model) await model.ReloadAsync();
        };
        Closed += (_, _) => _timer.Stop();
    }
}
