using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MigrationTool.Shared
{
    /// <summary>
    /// Persistent application settings stored as app_settings.json.
    /// The file's location is itself configurable and tracked via a pointer file.
    /// </summary>
    public class AppSettingsData
    {
        // ── Connection ────────────────────────────────────────────────────────
        public string SourceServer { get; set; } = string.Empty;
        public string SourceDatabase { get; set; } = string.Empty;
        public string SourceUser { get; set; } = string.Empty;
        public bool SourceUseWindowsAuth { get; set; } = true;

        public string DestServer { get; set; } = string.Empty;
        public string DestDatabase { get; set; } = string.Empty;
        public string DestUser { get; set; } = string.Empty;
        public bool DestUseWindowsAuth { get; set; } = false;

        // ── Mapping files path ────────────────────────────────────────────────
        public string MappingDirectory { get; set; } = string.Empty;
    }

    public static class AppSettings
    {
        // ── Fixed locations ───────────────────────────────────────────────────

        /// <summary>The AppData subfolder — always fixed.</summary>
        public static readonly string AppDataDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MigrationTool");

        /// <summary>
        /// Tiny pointer file (fixed location) that records where app_settings.json lives.
        /// If absent, the default path is used.
        /// </summary>
        private static readonly string PointerFilePath =
            Path.Combine(AppDataDir, "settings_location.txt");

        /// <summary>Default path for app_settings.json.</summary>
        public static readonly string DefaultSettingsFilePath =
            Path.Combine(AppDataDir, "app_settings.json");

        // ── Dynamic settings path ─────────────────────────────────────────────

        /// <summary>
        /// Current path of app_settings.json (may differ from default if user changed it).
        /// </summary>
        public static string SettingsFilePath
        {
            get
            {
                try
                {
                    if (File.Exists(PointerFilePath))
                    {
                        var path = File.ReadAllText(PointerFilePath).Trim();
                        if (!string.IsNullOrEmpty(path)) return path;
                    }
                }
                catch { /* fall through */ }
                return DefaultSettingsFilePath;
            }
        }

        // ── JSON options ──────────────────────────────────────────────────────

        private static readonly JsonSerializerOptions _options = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private static AppSettingsData? _cache;

        // ── Public API ────────────────────────────────────────────────────────

        public static AppSettingsData Load()
        {
            try
            {
                var path = SettingsFilePath;
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    _cache = JsonSerializer.Deserialize<AppSettingsData>(json, _options) ?? new();
                    return _cache;
                }

                // Migrate from legacy INI file if it exists
                var legacyPath = Path.Combine(AppDataDir, "connection_settings.ini");
                if (File.Exists(legacyPath))
                {
                    _cache = MigrateFromIni(legacyPath);
                    Save(_cache);
                    return _cache;
                }
            }
            catch { /* fall through to defaults */ }

            _cache = new AppSettingsData();
            return _cache;
        }

        public static void Save(AppSettingsData settings)
        {
            try
            {
                var path = SettingsFilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var json = JsonSerializer.Serialize(settings, _options);
                File.WriteAllText(path, json);
                _cache = settings;
            }
            catch { /* non-critical */ }
        }

        /// <summary>
        /// Changes where app_settings.json is stored.
        /// Writes the file to the new location and updates the pointer.
        /// </summary>
        public static void ChangeSettingsPath(string newDirectory)
        {
            try
            {
                Directory.CreateDirectory(AppDataDir);
                var newFilePath = Path.Combine(newDirectory, "app_settings.json");

                // Write pointer
                File.WriteAllText(PointerFilePath, newFilePath);

                // Save current settings to new location
                var s = _cache ?? Load();
                Directory.CreateDirectory(newDirectory);
                var json = JsonSerializer.Serialize(s, _options);
                File.WriteAllText(newFilePath, json);
                _cache = s;
            }
            catch { /* non-critical */ }
        }

        /// <summary>
        /// Resets the settings path to the default AppData location.
        /// </summary>
        public static void ResetSettingsPath()
        {
            try
            {
                if (File.Exists(PointerFilePath))
                    File.Delete(PointerFilePath);

                // Save to default
                var s = _cache ?? Load();
                Directory.CreateDirectory(AppDataDir);
                var json = JsonSerializer.Serialize(s, _options);
                File.WriteAllText(DefaultSettingsFilePath, json);
                _cache = s;
            }
            catch { /* non-critical */ }
        }

        /// <summary>
        /// Saves only the MappingDirectory field.
        /// </summary>
        public static void SaveMappingDirectory(string directory)
        {
            var s = _cache ?? Load();
            s.MappingDirectory = directory;
            Save(s);
        }

        // ── Migration from legacy INI ─────────────────────────────────────────

        private static AppSettingsData MigrateFromIni(string path)
        {
            var d = new AppSettingsData();
            foreach (var line in File.ReadAllLines(path))
            {
                if (!line.Contains('=')) continue;
                var parts = line.Split(new[] { '=' }, 2);
                var key = parts[0].Trim();
                var val = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                switch (key)
                {
                    case "SourceServer":         d.SourceServer         = val; break;
                    case "SourceDatabase":       d.SourceDatabase       = val; break;
                    case "SourceUser":           d.SourceUser           = val; break;
                    case "SourceUseWindowsAuth": bool.TryParse(val, out var sa); d.SourceUseWindowsAuth = sa; break;
                    case "DestServer":           d.DestServer           = val; break;
                    case "DestDatabase":         d.DestDatabase         = val; break;
                    case "DestUser":             d.DestUser             = val; break;
                    case "DestUseWindowsAuth":   bool.TryParse(val, out var da); d.DestUseWindowsAuth   = da; break;
                    case "MappingDirectory":     d.MappingDirectory     = val; break;
                }
            }
            return d;
        }
    }
}
