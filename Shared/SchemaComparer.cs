using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace MigrationTool.Shared
{
    /// <summary>
    /// Service for comparing database schemas and creating column mappings
    /// </summary>
    public static class SchemaComparer
    {
        /// <summary>
        /// Retrieves column metadata for a specific table
        /// </summary>
        public static async Task<List<ColumnInfo>> GetTableColumnsAsync(string connectionString, string schema, string tableName)
        {
            var columns = new List<ColumnInfo>();

            string query = @"
                SELECT 
                    c.COLUMN_NAME,
                    c.ORDINAL_POSITION,
                    c.DATA_TYPE,
                    c.CHARACTER_MAXIMUM_LENGTH,
                    c.NUMERIC_PRECISION,
                    c.NUMERIC_SCALE,
                    c.IS_NULLABLE,
                    c.COLUMN_DEFAULT,
                    COLUMNPROPERTY(OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME), c.COLUMN_NAME, 'IsIdentity') AS IS_IDENTITY
                FROM INFORMATION_SCHEMA.COLUMNS c
                WHERE c.TABLE_SCHEMA = @Schema 
                  AND c.TABLE_NAME = @TableName
                ORDER BY c.ORDINAL_POSITION";

            using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@Schema", schema);
            cmd.Parameters.AddWithValue("@TableName", tableName);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(new ColumnInfo
                {
                    ColumnName = reader.GetString(0),
                    OrdinalPosition = reader.GetInt32(1),
                    DataType = reader.GetString(2),
                    MaxLength = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                    NumericPrecision = reader.IsDBNull(4) ? null : Convert.ToInt32(reader.GetByte(4)),
                    NumericScale = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                    IsNullable = reader.GetString(6) == "YES",
                    DefaultValue = reader.IsDBNull(7) ? null : reader.GetString(7),
                    IsIdentity = reader.IsDBNull(8) ? false : reader.GetInt32(8) == 1
                });
            }

            return columns;
        }

        /// <summary>
        /// Automatically maps columns between source and destination tables
        /// </summary>
        public static List<ColumnMapping> AutoMapColumns(List<ColumnInfo> sourceColumns, List<ColumnInfo> destColumns)
        {
            var mappings = new List<ColumnMapping>();

            foreach (var sourceCol in sourceColumns)
            {
                var mapping = new ColumnMapping
                {
                    SourceColumn = sourceCol,
                    DestinationColumn = null,
                    Status = MappingStatus.Unmapped
                };

                // Try exact name match (case-insensitive)
                var destCol = destColumns.FirstOrDefault(d => 
                    d.ColumnName.Equals(sourceCol.ColumnName, StringComparison.OrdinalIgnoreCase));

                if (destCol != null)
                {
                    if (sourceCol.IsCompatibleWith(destCol))
                    {
                        mapping.DestinationColumn = destCol;
                        mapping.Status = MappingStatus.AutoMapped;
                    }
                    else
                    {
                        mapping.DestinationColumn = destCol;
                        mapping.Status = MappingStatus.Incompatible;
                        mapping.ValidationMessage = $"Incompatible types: {sourceCol.DataType} → {destCol.DataType}";
                    }
                }
                else
                {
                    // Try fuzzy match (remove underscores, spaces, case-insensitive)
                    var normalizedSourceName = NormalizeName(sourceCol.ColumnName);
                    destCol = destColumns.FirstOrDefault(d => 
                        NormalizeName(d.ColumnName) == normalizedSourceName);

                    if (destCol != null && sourceCol.IsCompatibleWith(destCol))
                    {
                        mapping.DestinationColumn = destCol;
                        mapping.Status = MappingStatus.AutoMapped;
                    }
                }

                mappings.Add(mapping);
            }

            return mappings;
        }

        /// <summary>
        /// Validates a complete table mapping configuration
        /// </summary>
        public static (bool IsValid, List<string> Errors) ValidateMapping(TableMappingConfiguration config)
        {
            var errors = new List<string>();

            // Check for incompatible mappings
            var incompatible = config.Mappings.Where(m => m.Status == MappingStatus.Incompatible).ToList();
            if (incompatible.Any())
            {
                errors.Add($"{incompatible.Count} incompatible column mapping(s) found");
            }

            // Check for duplicate destination columns
            var duplicates = config.Mappings
                .Where(m => m.DestinationColumn != null)
                .GroupBy(m => m.DestinationColumn!.ColumnName)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);

            foreach (var dup in duplicates)
            {
                errors.Add($"Destination column '{dup}' is mapped multiple times");
            }

            // Warn about unmapped source columns
            var unmapped = config.Mappings.Where(m => m.Status == MappingStatus.Unmapped).ToList();
            if (unmapped.Any())
            {
                errors.Add($"Warning: {unmapped.Count} source column(s) not mapped (will be skipped)");
            }

            return (errors.Count == 0 || errors.All(e => e.StartsWith("Warning")), errors);
        }

        /// <summary>
        /// Normalizes a column name for fuzzy matching
        /// </summary>
        private static string NormalizeName(string name)
        {
            return name.Replace("_", "").Replace(" ", "").ToLowerInvariant();
        }

        /// <summary>
        /// Gets a summary of the mapping status
        /// </summary>
        public static string GetMappingSummary(List<ColumnMapping> mappings)
        {
            var autoMapped = mappings.Count(m => m.Status == MappingStatus.AutoMapped);
            var manualMapped = mappings.Count(m => m.Status == MappingStatus.ManualMapped);
            var unmapped = mappings.Count(m => m.Status == MappingStatus.Unmapped);
            var incompatible = mappings.Count(m => m.Status == MappingStatus.Incompatible);

            return $"Mapped: {autoMapped + manualMapped}/{mappings.Count} | Unmapped: {unmapped} | Incompatible: {incompatible}";
        }
    }
}
