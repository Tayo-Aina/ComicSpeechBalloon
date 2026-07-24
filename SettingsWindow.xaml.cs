using System.Windows;
using System.Windows.Controls;

namespace ComicSpeechBalloon;

/// <summary>
/// Floating settings panel — intervals, toggles, API key management.
/// Closing hides to tray (does not exit the app).
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly Action<int>? _onIntervalChanged;
    private readonly Action<int>? _onDurationChanged;
    private readonly Action? _onExitRequested;

    public SettingsWindow(Action<int>? onIntervalChanged = null,
                          Action<int>? onDurationChanged = null,
                          Action? onExitRequested = null)
    {
        InitializeComponent();
        _onIntervalChanged = onIntervalChanged;
        _onDurationChanged = onDurationChanged;
        _onExitRequested = onExitRequested;

        // Load current values from config
        IntervalSlider.Value = AppConfig.SpawnIntervalSeconds;
        IntervalLabel.Text = $"{AppConfig.SpawnIntervalSeconds} sec";

        DurationSlider.Value = AppConfig.DisplayDurationSeconds;
        DurationLabel.Text = $"{AppConfig.DisplayDurationSeconds} sec";

        AiToggle.IsChecked = DeepSeekConfig.IsEnabled;
        RoastToggle.IsChecked = DeepSeekConfig.RoastMode;

        UpdateKeyStatus();
    }

    private void UpdateKeyStatus()
    {
        if (ApiKeyStore.HasKey)
            KeyStatusLabel.Text = "Key is set and encrypted. AI is ready.";
        else
            KeyStatusLabel.Text = "No API key set. Click below to add one.";
    }

    private void OnIntervalChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        int seconds = (int)Math.Round(e.NewValue);
        IntervalLabel.Text = $"{seconds} sec";
        AppConfig.SpawnIntervalSeconds = seconds;
        _onIntervalChanged?.Invoke(seconds);
    }

    private void OnDurationChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        int seconds = (int)Math.Round(e.NewValue);
        DurationLabel.Text = $"{seconds} sec";
        AppConfig.DisplayDurationSeconds = seconds;
        _onDurationChanged?.Invoke(seconds);
    }

    private void OnAiToggled(object sender, RoutedEventArgs e)
    {
        DeepSeekConfig.IsEnabled = AiToggle.IsChecked == true;
    }

    private void OnRoastToggled(object sender, RoutedEventArgs e)
    {
        DeepSeekConfig.RoastMode = RoastToggle.IsChecked == true;
    }

    private void OnChangeKeyClick(object sender, RoutedEventArgs e)
    {
        // Open the key dialog from MainWindow — we fire via the owner
        if (Owner is MainWindow main)
        {
            main.ShowApiKeyDialog();
            UpdateKeyStatus();
        }
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        this.Hide();
    }

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        _onExitRequested?.Invoke();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Trap the close button (X) — hide instead of closing the app
        e.Cancel = true;
        this.Hide();
    }
}
