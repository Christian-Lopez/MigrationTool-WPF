using MigrationTool.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MigrationTool
{
    public partial class ColumnMappingPage : Page
    {
        public ObservableCollection<ColumnMapping> Mappings { get; set; } = new();
        public List<ColumnInfo> DestinationColumns { get; set; } = new();

        private TableMetadata _currentTable = null!;
        private List<ColumnInfo> _sourceColumns = new();
        private bool _isLoading = false;

        public ColumnMappingPage()
        {
            InitializeComponent();
            DataContext = this;
        }

        public ColumnMappingPage(TableMetadata table) : this()
        {
            _currentTable = table;
            TableNameText.Text = $"Table: {table.Schema}.{table.Name}";
            Loaded += async (s, e) =>
            {
                RefreshPathLabel();
                await LoadSchemaAndMappingsAsync();
            };
        }

        private async Task LoadSchemaAndMappingsAsync()
        {
            _isLoading = true;
            try
            {
                // Load source and destination schemas
                _sourceColumns = await SchemaComparer.GetTableColumnsAsync(
                    ConnectionsService.SourceBuilder!.ConnectionString,
                    _currentTable.Schema,
                    _currentTable.Name);

                DestinationColumns = await SchemaComparer.GetTableColumnsAsync(
                    ConnectionsService.DestBuilder!.ConnectionString,
                    _currentTable.Schema,
                    _currentTable.Name);

                // Add "(Not Mapped)" option
                DestinationColumns.Insert(0, new ColumnInfo { ColumnName = "(Not Mapped)", DataType = "" });

                // Try to load existing mapping
                var existingMapping = await MappingConfiguration.LoadMappingAsync(
                    ConnectionsService.SourceBuilder.ConnectionString,
                    ConnectionsService.DestBuilder.ConnectionString,
                    _currentTable.Schema,
                    _currentTable.Name);

                if (existingMapping != null)
                {
                    // Re-resolve DestinationColumn references to match the live DestinationColumns list.
                    // Deserialized instances are new objects, so the ComboBox won't find them by reference
                    // unless we swap them for the actual items in its ItemsSource.
                    Mappings.Clear();
                    foreach (var mapping in existingMapping.Mappings)
                    {
                        if (mapping.DestinationColumn != null)
                        {
                            mapping.DestinationColumn = DestinationColumns
                                .FirstOrDefault(d => d.ColumnName == mapping.DestinationColumn.ColumnName)
                                ?? null;
                        }
                        Mappings.Add(mapping);
                    }
                }
                else
                {
                    // Start with all columns unmapped
                    Mappings.Clear();
                    foreach (var col in _sourceColumns)
                    {
                        Mappings.Add(new ColumnMapping
                        {
                            SourceColumn = col,
                            DestinationColumn = null,
                            Status = MappingStatus.Unmapped
                        });
                    }
                }

                UpdateSummary();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading schema: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Defer reset so WPF's queued SelectionChanged events (fired during binding
                // at Normal priority) are suppressed while _isLoading is still true.
                _ = Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Background,
                    new Action(() => _isLoading = false));
            }
        }

        private void AutoMap_Click(object sender, RoutedEventArgs e)
        {
            AutoMapColumns();
        }

        private void AutoMapColumns()
        {
            _isLoading = true;
            try
            {
                var destColumnsWithoutNotMapped = DestinationColumns
                    .Where(c => c.ColumnName != "(Not Mapped)")
                    .ToList();

                // Only auto-map columns that are currently unmapped — preserve Manual/AutoMapped/Incompatible
                foreach (var mapping in Mappings.Where(m => m.Status == MappingStatus.Unmapped))
                {
                    if (mapping.SourceColumn == null) continue;

                    // Try exact name match (case-insensitive)
                    var destCol = destColumnsWithoutNotMapped.FirstOrDefault(d =>
                        d.ColumnName.Equals(mapping.SourceColumn.ColumnName, StringComparison.OrdinalIgnoreCase));

                    if (destCol != null)
                    {
                        mapping.DestinationColumn = destCol;
                        mapping.Status = mapping.SourceColumn.IsCompatibleWith(destCol)
                            ? MappingStatus.AutoMapped
                            : MappingStatus.Incompatible;

                        if (mapping.Status == MappingStatus.Incompatible)
                            mapping.ValidationMessage = $"Incompatible types: {mapping.SourceColumn.DataType} → {destCol.DataType}";
                    }
                }

                MappingGrid.Items.Refresh();
            }
            finally
            {
                // Defer reset so WPF's queued SelectionChanged events are suppressed
                // before the flag clears (they fire at Normal priority; Background is lower).
                _ = Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Background,
                    new Action(() =>
                    {
                        _isLoading = false;
                        UpdateSummary();
                    }));
            }
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var mapping in Mappings)
            {
                mapping.DestinationColumn = null;
                mapping.Status = MappingStatus.Unmapped;
            }

            MappingGrid.Items.Refresh();
            UpdateSummary();
        }

        private async void SaveMapping_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var config = new TableMappingConfiguration
                {
                    SourceConnectionString = ConnectionsService.SourceBuilder!.ConnectionString,
                    DestinationConnectionString = ConnectionsService.DestBuilder!.ConnectionString,
                    TableSchema = _currentTable.Schema,
                    TableName = _currentTable.Name,
                    Mappings = Mappings.ToList()
                };

                var (isValid, errors) = SchemaComparer.ValidateMapping(config);

                if (!isValid)
                {
                    var errorMessage = string.Join("\n", errors);
                    var result = MessageBox.Show(
                        $"Validation issues found:\n\n{errorMessage}\n\nSave anyway?",
                        "Validation Warning",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.No)
                        return;
                }

                await MappingConfiguration.SaveMappingAsync(config);

                MessageBox.Show("Mapping saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                NavigationService?.GoBack();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving mapping: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.GoBack();
        }

        private void ChangePath_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select folder for mapping files",
                SelectedPath = MappingConfiguration.MappingDirectory,
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                MappingConfiguration.MappingDirectory = dialog.SelectedPath;
                MappingConfiguration.SavePathPreference();
                RefreshPathLabel();
            }
        }

        private void ResetPath_Click(object sender, RoutedEventArgs e)
        {
            MappingConfiguration.ResetToDefaultDirectory();
            MappingConfiguration.SavePathPreference();
            RefreshPathLabel();
        }

        private void RefreshPathLabel()
        {
            MappingPathText.Text = MappingConfiguration.MappingDirectory;
        }

        private void DestColumnCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;

            if (sender is ComboBox combo && combo.DataContext is ColumnMapping mapping)
            {
                if (mapping.DestinationColumn?.ColumnName == "(Not Mapped)")
                {
                    mapping.DestinationColumn = null;
                    mapping.Status = MappingStatus.Unmapped;
                }
                else if (mapping.DestinationColumn != null && mapping.SourceColumn != null)
                {
                    if (mapping.SourceColumn.IsCompatibleWith(mapping.DestinationColumn))
                    {
                        mapping.Status = MappingStatus.ManualMapped;
                        mapping.ValidationMessage = null;
                    }
                    else
                    {
                        mapping.Status = MappingStatus.Incompatible;
                        mapping.ValidationMessage = $"Incompatible types: {mapping.SourceColumn.DataType} → {mapping.DestinationColumn.DataType}";
                    }
                }

                UpdateSummary();
            }
        }

        private void UpdateSummary()
        {
            if (Mappings.Count == 0)
                return;

            var summary = SchemaComparer.GetMappingSummary(Mappings.ToList());
            SummaryText.Text = summary;
            SummaryBorder.Visibility = Visibility.Visible;

            // Show validation errors
            var incompatible = Mappings.Where(m => m.Status == MappingStatus.Incompatible).ToList();
            if (incompatible.Any())
            {
                var messages = string.Join("\n", incompatible.Select(m => $"• {m.SourceColumn?.ColumnName}: {m.ValidationMessage}"));
                ValidationText.Text = messages;
                ValidationBorder.Visibility = Visibility.Visible;
            }
            else
            {
                ValidationBorder.Visibility = Visibility.Collapsed;
            }
        }
    }
}
