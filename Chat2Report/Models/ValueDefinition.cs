// USearchVectorDataExample/Models/SLMQuery/ValueDefinition.cs
// ... (other parts of ValueDefinition) ...
using Microsoft.Extensions.VectorData;



namespace Chat2Report.Models
{

    public class ValueDefinition
    {


        [VectorStoreRecordKey]
        public ulong Id { get; set; }

        public string SourceColumnFullQualifiedName { get; set; } = string.Empty;
        public string SourceColumnType { get; set; } = string.Empty;


        /// <summary>
        /// Информации за специфичен клуч (примарен или странски) поврзан со оваа вредност.
        /// </summary>
        public record KeyInfo(string ColumnName, string Value, KeyType KeyType);

        /// <summary>
        /// Содржи информации за сите примарни и странски клучеви за редот од кој потекнува оваа вредност.
        /// </summary>
        public List<KeyInfo> KeysWithTypeColumnNamePairs { get; set; } = new List<KeyInfo>();

        [VectorStoreRecordData(IsFilterable = true)]
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// Stringified value of the column.
        /// if value is string or date has '<value>' 
        /// </summary>
        public string ValueStringified { get; set; } = string.Empty;

        public bool IsRequired { get; set; }
        public bool IsArray { get; set; }
        public bool IsNullable { get; set; }
        public bool IsReference { get; set; }
        public bool IsEnum { get; set; }
        public bool IsComplexType { get; set; }

        [VectorStoreRecordVector]
        public ReadOnlyMemory<float> Embedding { get; set; }


        public string ConditionHint
        {
            get
            {
                if (KeysWithTypeColumnNamePairs != null && KeysWithTypeColumnNamePairs.Count > 0)
                {
                    return string.Join(", ", KeysWithTypeColumnNamePairs
                        //.OrderBy(pair => pair.ColumnName)
                        .Select(pair => $"{pair.ColumnName}={pair.Value}"));
                }
                else
                {
                    return $"{SourceColumnFullQualifiedName}={ValueStringified}";
                }
            }
        }



        /// <summary>
        /// Returns a string representation of the ValueDefinition.
        /// 
        /// </summary>
        public override string ToString()
        {
            //print most important properties
            return $"ValueDefinition(Id: {Id}, SourceColumn: {SourceColumnFullQualifiedName}, " +
                   $"Value: {ValueStringified}, IsRequired: {IsRequired}, IsArray: {IsArray}, " +
                   $"IsNullable: {IsNullable}, IsReference: {IsReference}, IsEnum: {IsEnum}, " +
                   $"IsComplexType: {IsComplexType}, Tags: [{string.Join(", ", Tags)}])";
        }
    }
}