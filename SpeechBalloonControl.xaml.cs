using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ComicSpeechBalloon;

public partial class SpeechBalloonControl : UserControl
{

    private static readonly Color[] AccentColors =
    [
        Color.FromRgb(255, 225, 53),  // Yellow
        Color.FromRgb(255, 94, 77),   // Coral
        Color.FromRgb(0, 255, 127),   // Spring Green
        Color.FromRgb(255, 105, 180), // Hot Pink
        Color.FromRgb(0, 214, 255),   // Cyan
        Color.FromRgb(255, 160, 0),   // Orange
        Color.FromRgb(186, 85, 211),  // Orchid
        Color.FromRgb(255, 215, 0),   // Gold
    ];

    private static readonly Random Rng = new();
    private readonly Storyboard _fadeInStoryboard;
    private readonly Storyboard _fadeOutStoryboard;
    private TaskCompletionSource<bool>? _hideCompletion;

    public enum TailSide { Left, Right }

    /// <summary>
    /// Returns a fallback message when AI is unavailable.
    /// </summary>
    public static string PickRandomPhrase() => "Set your DeepSeek API key to get started!";

    /// <summary>
    /// Creates a balloon with a random built-in phrase.
    /// </summary>
    public SpeechBalloonControl() : this(null) { }

    /// <summary>
    /// Creates a balloon with a specific phrase (from DeepSeek or fallback).
    /// If <paramref name="customPhrase"/> is null or empty, picks a random one.
    /// </summary>
    public SpeechBalloonControl(string? customPhrase)
    {
        InitializeComponent();

        var phrase = !string.IsNullOrWhiteSpace(customPhrase) ? customPhrase : PickRandomPhrase();
        var randomColor = AccentColors[Rng.Next(AccentColors.Length)];

        var fontSize = PickFontSize(phrase);

        BalloonText.Text = phrase;
        BalloonText.FontSize = fontSize;
        AccentBrush.Color = randomColor;

        var accentColorBrush = new SolidColorBrush(randomColor);
        TailLeft.Stroke = accentColorBrush;
        TailRight.Stroke = accentColorBrush;

        SetTailSide(TailSide.Left);

        _fadeInStoryboard = new Storyboard();

        var borderOpacityIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(350))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(borderOpacityIn, BalloonBody);
        Storyboard.SetTargetProperty(borderOpacityIn, new PropertyPath(OpacityProperty));
        _fadeInStoryboard.Children.Add(borderOpacityIn);

        var scaleXIn = new DoubleAnimation(0.6, 1.0, TimeSpan.FromMilliseconds(400))
        {
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 }
        };
        Storyboard.SetTarget(scaleXIn, PopScale);
        Storyboard.SetTargetProperty(scaleXIn, new PropertyPath(ScaleTransform.ScaleXProperty));
        _fadeInStoryboard.Children.Add(scaleXIn);

        var scaleYIn = new DoubleAnimation(0.6, 1.0, TimeSpan.FromMilliseconds(400))
        {
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 }
        };
        Storyboard.SetTarget(scaleYIn, PopScale);
        Storyboard.SetTargetProperty(scaleYIn, new PropertyPath(ScaleTransform.ScaleYProperty));
        _fadeInStoryboard.Children.Add(scaleYIn);

        BalloonBody.Opacity = 0;

        _fadeOutStoryboard = new Storyboard();

        var borderOpacityOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(500))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(borderOpacityOut, BalloonBody);
        Storyboard.SetTargetProperty(borderOpacityOut, new PropertyPath(OpacityProperty));
        _fadeOutStoryboard.Children.Add(borderOpacityOut);

        _fadeOutStoryboard.Completed += (_, _) =>
        {
            _hideCompletion?.TrySetResult(true);
        };
    }

    private static double PickFontSize(string text)
    {
        int len = text.Length;
        if (len <= 8) return 42;
        if (len <= 14) return 34;
        if (len <= 20) return 28;
        if (len <= 28) return 24;
        if (len <= 38) return 20;
        if (len <= 50) return 17;
        return 14;
    }

    public void SetTailSide(TailSide side)
    {
        TailLeft.Visibility = side == TailSide.Left ? Visibility.Visible : Visibility.Collapsed;
        TailRight.Visibility = side == TailSide.Right ? Visibility.Visible : Visibility.Collapsed;
    }

    public async Task ShowAsync()
    {
        _fadeInStoryboard.Stop();
        _fadeInStoryboard.Begin();
        await Task.Delay(450);
    }

    public async Task HideAsync()
    {
        _hideCompletion = new TaskCompletionSource<bool>();
        _fadeOutStoryboard.Stop();
        _fadeOutStoryboard.Begin();
        await _hideCompletion.Task;
    }
}
