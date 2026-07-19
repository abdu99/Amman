using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Aman.Encoder.ViewModels;

namespace Aman.Encoder;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    private Point _dragStart;
    private VideoItemViewModel? _draggedItem;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    private void PasswordBox1_PasswordChanged(object sender, RoutedEventArgs e) => _viewModel.Password = PasswordBox1.Password;
    private void PasswordBox2_PasswordChanged(object sender, RoutedEventArgs e) => _viewModel.ConfirmPassword = PasswordBox2.Password;

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var paths = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        _viewModel.AddFiles(paths);
    }

    // --- Playlist drag-to-reorder ---

    private void PlaylistBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _draggedItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource)?.DataContext as VideoItemViewModel;
    }

    private void PlaylistBox_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _draggedItem is null) return;

        Point pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        DragDrop.DoDragDrop(PlaylistBox, _draggedItem, DragDropEffects.Move);
    }

    private void PlaylistBox_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(VideoItemViewModel))) return;

        var source = (VideoItemViewModel)e.Data.GetData(typeof(VideoItemViewModel))!;
        var targetContainer = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
        if (targetContainer?.DataContext is not VideoItemViewModel target || ReferenceEquals(source, target)) return;

        int oldIndex = _viewModel.Videos.IndexOf(source);
        int newIndex = _viewModel.Videos.IndexOf(target);
        if (oldIndex >= 0 && newIndex >= 0)
            _viewModel.Videos.Move(oldIndex, newIndex);
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match) return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
