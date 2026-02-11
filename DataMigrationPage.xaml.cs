using Microsoft.Data.SqlClient;
using MigrationTool.Shared;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MigrationTool
{
    public partial class DataMigrationPage : Page
    {
        public ObservableCollection<TableMetadata> TableList { get; set; } = new();

        public DataMigrationPage()
        {
            InitializeComponent();
            Loaded += (s, e) => _ = LoadTablesIntoGridAsync();
        }

        private async Task LoadTablesIntoGridAsync()
        {
            if (ConnectionsService.SourceBuilder == null)
            {
                ShowStatus("Please configure source connection first.", false);
                return;
            }

            TableList.Clear();
            try
            {
                using var conn = new SqlConnection(ConnectionsService.SourceBuilder.ConnectionString);
                await conn.OpenAsync();

                string query = "SELECT TABLE_SCHEMA, TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME";
                using var cmd = new SqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    TableList.Add(new TableMetadata
                    {
                        Schema = reader.GetString(0),
                        Name = reader.GetString(1)
                    });
                }

                TablesGrid.ItemsSource = TableList;
                ShowStatus($"Loaded {TableList.Count} tables.", true);
            }
            catch (Exception ex)
            {
                ShowStatus($"Error loading tables: {ex.Message}", false);
            }
        }

        private void RefreshTables_Click(object sender, RoutedEventArgs e) => _ = LoadTablesIntoGridAsync();

        private void TableSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = TableSearchBox.Text.ToLower();
            if (string.IsNullOrWhiteSpace(searchText))
            {
                TablesGrid.ItemsSource = TableList;
            }
            else
            {
                TablesGrid.ItemsSource = new ObservableCollection<TableMetadata>(
                    TableList.Where(t => t.Name.ToLower().Contains(searchText) ||
                                         t.Schema.ToLower().Contains(searchText))
                );
            }
        }

        private async Task RunBulkCopyAsync(string fullTableName)
        {
            using var sourceConn = new SqlConnection(ConnectionsService.SourceBuilder.ConnectionString);
            using var destConn = new SqlConnection(ConnectionsService.DestBuilder.ConnectionString);

            await sourceConn.OpenAsync();
            await destConn.OpenAsync();

            // Parse row limit
            int limit = 0;
            if (int.TryParse(RowLimitBox.Text, out int parsedLimit))
            {
                limit = parsedLimit;
            }
            
            string topClause = limit > 0 ? $"TOP ({limit})" : "";

            // Where condition
            string filter = WhereConditionBox.Text.Trim();
            string whereClause = !string.IsNullOrEmpty(filter) ? $"WHERE {filter}" : "";

            // Get total count
            string countQuery = limit > 0
                ? $"SELECT COUNT(*) FROM (SELECT TOP ({limit}) * FROM {fullTableName} {whereClause}) AS T"
                : $"SELECT COUNT(*) FROM {fullTableName} {whereClause}";

            var countCmd = new SqlCommand(countQuery, sourceConn);
            int totalRows = (int)await countCmd.ExecuteScalarAsync();

            // Update UI
            Dispatcher.Invoke(() =>
            {
                MigrationProgress.Maximum = totalRows;
                MigrationProgress.Value = 0;
                MigrationProgress.Visibility = Visibility.Visible;
                ProgressStatusText.Text = $"Migrating {fullTableName}...";
            });

            // Setup Reader
            string selectQuery = $"SELECT {topClause} * FROM {fullTableName} {whereClause}";
            var cmd = new SqlCommand(selectQuery, sourceConn);
            using var reader = await cmd.ExecuteReaderAsync();

            // Setup Bulk Copy
            using var bulkCopy = new SqlBulkCopy(destConn, SqlBulkCopyOptions.KeepIdentity, null);
            bulkCopy.DestinationTableName = fullTableName;
            bulkCopy.NotifyAfter = 100;

            bulkCopy.SqlRowsCopied += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    MigrationProgress.Value = e.RowsCopied;
                    ProgressStatusText.Text = $"Copied {e.RowsCopied} of {totalRows} rows...";
                });
            };

            await bulkCopy.WriteToServerAsync(reader);

            Dispatcher.Invoke(() =>
            {
                ProgressStatusText.Text = $"Completed {fullTableName}";
            });
        }

        private void ConfigureMapping_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is TableMetadata table)
            {
                NavigationService?.Navigate(new ColumnMappingPage(table));
            }
        }

        private async void StartMigration_Click(object sender, RoutedEventArgs e)
        {
            var selectedTables = TablesGrid.SelectedItems.Cast<TableMetadata>().ToList();

            if (!selectedTables.Any())
            {
                ShowStatus("Please select at least one table from the list.", false);
                return;
            }

            if (ConnectionsService.DestBuilder == null)
            {
                ShowStatus("Please configure destination connection first.", false);
                return;
            }

            MigrationProgress.Visibility = Visibility.Visible;

            try
            {
                foreach (var table in selectedTables)
                {
                    // Try to load existing mapping
                    var mapping = await MappingConfiguration.LoadMappingAsync(
                        ConnectionsService.SourceBuilder.ConnectionString,
                        ConnectionsService.DestBuilder.ConnectionString,
                        table.Schema,
                        table.Name);

                    if (mapping != null)
                    {
                        // Use mapping-based migration
                        ProgressStatusText.Text = $"Migrating {table.Schema}.{table.Name} with column mapping...";
                        
                        int totalRows = await MigrationService.GetRowCountAsync(
                            ConnectionsService.SourceBuilder.ConnectionString,
                            table.Schema,
                            table.Name,
                            WhereConditionBox.Text.Trim(),
                            int.TryParse(RowLimitBox.Text, out int limit) ? limit : 0);

                        MigrationProgress.Maximum = totalRows;
                        MigrationProgress.Value = 0;

                        await MigrationService.MigrateWithMappingAsync(
                            ConnectionsService.SourceBuilder.ConnectionString,
                            ConnectionsService.DestBuilder.ConnectionString,
                            mapping,
                            WhereConditionBox.Text.Trim(),
                            int.TryParse(RowLimitBox.Text, out int rowLimit) ? rowLimit : 0,
                            (rowsCopied) =>
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    MigrationProgress.Value = rowsCopied;
                                    ProgressStatusText.Text = $"Copied {rowsCopied} of {totalRows} rows...";
                                });
                            });
                    }
                    else
                    {
                        // Use traditional bulk copy (assumes identical schemas)
                        string tableName = $"[{table.Schema}].[{table.Name}]";
                        await RunBulkCopyAsync(tableName);
                    }
                }

                ShowStatus($"Successfully migrated {selectedTables.Count} table(s)!", true);
            }
            catch (Exception ex)
            {
                ShowStatus($"Migration Error: {ex.Message}", false);
            }
            finally
            {
                MigrationProgress.Visibility = Visibility.Collapsed;
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
    }
}
