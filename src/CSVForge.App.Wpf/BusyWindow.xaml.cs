using System.Windows;

namespace CSVForge.App.Wpf;

public partial class BusyWindow : Window
{
    public BusyWindow(string message)
    {
        InitializeComponent();
        MessageText.Text = message;
    }
}
