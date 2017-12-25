using MahApps.Metro.Controls;

namespace MacEfiDb
{
    /// <summary>
    /// AboutApp.xaml 的交互逻辑
    /// </summary>
    public partial class AboutApp : MetroWindow
    {
        public AboutApp()
        {
            InitializeComponent();
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(e.Uri.AbsoluteUri);
        }
    }
}
