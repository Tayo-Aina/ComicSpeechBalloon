using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;

namespace ComicSpeechBalloon;

/// <summary>
/// Transparent full-screen overlay window that hosts comic speech balloons.
/// Click-through and hidden from taskbar.
/// </summary>
public partial class MainWindow : Window
{
    private BalloonManager? _balloonManager;
    private AppContextService? _contextService;
    private RoastEngine? _roastEngine;
    private ActivityTracker? _activityTracker;
    private MemoryStore? _memoryStore;
    private SettingsWindow? _settingsWindow;
    private uint _trayCallbackMsg;
    private bool _initialized;

    public MainWindow()
    {
        InitializeComponent();

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closing += OnClosing;
        LocationChanged += OnScreenBoundsChanged;

        // Cover the entire virtual screen (all monitors) — set after subscribing
        // to LocationChanged so the handler is ready, but guard with _initialized
        UpdateScreenBounds();
        _initialized = true;
    }

    /// <summary>
    /// Sets the window to cover the entire virtual screen (all monitors).
    /// </summary>
    private void UpdateScreenBounds()
    {
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var hwndSource = HwndSource.FromHwnd(hwnd);
        hwndSource?.AddHook(WndProc);

        // Register system tray icon (left-click = settings, right-click = menu)
        _trayCallbackMsg = NativeMethods.AddTrayIcon(hwnd, "Comic Speech Balloon",
            onLeftClick: OpenSettings,
            onRightClick: ShowTrayMenu);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        NativeMethods.HandleTrayMessage(msg, wParam.ToInt32(), lParam.ToInt32());
        return IntPtr.Zero;
    }

    private System.Windows.Controls.Primitives.Popup? _trayPopup;

    private void ShowTrayMenu()
    {
        Dispatcher.Invoke(() =>
        {
            // Temporarily let clicks through so the popup can be dismissed
            var hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.MakeWindowOpaque(hwnd);

            // Close any existing popup first
            if (_trayPopup != null)
            {
                _trayPopup.IsOpen = false;
                _trayPopup = null;
            }

            var popup = new System.Windows.Controls.Primitives.Popup
            {
                Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint,
                StaysOpen = false,
                AllowsTransparency = true,
                PopupAnimation = System.Windows.Controls.Primitives.PopupAnimation.Fade
            };

            _trayPopup = popup;

            void ClosePopup()
            {
                if (_trayPopup != null)
                {
                    _trayPopup.IsOpen = false;
                    _trayPopup = null;
                    // Restore click-through
                    NativeMethods.MakeWindowTransparent(hwnd);
                }
            }

            popup.Closed += (_, _) =>
            {
                _trayPopup = null;
                NativeMethods.MakeWindowTransparent(hwnd);
            };

            var border = new System.Windows.Controls.Border
            {
                Background = System.Windows.Media.Brushes.White,
                BorderBrush = System.Windows.Media.Brushes.DarkGray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(2),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 8,
                    ShadowDepth = 2,
                    Opacity = 0.3
                }
            };

            var stack = new System.Windows.Controls.StackPanel();

            bool hasKey = ApiKeyStore.HasKey;

            var setKeyBtn = new System.Windows.Controls.Button
            {
                Content = "  🔧 Settings  ",
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0, 0, 0, 0),
                Padding = new Thickness(12, 6, 12, 6),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 13
            };
            setKeyBtn.Click += (_, _) =>
            {
                ClosePopup();
                OpenSettings();
            };

            var apiKeyBtn = new System.Windows.Controls.Button
            {
                Content = hasKey ? "  🔑 Change API Key…  " : "  🔑 Set API Key…  ",
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0, 0, 0, 0),
                Padding = new Thickness(12, 6, 12, 6),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 13
            };
            apiKeyBtn.Click += (_, _) =>
            {
                ClosePopup();
                ShowApiKeyDialog();
            };

            var aiToggleBtn = new System.Windows.Controls.Button
            {
                Content = DeepSeekConfig.IsEnabled ? "  🤖 AI Context: ON  " : "  🤖 AI Context: OFF  ",
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0, 0, 0, 0),
                Padding = new Thickness(12, 6, 12, 6),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 13
            };
            aiToggleBtn.Click += (_, _) =>
            {
                DeepSeekConfig.IsEnabled = !DeepSeekConfig.IsEnabled;
                aiToggleBtn.Content = DeepSeekConfig.IsEnabled ? "  🤖 AI Context: ON  " : "  🤖 AI Context: OFF  ";
            };

            var sep = new System.Windows.Controls.Separator
            {
                Margin = new Thickness(8, 4, 8, 4),
                Background = System.Windows.Media.Brushes.LightGray,
                Height = 1
            };

            var exitBtn = new System.Windows.Controls.Button
            {
                Content = "  Exit  ",
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0, 0, 0, 0),
                Padding = new Thickness(12, 6, 12, 6),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 13
            };
            exitBtn.Click += (_, _) =>
            {
                ClosePopup();
                ShutdownApp();
            };

            stack.Children.Add(setKeyBtn);
            stack.Children.Add(aiToggleBtn);
            stack.Children.Add(apiKeyBtn);
            stack.Children.Add(sep);
            stack.Children.Add(exitBtn);
            border.Child = stack;
            popup.Child = border;

            // Capture clicks anywhere on the overlay to dismiss the popup
            var dismissHandler = new System.Windows.Input.MouseButtonEventHandler((_, _) =>
            {
                if (_trayPopup != null && _trayPopup.IsOpen)
                    ClosePopup();
            });
            this.MouseLeftButtonDown += dismissHandler;

            popup.Closed += (_, _) =>
            {
                this.MouseLeftButtonDown -= dismissHandler;
                NativeMethods.MakeWindowTransparent(hwnd);
                _trayPopup = null;
            };

            popup.IsOpen = true;
        });
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Make the window click-through
        var hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.MakeWindowTransparent(hwnd);

        // Wire up DeepSeek AI + memory + activity tracking
        var deepSeekService = new DeepSeekService();
        _memoryStore = new MemoryStore();
        _activityTracker = new ActivityTracker();

        // Feed activity events into memory store
        _activityTracker.OnAppSwitched += evt => _memoryStore.RecordEvent(evt);

        _roastEngine = new RoastEngine(deepSeekService, _memoryStore, _activityTracker);
        _contextService = new AppContextService(_roastEngine);

        // Start background activity polling
        _activityTracker.Start();

        // Start the balloon spawner
        _balloonManager = new BalloonManager(BalloonCanvas, _contextService);
        _balloonManager.Start();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.RemoveTrayIcon(hwnd);
        _activityTracker?.Dispose();
        _balloonManager?.Dispose();
    }

    /// <summary>
    /// Opens the settings window or brings it to front if already open.
    /// </summary>
    private void OpenSettings()
    {
        Dispatcher.Invoke(() =>
        {
            if (_settingsWindow != null && _settingsWindow.IsLoaded)
            {
                _settingsWindow.Show();
                _settingsWindow.Activate();
                return;
            }

            _settingsWindow = new SettingsWindow(
                onIntervalChanged: seconds =>
                {
                    _balloonManager?.UpdateInterval(seconds);
                },
                onDurationChanged: _ => { /* Duration is read from AppConfig each spawn — no action needed */ },
                onExitRequested: ShutdownApp)
            {
                Owner = this
            };

            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        });
    }

    /// <summary>
    /// Opens a small input window for the user to paste their DeepSeek API key.
    /// Public so SettingsWindow can trigger it.
    /// </summary>
    public void ShowApiKeyDialog()
    {
        var existingKey = ApiKeyStore.LoadKey();

        var dialog = new Window
        {
            Title = "DeepSeek API Key",
            Width = 480,
            Height = 260,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
            ShowInTaskbar = true,
            Topmost = true
        };

        var grid = new System.Windows.Controls.Grid { Margin = new Thickness(20) };
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(10) });
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(14) });
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(14) });
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });

        var label = new System.Windows.Controls.TextBlock
        {
            Text = "Paste your DeepSeek API key below. It will be encrypted\nand stored securely on your computer — never in source code.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            Foreground = System.Windows.Media.Brushes.DimGray
        };
        System.Windows.Controls.Grid.SetRow(label, 0);
        grid.Children.Add(label);

        var keyBox = new System.Windows.Controls.TextBox
        {
            Text = existingKey ?? "",
            FontSize = 13,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            Padding = new Thickness(8, 6, 8, 6)
        };
        System.Windows.Controls.Grid.SetRow(keyBox, 2);
        grid.Children.Add(keyBox);

        var hint = new System.Windows.Controls.TextBlock
        {
            Text = "Get a key at: platform.deepseek.com → API Keys",
            FontSize = 11,
            Foreground = System.Windows.Media.Brushes.Gray
        };
        System.Windows.Controls.Grid.SetRow(hint, 4);
        grid.Children.Add(hint);

        var btnPanel = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var clearBtn = new System.Windows.Controls.Button
        {
            Content = "  Clear Key  ",
            Width = 100,
            Height = 30,
            Margin = new Thickness(0, 0, 8, 0),
            Visibility = string.IsNullOrWhiteSpace(existingKey) ? Visibility.Collapsed : Visibility.Visible
        };
        clearBtn.Click += (_, _) =>
        {
            ApiKeyStore.DeleteKey();
            dialog.Close();
            System.Windows.MessageBox.Show("API key removed. AI context disabled.", "Key Cleared",
                MessageBoxButton.OK, MessageBoxImage.Information);
        };

        var saveBtn = new System.Windows.Controls.Button
        {
            Content = "  Save  ",
            Width = 80,
            Height = 30,
            IsDefault = true
        };
        saveBtn.Click += (_, _) =>
        {
            var key = keyBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                ApiKeyStore.DeleteKey();
                System.Windows.MessageBox.Show("API key removed. AI context disabled.", "Key Cleared",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else if (!key.StartsWith("sk-"))
            {
                System.Windows.MessageBox.Show("That doesn't look like a DeepSeek API key (should start with 'sk-').",
                    "Invalid Key", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            else
            {
                ApiKeyStore.SaveKey(key);
                System.Windows.MessageBox.Show("API key saved! AI context balloons are ready.", "Key Saved",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            dialog.DialogResult = true;
            dialog.Close();
        };

        btnPanel.Children.Add(clearBtn);
        btnPanel.Children.Add(saveBtn);
        System.Windows.Controls.Grid.SetRow(btnPanel, 6);
        grid.Children.Add(btnPanel);

        dialog.Content = grid;
        dialog.Loaded += (_, _) => keyBox.Focus();
        dialog.ShowDialog();
    }

    private void ShutdownApp()
    {
        _activityTracker?.Dispose();
        _memoryStore?.Dispose();
        _balloonManager?.Dispose();
        _contextService?.Dispose();
        var hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.RemoveTrayIcon(hwnd);
        Close();
        Application.Current.Shutdown();
    }

    /// <summary>
    /// Repositions the window if the virtual screen boundaries change (e.g. monitor setup change).
    /// </summary>
    private void OnScreenBoundsChanged(object? sender, EventArgs e)
    {
        if (!_initialized) return;
        UpdateScreenBounds();
    }
}
