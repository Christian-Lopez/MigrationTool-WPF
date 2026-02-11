using System.Windows;
using System.Windows.Controls;

namespace MigrationTool
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            // Set version from assembly
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = $"v{version?.Major}.{version?.Minor}.{version?.Build}";
            
            // Navigate to Connections page by default
            ContentFrame.Navigate(new ConnectionsPage());
        }

        private void NavigateToConnections_Click(object sender, RoutedEventArgs e)
        {
            ContentFrame.Navigate(new ConnectionsPage());
        }

        private void NavigateToDataMigration_Click(object sender, RoutedEventArgs e)
        {
            ContentFrame.Navigate(new DataMigrationPage());
        }

        private void NavigateToMigrations_Click(object sender, RoutedEventArgs e)
        {
            ContentFrame.Navigate(new MigrationsPage());
        }
    }
}
