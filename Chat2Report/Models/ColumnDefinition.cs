// ColumnDefinition.cs
using Microsoft.Extensions.VectorData;
using System;
using System.Text;

namespace Chat2Report.Models
{
    [Flags]
    public enum KeyType
    {
        None = 0,
        Primary = 1,
        Foreign = 2,
        Composite = 4
    }


    /// <summary>
    /// Represents the definition of a database column.
    /// </summary>
    public class ColumnDefinition
    {

        [VectorStoreRecordKey] public ulong Id { get; set; }

        /// <summary>
        /// Descriptive name of the column, often in a local language or a more user-friendly format.
        /// Example: "Име_на_производ" for a column named "ProductName".
        /// </summary>
        public string DescriptiveName { get; set; }

        /// <summary>
        /// The actual name of the column in the database.
        /// Example: "ProductName".
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The data Type of the column.
        /// Example: "nvarchar(100)", "int", "datetime".
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// The fully qualified name of the table this column belongs to (e.g., "dbo.Products").
        /// </summary>
        public string FullQualifiedTableName { get; set; } = string.Empty;

        /// <summary>
        /// A description of the column's purpose or content.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Indicates if this column is part of the primary key.
        /// </summary>
        public bool IsPrimaryKey { get; set; }

        public KeyType KeyType { get; set; } = KeyType.None; // Default to None, can be Primary, Foreign, Composite, or a combination

        /// <summary>
        /// Indicates if this column allows NULL values.
        /// </summary>
        public bool IsNullable { get; set; }

        /// <summary>
        /// Indicates if this column is an identity column (auto-incrementing).
        /// </summary>
        public bool IsAutoIncrement { get; set; }

        /// <summary>
        /// The default value of the column, if any.
        /// </summary>
        public string DefaultValue { get; set; }




        /// <summary>
        /// 
        /// Indicates if colum is a code/option/enum value
        /// flag is defined in MSSQL column extended property 'Embed'
        /// </summary>
        public bool IsEnumLikeColumn { get; set; } 


        [VectorStoreRecordVector]
        public ReadOnlyMemory<float> Embedding { get; set; } // Vector representation


        public override bool Equals(object obj)
        {
            if (obj is ColumnDefinition other)
            {
                return FullQualifiedTableName == other.FullQualifiedTableName && Name == other.Name;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(FullQualifiedTableName, Name);
        }


        /// <summary>
        /// Provides an SLM-friendly string representation of the column definition.
        /// Helpful for generating SQL or understanding column properties.
        /// </summary>
        /// <returns>A descriptive string about the column.</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append($"Column '{Name}' in table '{FullQualifiedTableName}' ");
            sb.Append($"is of type '{Type}'. ");
            if (!string.IsNullOrWhiteSpace(DescriptiveName))
            {
                sb.Append($"Its descriptive name is '{DescriptiveName}'. ");
            }
            if (!string.IsNullOrWhiteSpace(Description))
            {
                sb.Append($"Description: '{Description}'. ");
            }
            if (IsPrimaryKey)
            {
                sb.Append("It IS a PRIMARY KEY. ");
            }
            sb.Append(IsNullable ? "It ALLOWS NULL values. " : "It DOES NOT ALLOW NULL values. ");
            if (IsAutoIncrement)
            {
                sb.Append("It IS an AUTO-INCREMENT/IDENTITY column. ");
            }

            if (IsEnumLikeColumn) {
                sb.Append("It is an ENUM/OPTION/CODE value. ");
            }
            if (!string.IsNullOrWhiteSpace(DefaultValue))
            {
                sb.Append($"It has a DEFAULT VALUE of '{DefaultValue}'. ");
            }
            return sb.ToString().Trim();
        }
    }
}
