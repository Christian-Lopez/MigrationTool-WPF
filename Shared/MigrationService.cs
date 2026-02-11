using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace MigrationTool.Shared
{
    /// <summary>
    /// Service for executing data migrations with column mapping support
    /// </summary>
    public static class MigrationService
    {
        /// <summary>
        /// Migrates data from source to destination using column mappings
        /// </summary>
        public static async Task MigrateWithMappingAsync(
            string sourceConnectionString,
            string destConnectionString,
            TableMappingConfiguration mappingConfig,
            string? whereClause = null,
            int rowLimit = 0,
            Action<long>? progressCallback = null)
        {
            using var sourceConn = new SqlConnection(sourceConnectionString);
            using var destConn = new SqlConnection(destConnectionString);

            await sourceConn.OpenAsync();
            await destConn.OpenAsync();

            // Build SELECT query with only mapped columns
            var mappedColumns = mappingConfig.Mappings
                .Where(m => m.IsValid && m.DestinationColumn != null)
                .ToList();

            if (!mappedColumns.Any())
            {
                throw new InvalidOperationException("No valid column mappings found");
            }

            var sourceColumnList = string.Join(", ", mappedColumns.Select(m => $"[{m.SourceColumn!.ColumnName}]"));
            
            string topClause = rowLimit > 0 ? $"TOP ({rowLimit})" : "";
            string whereFilter = !string.IsNullOrWhiteSpace(whereClause) ? $"WHERE {whereClause}" : "";
            
            string selectQuery = $"SELECT {topClause} {sourceColumnList} FROM {mappingConfig.FullTableName} {whereFilter}";

            // Execute query
            using var cmd = new SqlCommand(selectQuery, sourceConn);
            using var reader = await cmd.ExecuteReaderAsync();

            // Setup SqlBulkCopy with column mappings
            using var bulkCopy = new SqlBulkCopy(destConn, SqlBulkCopyOptions.KeepIdentity, null);
            bulkCopy.DestinationTableName = mappingConfig.FullTableName;
            bulkCopy.BatchSize = 1000;
            bulkCopy.BulkCopyTimeout = 300; // 5 minutes

            // Add column mappings
            foreach (var mapping in mappedColumns)
            {
                bulkCopy.ColumnMappings.Add(
                    mapping.SourceColumn!.ColumnName,
                    mapping.DestinationColumn!.ColumnName
                );
            }

            // Setup progress notification
            if (progressCallback != null)
            {
                bulkCopy.NotifyAfter = 100;
                bulkCopy.SqlRowsCopied += (s, e) => progressCallback(e.RowsCopied);
            }

            // Execute bulk copy
            await bulkCopy.WriteToServerAsync(reader);
        }

        /// <summary>
        /// Gets the total row count for a migration
        /// </summary>
        public static async Task<int> GetRowCountAsync(
            string connectionString,
            string schema,
            string tableName,
            string? whereClause = null,
            int rowLimit = 0)
        {
            using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            string fullTableName = $"[{schema}].[{tableName}]";
            string whereFilter = !string.IsNullOrWhiteSpace(whereClause) ? $"WHERE {whereClause}" : "";

            string countQuery = rowLimit > 0
                ? $"SELECT COUNT(*) FROM (SELECT TOP ({rowLimit}) * FROM {fullTableName} {whereFilter}) AS T"
                : $"SELECT COUNT(*) FROM {fullTableName} {whereFilter}";

            using var cmd = new SqlCommand(countQuery, conn);
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
    }
}
