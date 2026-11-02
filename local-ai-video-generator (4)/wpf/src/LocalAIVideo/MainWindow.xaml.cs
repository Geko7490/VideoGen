using System.Windows;
using LocalAIVideo.ViewModels;

namespace LocalAIVideo;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
    }

    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button b && b.Tag is string tag && double.TryParse(tag, out var v))
            _vm.DurationSec = v;
    }

    private void SceneRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is SceneRow row)
            _vm.SelectSceneCommand.Execute(row);
    }
}
