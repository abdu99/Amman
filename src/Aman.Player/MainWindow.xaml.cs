using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Aman.Player.Services;
using Aman.Player.ViewModels;

namespace Aman.Player;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private readonly AntiCaptureService _antiCapture = new();
    private readonly DispatcherTimer _positionTimer;
    private bool _isDraggingSeek;
    private readonly Random _rng = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _positionTimer.Tick += (_, _) => UpdatePositionUi();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _antiCapture.ProtectWindow(this);
        _antiCapture.InstallKeyboardHook();
        StartWatermarkDrift();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _positionTimer.Stop();
        _antiCapture.Dispose();
        _viewModel.Dispose();
    }

    private void PasswordBox1_PasswordChanged(object sender, RoutedEventArgs e) => _viewModel.SetPassword(PasswordBox1.Password);

    private void CopyMachineId_Click(object sender, RoutedEventArgs e) => Clipboard.SetText(_viewModel.MachineIdDisplay);

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.CurrentVideoUri)) return;

        Dispatcher.Invoke(() =>
        {
            if (_viewModel.CurrentVideoUri is null) return;
            Media.Source = _viewModel.CurrentVideoUri;
            Media.Play();
            PlayPauseButton.Content = "⏸";
            _positionTimer.Start();
        });
    }

    // --- Transport controls (imperative MediaElement API — not bindable) ---

    private void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (Media.Source is null) return;

        if (PlayPauseButton.Content as string == "⏸")
        {
            Media.Pause();
            PlayPauseButton.Content = "▶";
        }
        else
        {
            Media.Play();
            PlayPauseButton.Content = "⏸";
        }
    }

    private void Next_Click(object sender, RoutedEventArgs e) => _viewModel.NextCommand.Execute(null);
    private void Previous_Click(object sender, RoutedEventArgs e) => _viewModel.PreviousCommand.Execute(null);

    private void Media_MediaOpened(object sender, RoutedEventArgs e)
    {
        if (Media.NaturalDuration.HasTimeSpan)
            SeekSlider.Maximum = Media.NaturalDuration.TimeSpan.TotalSeconds;
    }

    private void Media_MediaEnded(object sender, RoutedEventArgs e) => _viewModel.NextCommand.Execute(null);

    private void UpdatePositionUi()
    {
        if (_isDraggingSeek || Media.Source is null || !Media.NaturalDuration.HasTimeSpan) return;

        SeekSlider.Value = Media.Position.TotalSeconds;
        TimeLabel.Text = $"{Media.Position:mm\\:ss} / {Media.NaturalDuration.TimeSpan:mm\\:ss}";
    }

    private void SeekSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e) => _isDraggingSeek = true;

    private void SeekSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingSeek = false;
        Media.Position = TimeSpan.FromSeconds(SeekSlider.Value);
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => Media.Volume = e.NewValue;

    // --- Deterrents (see AntiCaptureService remarks: none of this is a hard guarantee) ---

    private void Video_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) => e.Handled = true;

    private void Video_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) ToggleFullscreen();
    }

    private void ToggleFullscreen()
    {
        if (WindowStyle == WindowStyle.None)
        {
            WindowStyle = WindowStyle.SingleBorderWindow;
            WindowState = WindowState.Normal;
        }
        else
        {
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (ctrl && e.Key is Key.C or Key.S or Key.P)
            e.Handled = true; // best-effort: block obvious copy/save/print shortcuts within the window
    }

    /// <summary>Moves the traceability watermark every few seconds so a static crop can't remove it.</summary>
    private void StartWatermarkDrift()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        timer.Tick += (_, _) =>
        {
            Watermark.HorizontalAlignment = _rng.Next(2) == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            Watermark.VerticalAlignment = _rng.Next(2) == 0 ? VerticalAlignment.Top : VerticalAlignment.Bottom;
        };
        timer.Start();
    }
}
