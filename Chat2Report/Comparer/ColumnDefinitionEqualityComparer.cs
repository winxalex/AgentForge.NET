

using Chat2Report.Models;

namespace Chat2Report.Comparer
{

    public class ColumnDefinitionEqualityComparer : IEqualityComparer<ColumnDefinition>
    {
        public bool Equals(ColumnDefinition x, ColumnDefinition y)
        {
            // Check if either x or y is null
            if (x == null && y == null) return true; // Both null, consider them equal
            if (x == null || y == null) return false; // Only one is null, they are not equal

            // Both are not null, compare their properties
            return x.FullQualifiedTableName == y.FullQualifiedTableName && x.Name == y.Name;
        }

        public int GetHashCode(ColumnDefinition obj)
        {
            return HashCode.Combine(obj.FullQualifiedTableName, obj.Name);
        }
    }
}