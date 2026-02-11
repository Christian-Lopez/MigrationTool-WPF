using System;
using System.Collections.Generic;

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
            // Simple hash based on server and database
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
            return $"{builder.DataSource}_{builder.InitialCatalog}".Replace("\\", "_").Replace(".", "_");
        }
    }
}
