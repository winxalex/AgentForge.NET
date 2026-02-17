// TableRelationship.cs
using System.Text;

namespace Chat2Report.Models
{
    /// <summary>
    /// Defines the type of a database relationship (e.g., One-to-One, One-to-Many).
    /// </summary>
    public enum RelationshipType
    {
        Unknown,
        OneToOne,
        OneToMany, // Represents a relationship where the 'FromTable' has many records pointing to one in the 'ToTable'.
        ManyToMany // Represents a conceptual relationship between two tables, typically implemented via a junction table.
        
    }

    /// <summary>
    /// Represents a pair of joining columns in a foreign key relationship.
    /// </summary>
    public record KeyColumnPair(string FromColumn, string ToColumn);

    /// <summary>
    /// Represents a relationship between two tables in a database.
    /// </summary>
    public class TableRelationship
    {
        /// <summary>
        /// The fully qualified name of the table where the foreign key resides (the "many" side in a one-to-many).
        /// Example: "dbo.OrderItems".
        /// </summary>
        public string FromTable { get; set; } = string.Empty;

        /// <summary>
        /// The fully qualified name of the table referenced by the foreign key (the "one" side in a one-to-many).
        /// Example: "dbo.Products".
        /// </summary>
        public string ToTable { get; set; } = string.Empty;

        /// <summary>
        /// A list of column pairs that define the join for this relationship.
        /// This supports composite keys.
        /// </summary>
        public List<KeyColumnPair> KeyColumns { get; set; } = new List<KeyColumnPair>();

        /// <summary>
        /// The type of relationship (e.g., One-to-One, One-to-Many).
        /// </summary>
        public RelationshipType RelationType { get; set; }

        /// <summary>
        /// The name of the foreign key constraint, if available.
        /// </summary>
        public string ForeignKeyName { get; set; }


        /// <summary>
        /// Provides an SLM-friendly string representation of the relationship.
        /// Helpful for generating SQL or understanding table connections.
        /// </summary>
        /// <returns>A descriptive string about the relationship.</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            string relationDescription = RelationType switch
            {
                RelationshipType.OneToMany => $"(Many) to '{ToTable}' (One)",
                RelationshipType.OneToOne => $"(One) to '{ToTable}' (One)",
                RelationshipType.ManyToMany => $"(Many) to '{ToTable}' (Many)",
                _ => $"to '{ToTable}'"
            };

            sb.Append($"Table '{FromTable}' has a {RelationType} relationship {relationDescription}. ");

            if (KeyColumns != null && KeyColumns.Any())
            {
                var keyDetails = string.Join(", ", KeyColumns.Select(p => $"({p.FromColumn} -> {p.ToColumn})"));
                sb.Append($"The relationship is on key(s): {keyDetails}. ");
            }

            if (!string.IsNullOrWhiteSpace(ForeignKeyName))
            {
                sb.Append($"Constraint name: '{ForeignKeyName}'.");
            }
            return sb.ToString().Trim();
        }

        /// <summary>
        /// Provides a concise, SQL-like hint for how to join the tables.
        /// Handles composite keys correctly.
        /// </summary>
        /// <returns>A string hint for the ON clause of a JOIN statement.</returns>
        public string ToSqlJoinHintString()
        {
            if (KeyColumns == null || !KeyColumns.Any())
            {
                // Fallback for malformed data
                return $"-- Malformed relationship: No key columns defined for relationship between {FromTable} and {ToTable}";
            }

            var joinConditions = KeyColumns
                .Select(pair => $"{FromTable}.[{pair.FromColumn}] = {ToTable}.[{pair.ToColumn}]")
                .ToList();

            return string.Join(" AND ", joinConditions);
        }
    }
}
