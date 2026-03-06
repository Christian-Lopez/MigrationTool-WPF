using Microsoft.Data.SqlClient;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using MigrationTool.Shared;

namespace MigrationTool
{
    public partial class ConnectionsPage : Page
    {

        public ConnectionsPage()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                LoadSavedSettings();
                AppSettingsPathBox.Text = AppSettings.SettingsFilePath;
            };
            // Refresh the path label whenever we navigate back to this page
            IsVisibleChanged += (s, e) =>
            {
                if ((bool)e.NewValue)
                    AppSettingsPathBox.Text = AppSettings.SettingsFilePath;
            };
        }

        private async void TestSourceConnection_Click(object sender, RoutedEventArgs e)
        {
            var dataSource = SourceServer.Text;

            // For LocalDB, resolve to actual pipe name for reliability
            if (dataSource.Contains("(localdb)", StringComparison.OrdinalIgnoreCase))
            {
                var pipeName = await GetLocalDbPipeNameAsync();
                if (!string.IsNullOrEmpty(pipeName))
                {
                    dataSource = pipeName;
                    Debug.WriteLine($"Resolved LocalDB to pipe: {pipeName}");
                }
            }

            // Build the connection string
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = dataSource,
                InitialCatalog = SourceDatabase.Text,
                IntegratedSecurity = SourceUseWindowsAuth.IsChecked ?? false,
                Encrypt = false, // Changed for Server 2016 compatibility
                TrustServerCertificate = true,
                ConnectTimeout = 30,
                Pooling = true,
                MinPoolSize = 0,
                MaxPoolSize = 100
            };

            // Only add credentials if NOT using Windows Auth
            if (!(SourceUseWindowsAuth.IsChecked ?? false))
            {
                builder.UserID = SourceUser.Text;
                builder.Password = SourcePassword.Password;
            }

            SaveBasicSettings(true, SourceServer.Text, SourceDatabase.Text, SourceUser.Text, SourcePassword.Password, SourceUseWindowsAuth.IsChecked ?? false);

            if (await OpenConnectionAsync(builder.ConnectionString))
            {
                ConnectionsService.SourceBuilder = builder;
                ShowStatus("Source connection successful!", true);
            }
        }

        private async void TestDestinationConnection_Click(object sender, RoutedEventArgs e)
        {
            var dataSource = DestinationServer.Text;

            // For LocalDB, resolve to actual pipe name for reliability
            if (dataSource.Contains("(localdb)", StringComparison.OrdinalIgnoreCase))
            {
                var pipeName = await GetLocalDbPipeNameAsync();
                if (!string.IsNullOrEmpty(pipeName))
                {
                    dataSource = pipeName;
                    Debug.WriteLine($"Resolved LocalDB to pipe: {pipeName}");
                }
            }

            // Build the connection string
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = dataSource,
                InitialCatalog = DestinationDatabase.Text,
                IntegratedSecurity = DestinationUseWindowsAuth.IsChecked ?? false,
                Encrypt = false, // Changed for Server 2016 compatibility
                TrustServerCertificate = true,
                ConnectTimeout = 30,
                Pooling = true,
                MinPoolSize = 0,
                MaxPoolSize = 100
            };

            // Only add credentials if NOT using Windows Auth
            if (!(DestinationUseWindowsAuth.IsChecked ?? false))
            {
                builder.UserID = DestinationUser.Text;
                builder.Password = DestinationPassword.Password;
            }

            SaveBasicSettings(false, DestinationServer.Text, DestinationDatabase.Text, DestinationUser.Text, DestinationPassword.Password, DestinationUseWindowsAuth.IsChecked ?? false);

            if (await OpenConnectionAsync(builder.ConnectionString))
            {
                ConnectionsService.DestBuilder = builder;
                ShowStatus("Destination connection successful!", true);
            }
        }

        private async Task<string?> GetLocalDbPipeNameAsync()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "sqllocaldb",
                    Arguments = "info MSSQLLocalDB",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    var output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    // Use regex to extract the pipe name
                    var match = Regex.Match(
                        output,
                        @"Instance pipe name:\s*(np:[^\r\n]+)",
                        RegexOptions.IgnoreCase
                    );

                    if (match.Success)
                    {
                        var pipeName = match.Groups[1].Value.Trim();
                        Debug.WriteLine($"Found pipe name: {pipeName}");
                        return pipeName;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting LocalDB pipe name: {ex.Message}");
            }

            return null;
        }

        private async Task<bool> OpenConnectionAsync(string connectionString)
        {
            try
            {
                // Clear the connection pool before attempting to connect
                SqlConnection.ClearAllPools();

                // For LocalDB: Ensure instance is started
                if (connectionString.Contains("(localdb)", StringComparison.OrdinalIgnoreCase) ||
                    connectionString.Contains("np:", StringComparison.OrdinalIgnoreCase))
                {
                    await EnsureLocalDbStartedAsync();
                }

                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                // Verify with a simple query
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT DB_NAME()";
                var result = await command.ExecuteScalarAsync();

                Debug.WriteLine($"Successfully connected to: {result}");

                return true;
            }
            catch (SqlException ex)
            {
                ShowStatus($"SQL Error: {ex.Number} - {ex.Message}", false);
                Debug.WriteLine($"SqlException Details: {ex}");
                return false;
            }
            catch (Exception ex)
            {
                ShowStatus($"Error: {ex.Message}", false);
                Debug.WriteLine($"Exception Details: {ex}");
                return false;
            }
        }

        private async Task EnsureLocalDbStartedAsync()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "sqllocaldb",
                    Arguments = "start MSSQLLocalDB",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LocalDB start warning: {ex.Message}");
            }
        }

        private void ShowStatus(string message, bool isSuccess)
        {
            StatusText.Text = message;
            StatusBorder.Background = isSuccess ?
                new SolidColorBrush(Color.FromRgb(220, 252, 231)) :
                new SolidColorBrush(Color.FromRgb(254, 226, 226));
            StatusBorder.BorderBrush = isSuccess ?
                new SolidColorBrush(Color.FromRgb(134, 239, 172)) :
                new SolidColorBrush(Color.FromRgb(252, 165, 165));
            StatusText.Foreground = isSuccess ?
                new SolidColorBrush(Color.FromRgb(22, 101, 52)) :
                new SolidColorBrush(Color.FromRgb(127, 29, 29));
            StatusBorder.Visibility = Visibility.Visible;
        }

        private void Auth_CheckChanged(object sender, RoutedEventArgs e)
        {
            if (SourceCredentialsGrid == null || DestinationCredentialsGrid == null) return;

            bool isWindowsAuthS = SourceUseWindowsAuth.IsChecked ?? false;
            SourceCredentialsGrid.Opacity = isWindowsAuthS ? 0.5 : 1.0;

            bool isWindowsAuthD = DestinationUseWindowsAuth.IsChecked ?? false;
            DestinationCredentialsGrid.Opacity = isWindowsAuthD ? 0.5 : 1.0;
        }

        private void SaveBasicSettings(bool isSource, string server, string database, string user, string password, bool isWindowsAuth)
        {
            try
            {
                var s = AppSettings.Load();
                if (isSource)
                {
                    s.SourceServer = server;
                    s.SourceDatabase = database;
                    s.SourceUser = user;
                    s.SourceUseWindowsAuth = isWindowsAuth;
                }
                else
                {
                    s.DestServer = server;
                    s.DestDatabase = database;
                    s.DestUser = user;
                    s.DestUseWindowsAuth = isWindowsAuth;
                }
                // Always stamp the current mapping path so it's preserved in the JSON
                s.MappingDirectory = MappingConfiguration.MappingDirectory;
                AppSettings.Save(s);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        private void LoadSavedSettings()
        {
            try
            {
                var s = AppSettings.Load();

                if (!string.IsNullOrEmpty(s.SourceServer))   SourceServer.Text       = s.SourceServer;
                if (!string.IsNullOrEmpty(s.SourceDatabase)) SourceDatabase.Text     = s.SourceDatabase;
                if (!string.IsNullOrEmpty(s.SourceUser))     SourceUser.Text         = s.SourceUser;
                SourceUseWindowsAuth.IsChecked = s.SourceUseWindowsAuth;

                if (!string.IsNullOrEmpty(s.DestServer))     DestinationServer.Text   = s.DestServer;
                if (!string.IsNullOrEmpty(s.DestDatabase))   DestinationDatabase.Text = s.DestDatabase;
                if (!string.IsNullOrEmpty(s.DestUser))       DestinationUser.Text     = s.DestUser;
                DestinationUseWindowsAuth.IsChecked = s.DestUseWindowsAuth;

                Auth_CheckChanged(null!, null!);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading settings: {ex.Message}");
            }
        }

        private void BrowseAppSettingsPath_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select folder for app_settings.json",
                SelectedPath = System.IO.Path.GetDirectoryName(AppSettings.SettingsFilePath)!,
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                AppSettings.ChangeSettingsPath(dialog.SelectedPath);
                AppSettingsPathBox.Text = AppSettings.SettingsFilePath;
            }
        }

        private void ResetAppSettingsPath_Click(object sender, RoutedEventArgs e)
        {
            AppSettings.ResetSettingsPath();
            AppSettingsPathBox.Text = AppSettings.SettingsFilePath;
        }
    }

    // Inverse Boolean Converter for WPF binding
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;
            return true;
        }
    }
}
