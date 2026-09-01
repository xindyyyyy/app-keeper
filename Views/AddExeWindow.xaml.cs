using Microsoft.Win32;
using System.Windows;
using System.Windows.Input;

namespace AppKeeper.Views;

public partial class AddExeWindow : Window
{
    public AddExeWindow()
    {
        InitializeComponent();
    }

    public string SelectedPath { get; private set; } = string.Empty;

    private void WindowLoaded(object sender, RoutedEventArgs e)
    {
        PathBox.Focus();
        Keyboard.Focus(PathBox);
    }

    private void BrowseClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Windows 程序 (*.exe)|*.exe",
            CheckFileExists = true,
            Multiselect = false,
            Title = "选择需要守护的程序"
        };
        if (dialog.ShowDialog(this) == true)
            PathBox.Text = dialog.FileName;
    }

    private void ConfirmClick(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(PathBox.Text) || !string.Equals(Path.GetExtension(PathBox.Text), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            System.Windows.MessageBox.Show(this, "请选择一个存在的 EXE 文件。", "无法添加", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SelectedPath = PathBox.Text;
        DialogResult = true;
    }
}
