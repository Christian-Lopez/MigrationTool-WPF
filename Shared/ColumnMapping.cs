using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MigrationTool.Shared
{
    /// <summary>
    /// Represents a mapping between source and destination columns
    /// </summary>
    public class ColumnMapping
    {
        public ColumnInfo? SourceColumn { get; set; }
        public ColumnInfo? DestinationColumn { get; set; }
        public MappingStatus Status { get; set; }
        public string? ValidationMessage { get; set; }

        public bool IsValid => Status != MappingStatus.Incompatible && DestinationColumn != null;

        public string StatusDisplay => Status switch
        {
            MappingStatus.AutoMapped => "✓ Auto-mapped",
            MappingStatus.ManualMapped => "✓ Manual",
            MappingStatus.Unmapped => "⚠ Not mapped",
            MappingStatus.Incompatible => "❌ Incompatible",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Status of a column mapping
    /// </summary>
    public enum MappingStatus
    {
        AutoMapped,
        ManualMapped,
        Unmapped,
        Incompatible
    }

    /// <summary>
    /// Configuration for table column mappings
    /// </summary>
    public class TableMappingConfiguration
    {
        public string SourceConnectionString { get; set; } = string.Empty;
        public string DestinationConnectionString { get; set; } = string.Empty;
        public string TableSchema { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public List<ColumnMapping> Mappings { get; set; } = new();
        public DateTime CreatedDate { get; set; }
        public DateTime LastModifiedDate { get; set; }
        /// <summary>
        /// The directory this mapping file was saved to. Informational only.
        /// </summary>
        public string MappingDirectory { get; set; } = string.Empty;

        public string FullTableName => $"[{TableSchema}].[{TableName}]";

        /// <summary>
        /// Gets the unique identifier for this mapping configuration
        /// </summary>
        public string GetMappingKey()
        {
            var sourceHash = GetConnectionHash(SourceConnectionString);
            var destHash = GetConnectionHash(DestinationConnectionString);
            return $"{sourceHash}_{destHash}_{TableSchema}_{TableName}";
        }

        private static string GetConnectionHash(string connectionString)
        {
            // Build a stable filename segment from server + database.
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
            var dataSource = builder.DataSource;

            // LocalDB resolves to a named pipe like "np:////pipe/LOCALDB#7664B7F1/tsql/query".
            // The instance ID (after #) is randomly assigned on every restart, so we must
            // normalize any LocalDB/pipe connection to a stable token.
            if (dataSource.StartsWith("np:", StringComparison.OrdinalIgnoreCase) ||
                dataSource.IndexOf("LOCALDB", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                dataSource = "localdb";
            }

            var raw = $"{dataSource}_{builder.InitialCatalog}";

            // Replace every character that is illegal in a Windows filename with '_'
            var invalidChars = Path.GetInvalidFileNameChars();
            return new string(raw.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
        }
    }
}
