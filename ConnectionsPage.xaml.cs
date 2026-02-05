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
        private const string SettingsFileName = "connection_settings.ini";
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MigrationTool",
            SettingsFileName);

        public ConnectionsPage()
        {
            InitializeComponent();
            Loaded += (s, e) => LoadSavedSettings();
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
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);

                var settings = File.Exists(SettingsFilePath) ? File.ReadAllLines(SettingsFilePath) : Array.Empty<string>();
                var settingsDict = new System.Collections.Generic.Dictionary<string, string>();

                foreach (var line in settings)
                {
                    if (line.Contains('='))
                    {
                        var parts = line.Split(new[] { '=' }, 2);
                        settingsDict[parts[0].Trim()] = parts.Length > 1 ? parts[1].Trim() : "";
                    }
                }

                string prefix = isSource ? "Source" : "Dest";
                settingsDict[$"{prefix}Server"] = server;
                settingsDict[$"{prefix}Database"] = database;
                settingsDict[$"{prefix}User"] = user;
                settingsDict[$"{prefix}UseWindowsAuth"] = isWindowsAuth.ToString();
                // Note: We're not saving passwords for security. Users must re-enter them.

                var lines = new System.Collections.Generic.List<string>();
                foreach (var kvp in settingsDict)
                {
                    lines.Add($"{kvp.Key}={kvp.Value}");
                }

                File.WriteAllLines(SettingsFilePath, lines);
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
                if (!File.Exists(SettingsFilePath)) return;

                var lines = File.ReadAllLines(SettingsFilePath);
                var settingsDict = new System.Collections.Generic.Dictionary<string, string>();

                foreach (var line in lines)
                {
                    if (line.Contains('='))
                    {
                        var parts = line.Split(new[] { '=' }, 2);
                        settingsDict[parts[0].Trim()] = parts.Length > 1 ? parts[1].Trim() : "";
                    }
                }

                if (settingsDict.ContainsKey("SourceServer"))
                    SourceServer.Text = settingsDict["SourceServer"];

                if (settingsDict.ContainsKey("DestServer"))
                    DestinationServer.Text = settingsDict["DestServer"];

                if (settingsDict.ContainsKey("SourceDatabase"))
                    SourceDatabase.Text = settingsDict["SourceDatabase"];

                if (settingsDict.ContainsKey("DestDatabase"))
                    DestinationDatabase.Text = settingsDict["DestDatabase"];

                if (settingsDict.ContainsKey("SourceUser"))
                    SourceUser.Text = settingsDict["SourceUser"];

                if (settingsDict.ContainsKey("DestUser"))
                    DestinationUser.Text = settingsDict["DestUser"];

                if (settingsDict.ContainsKey("SourceUseWindowsAuth"))
                {
                    SourceUseWindowsAuth.IsChecked = bool.Parse(settingsDict["SourceUseWindowsAuth"]);
                }

                if (settingsDict.ContainsKey("DestUseWindowsAuth"))
                {
                    DestinationUseWindowsAuth.IsChecked = bool.Parse(settingsDict["DestUseWindowsAuth"]);
                }

                Auth_CheckChanged(null!, null!);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading settings: {ex.Message}");
            }
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
