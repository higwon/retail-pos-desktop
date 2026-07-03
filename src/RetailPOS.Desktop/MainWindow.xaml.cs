using System.Windows;

namespace RetailPOS.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(Controls.NavigationHost navigationHost)
    {
        InitializeComponent();
        Root.Children.Add(navigationHost);
    }
}
