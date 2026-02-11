using System;

namespace MigrationTool.Shared
{
    /// <summary>
    /// Represents metadata for a database column
    /// </summary>
    public class ColumnInfo
    {
        public string ColumnName { get; set; } = string.Empty;
        public int OrdinalPosition { get; set; }
        public string DataType { get; set; } = string.Empty;
        public int? MaxLength { get; set; }
        public int? NumericPrecision { get; set; }
        public int? NumericScale { get; set; }
        public bool IsNullable { get; set; }
        public string? DefaultValue { get; set; }
        public bool IsIdentity { get; set; }

        /// <summary>
        /// Gets a display-friendly representation of the column
        /// </summary>
        public string DisplayName => $"{ColumnName} ({GetDataTypeDisplay()})";

        /// <summary>
        /// Gets a formatted data type string with length/precision
        /// </summary>
        private string GetDataTypeDisplay()
        {
            if (MaxLength.HasValue && MaxLength.Value > 0 && MaxLength.Value != int.MaxValue)
            {
                return $"{DataType}({MaxLength})";
            }
            else if (NumericPrecision.HasValue && NumericScale.HasValue)
            {
                return $"{DataType}({NumericPrecision},{NumericScale})";
            }
            else if (MaxLength == -1)
            {
                return $"{DataType}(MAX)";
            }
            return DataType;
        }

        /// <summary>
        /// Checks if this column is compatible with another column for data migration
        /// </summary>
        public bool IsCompatibleWith(ColumnInfo other)
        {
            // Exact match
            if (DataType.Equals(other.DataType, StringComparison.OrdinalIgnoreCase))
            {
                // Check size constraints
                if (MaxLength.HasValue && other.MaxLength.HasValue)
                {
                    return other.MaxLength >= MaxLength; // Destination must be equal or larger
                }
                return true;
            }

            // Compatible type mappings
            return IsCompatibleDataType(DataType, other.DataType);
        }

        /// <summary>
        /// Checks if two data types are compatible for migration
        /// </summary>
        private static bool IsCompatibleDataType(string sourceType, string destType)
        {
            sourceType = sourceType.ToLower();
            destType = destType.ToLower();

            // String types
            if (IsStringType(sourceType) && IsStringType(destType))
                return true;

            // Numeric types - allow widening conversions
            if (IsNumericType(sourceType) && IsNumericType(destType))
            {
                return GetNumericTypeRank(destType) >= GetNumericTypeRank(sourceType);
            }

            // Date/Time types
            if (IsDateTimeType(sourceType) && IsDateTimeType(destType))
                return true;

            return false;
        }

        private static bool IsStringType(string type)
        {
            return type.Contains("char") || type.Contains("text");
        }

        private static bool IsNumericType(string type)
        {
            return type == "tinyint" || type == "smallint" || type == "int" || 
                   type == "bigint" || type == "decimal" || type == "numeric" || 
                   type == "float" || type == "real" || type == "money" || type == "smallmoney";
        }

        private static bool IsDateTimeType(string type)
        {
            return type.Contains("date") || type.Contains("time");
        }

        private static int GetNumericTypeRank(string type)
        {
            return type switch
            {
                "tinyint" => 1,
                "smallint" => 2,
                "int" => 3,
                "bigint" => 4,
                "decimal" => 5,
                "numeric" => 5,
                "float" => 6,
                "real" => 4,
                "money" => 5,
                "smallmoney" => 4,
                _ => 0
            };
        }
    }
}
