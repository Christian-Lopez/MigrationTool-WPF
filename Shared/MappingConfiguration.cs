using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace MigrationTool.Shared
{
    /// <summary>
    /// Service for persisting and loading column mapping configurations
    /// </summary>
    public static class MappingConfiguration
    {
        private static string _mappingDirectory = GetDefaultMappingDirectory();

        /// <summary>
        /// Gets or sets the directory where mapping files are stored
        /// </summary>
        public static string MappingDirectory
        {
            get => _mappingDirectory;
            set
            {
                _mappingDirectory = value;
                Directory.CreateDirectory(_mappingDirectory);
            }
        }

        /// <summary>
        /// Gets the default mapping directory in AppData
        /// </summary>
        private static string GetDefaultMappingDirectory()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var path = Path.Combine(appData, "MigrationTool", "mappings");
            Directory.CreateDirectory(path);
            return path;
        }

        /// <summary>
        /// Saves a table mapping configuration to disk
        /// </summary>
        public static async Task SaveMappingAsync(TableMappingConfiguration config)
        {
            config.LastModifiedDate = DateTime.Now;
            if (config.CreatedDate == default)
            {
                config.CreatedDate = DateTime.Now;
            }

            var fileName = $"{config.GetMappingKey()}.json";
            var filePath = Path.Combine(MappingDirectory, fileName);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(config, options);
            await File.WriteAllTextAsync(filePath, json);
        }

        /// <summary>
        /// Loads a table mapping configuration from disk
        /// </summary>
        public static async Task<TableMappingConfiguration?> LoadMappingAsync(string sourceConnectionString, string destConnectionString, string schema, string tableName)
        {
            var tempConfig = new TableMappingConfiguration
            {
                SourceConnectionString = sourceConnectionString,
                DestinationConnectionString = destConnectionString,
                TableSchema = schema,
                TableName = tableName
            };

            var fileName = $"{tempConfig.GetMappingKey()}.json";
            var filePath = Path.Combine(MappingDirectory, fileName);

            if (!File.Exists(filePath))
            {
                return null;
            }

            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                return JsonSerializer.Deserialize<TableMappingConfiguration>(json, options);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Deletes a mapping configuration
        /// </summary>
        public static void DeleteMapping(string sourceConnectionString, string destConnectionString, string schema, string tableName)
        {
            var tempConfig = new TableMappingConfiguration
            {
                SourceConnectionString = sourceConnectionString,
                DestinationConnectionString = destConnectionString,
                TableSchema = schema,
                TableName = tableName
            };

            var fileName = $"{tempConfig.GetMappingKey()}.json";
            var filePath = Path.Combine(MappingDirectory, fileName);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        /// <summary>
        /// Gets all mapping files in the current directory
        /// </summary>
        public static List<string> GetAllMappingFiles()
        {
            if (!Directory.Exists(MappingDirectory))
            {
                return new List<string>();
            }

            return new List<string>(Directory.GetFiles(MappingDirectory, "*.json"));
        }

        /// <summary>
        /// Resets the mapping directory to the default AppData location
        /// </summary>
        public static void ResetToDefaultDirectory()
        {
            MappingDirectory = GetDefaultMappingDirectory();
        }

        // ── Persistence helpers ──────────────────────────────────────────────

        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MigrationTool",
            "connection_settings.ini");

        /// <summary>
        /// Saves the current MappingDirectory to the shared settings file.
        /// </summary>
        public static void SavePathPreference()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);

                var dict = ReadSettingsFile();
                dict["MappingDirectory"] = MappingDirectory;
                WriteSettingsFile(dict);
            }
            catch { /* non-critical */ }
        }

        /// <summary>
        /// Loads a previously saved MappingDirectory from the shared settings file.
        /// Falls back to the default if nothing is saved.
        /// </summary>
        public static void LoadPathPreference()
        {
            try
            {
                var dict = ReadSettingsFile();
                if (dict.TryGetValue("MappingDirectory", out var saved) && !string.IsNullOrWhiteSpace(saved))
                {
                    MappingDirectory = saved;
                }
            }
            catch { /* keep default */ }
        }

        private static Dictionary<string, string> ReadSettingsFile()
        {
            var dict = new Dictionary<string, string>();
            if (!File.Exists(SettingsFilePath)) return dict;

            foreach (var line in File.ReadAllLines(SettingsFilePath))
            {
                if (line.Contains('='))
                {
                    var parts = line.Split(new[] { '=' }, 2);
                    dict[parts[0].Trim()] = parts.Length > 1 ? parts[1].Trim() : "";
                }
            }
            return dict;
        }

        private static void WriteSettingsFile(Dictionary<string, string> dict)
        {
            var lines = new List<string>();
            foreach (var kvp in dict)
                lines.Add($"{kvp.Key}={kvp.Value}");
            File.WriteAllLines(SettingsFilePath, lines);
        }
    }
}
