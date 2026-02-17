using Chat2Report.Models;
using Chat2Report.Options;
using Chat2Report.Utilities;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace Chat2Report.Services
{
    public class SQLiteDataSchemaService : IDataSchemaService
    {
        private readonly string _connectionString;
        private readonly SchemaProcessingSettings _settings;
        private readonly ILogger<SQLiteDataSchemaService> _logger;

        private List<TableRelationship> _relationshipsCache = new();
        private readonly object _cacheLock = new();
        private bool _isCacheInitialized;

        public SQLiteDataSchemaService(
            IOptions<DataStoreSettings> options,
            IOptions<SchemaProcessingSettings> schemaSettings,
            ILogger<SQLiteDataSchemaService>? logger = null)
        {
            _settings = schemaSettings?.Value ?? throw new ArgumentNullException(nameof(schemaSettings));
            var dataStoreSettings = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _connectionString = dataStoreSettings.ConnectionString;
            _logger = logger ?? LoggerFactory.Create(builder => builder.AddDebug()).CreateLogger<SQLiteDataSchemaService>();
        }

        private static string QuoteIdentifier(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";
        private static string ObjectName(string tableOrViewWithSchema)
        {
            if (string.IsNullOrWhiteSpace(tableOrViewWithSchema)) return string.Empty;
            var idx = tableOrViewWithSchema.LastIndexOf('.');
            return idx >= 0 ? tableOrViewWithSchema[(idx + 1)..] : tableOrViewWithSchema;
        }

        private static string QualifiedName(string name) => name.Contains('.') ? name : $"main.{name}";
        private static string Normalize(string name) => ObjectName(name);

        private static bool ParseBool(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return false;
            if (bool.TryParse(raw, out var b)) return b;
            if (long.TryParse(raw, out var n)) return n != 0;
            return raw.Equals("yes", StringComparison.OrdinalIgnoreCase) || raw.Equals("y", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<Dictionary<string, string>> GetColumnPropertyMapAsync(string objectName, string propertyName, CancellationToken ct)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            const string sql = @"
                SELECT ColumnName, PropertyValue
                FROM ExtendedProperties
                WHERE ObjectType='column' AND ObjectName=@ObjectName AND PropertyName=@PropertyName AND ColumnName IS NOT NULL";

            using var conn = new SqliteConnection(_connectionString);
            using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ObjectName", objectName);
            cmd.Parameters.AddWithValue("@PropertyName", propertyName);
            await conn.OpenAsync(ct);
            using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var col = r.GetString(0);
                var val = r.IsDBNull(1) ? string.Empty : r.GetString(1);
                map[col] = val;
            }
            return map;
        }

        private async Task<string?> GetObjectPropertyAsync(string objectType, string objectName, string propertyName, CancellationToken ct)
        {
            const string sql = @"
                SELECT PropertyValue
                FROM ExtendedProperties
                WHERE ObjectType=@ObjectType AND ObjectName=@ObjectName AND ColumnName IS NULL AND PropertyName=@PropertyName
                LIMIT 1";
            using var conn = new SqliteConnection(_connectionString);
            using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ObjectType", objectType);
            cmd.Parameters.AddWithValue("@ObjectName", objectName);
            cmd.Parameters.AddWithValue("@PropertyName", propertyName);
            await conn.OpenAsync(ct);
            var raw = await cmd.ExecuteScalarAsync(ct);
            return raw == null || raw == DBNull.Value ? null : raw.ToString();
        }

        private static List<string> ParseDomains(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return new List<string>();
            return raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private async Task<HashSet<string>> GetPkColumnsAsync(string tableName, CancellationToken ct)
        {
            var pks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var conn = new SqliteConnection(_connectionString);
            using var cmd = new SqliteCommand($"PRAGMA table_info({QuoteIdentifier(tableName)});", conn);
            await conn.OpenAsync(ct);
            using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                if (r.GetInt32(5) > 0) pks.Add(r.GetString(1));
            }
            return pks;
        }

        public async Task InitializeRelationshipCacheAsync(List<string>? tableScope = null, CancellationToken cancellationToken = default)
        {
            if (_isCacheInitialized) return;

            var scope = tableScope?.Select(Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase)
                ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var tables = new List<string>();
            var relations = new List<TableRelationship>();

            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            using (var tcmd = new SqliteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';", conn))
            using (var tr = await tcmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await tr.ReadAsync(cancellationToken))
                {
                    var t = tr.GetString(0);
                    if (!t.Equals("ExtendedProperties", StringComparison.OrdinalIgnoreCase)) tables.Add(t);
                }
            }

            foreach (var fromTable in tables)
            {
                using var fkCmd = new SqliteCommand($"PRAGMA foreign_key_list({QuoteIdentifier(fromTable)});", conn);
                using var fkR = await fkCmd.ExecuteReaderAsync(cancellationToken);

                var groups = new Dictionary<int, List<(string toTable, string fromCol, string toCol)>>();
                while (await fkR.ReadAsync(cancellationToken))
                {
                    var id = fkR.GetInt32(0);
                    var toTable = fkR.GetString(2);
                    var fromCol = fkR.GetString(3);
                    var toCol = fkR.GetString(4);
                    if (!groups.TryGetValue(id, out var list))
                    {
                        list = new List<(string toTable, string fromCol, string toCol)>();
                        groups[id] = list;
                    }
                    list.Add((toTable, fromCol, toCol));
                }

                foreach (var g in groups)
                {
                    var toTable = g.Value.First().toTable;
                    if (scope.Any() && !scope.Contains(fromTable) && !scope.Contains(toTable)) continue;

                    var fromPk = await GetPkColumnsAsync(fromTable, cancellationToken);
                    var toPk = await GetPkColumnsAsync(toTable, cancellationToken);
                    var fkFrom = g.Value.Select(x => x.fromCol).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var fkTo = g.Value.Select(x => x.toCol).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var oneToOne = fromPk.Count > 0 && toPk.Count > 0 && fromPk.SetEquals(fkFrom) && toPk.SetEquals(fkTo);

                    relations.Add(new TableRelationship
                    {
                        ForeignKeyName = $"FK_{fromTable}_{toTable}_{g.Key}",
                        FromTable = QualifiedName(fromTable),
                        ToTable = QualifiedName(toTable),
                        KeyColumns = g.Value.Select(x => new KeyColumnPair(x.fromCol, x.toCol)).ToList(),
                        RelationType = oneToOne ? RelationshipType.OneToOne : RelationshipType.OneToMany
                    });
                }
            }

            lock (_cacheLock)
            {
                _relationshipsCache = relations;
                _isCacheInitialized = true;
            }
        }

        public Task InitializeRelationshipCacheForSchemaAsync(string schema, CancellationToken cancellationToken = default)
            => InitializeRelationshipCacheAsync(null, cancellationToken);

        public Task<List<TableRelationship>> GetForeignKeyRelationshipsForTableAsync(string tableWithSchema)
        {
            if (!_isCacheInitialized) return Task.FromResult(new List<TableRelationship>());
            var target = Normalize(tableWithSchema);
            List<TableRelationship> all;
            lock (_cacheLock) all = _relationshipsCache;
            return Task.FromResult(all.Where(x => Normalize(x.FromTable).Equals(target, StringComparison.OrdinalIgnoreCase)).ToList());
        }

        public async Task<List<ColumnDefinition>> GetColumnDefinitionsAsync(string tableOrViewWithSchema, string extendedPropertyName = "Description", CancellationToken cancellationToken = default)
        {
            var obj = ObjectName(tableOrViewWithSchema);
            if (string.IsNullOrWhiteSpace(obj)) return new List<ColumnDefinition>();

            // Calls made through IDataSchemaService may pass legacy default "MS_Description_EN".
            // For SQLite we keep compatibility by falling back to configured property (typically "Description").
            var descProp = string.IsNullOrWhiteSpace(extendedPropertyName)
                ? _settings.ColumnDescriptionProperty
                : extendedPropertyName;

            var descMap = await GetColumnPropertyMapAsync(obj, descProp, cancellationToken);
            if (descMap.Count == 0 &&
                !descProp.Equals(_settings.ColumnDescriptionProperty, StringComparison.OrdinalIgnoreCase))
            {
                descMap = await GetColumnPropertyMapAsync(obj, _settings.ColumnDescriptionProperty, cancellationToken);
            }
            var embedMap = await GetColumnPropertyMapAsync(obj, "Embed", cancellationToken);

            var cols = new List<ColumnDefinition>();
            using var conn = new SqliteConnection(_connectionString);
            using var cmd = new SqliteCommand($"PRAGMA table_info({QuoteIdentifier(obj)});", conn);
            await conn.OpenAsync(cancellationToken);
            using var r = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await r.ReadAsync(cancellationToken))
            {
                var name = r.GetString(1);
                var type = r.IsDBNull(2) ? "TEXT" : r.GetString(2);
                var notNull = r.GetInt32(3) == 1;
                var def = r.IsDBNull(4) ? null : r.GetString(4);
                var pk = r.GetInt32(5) > 0;

                descMap.TryGetValue(name, out var desc);
                if (!string.IsNullOrWhiteSpace(extendedPropertyName) && string.IsNullOrWhiteSpace(desc)) continue;
                embedMap.TryGetValue(name, out var embedRaw);

                cols.Add(new ColumnDefinition
                {
                    Id = GeneratorUtil.GenerateDeterministicUlongId($"main.{obj}.{name}", true),
                    Name = name,
                    FullQualifiedTableName = QualifiedName(obj),
                    Type = type,
                    Description = desc ?? string.Empty,
                    IsNullable = !notNull,
                    DefaultValue = def,
                    IsPrimaryKey = pk,
                    IsAutoIncrement = false,
                    IsEnumLikeColumn = ParseBool(embedRaw),
                    KeyType = KeyType.None,
                    DescriptiveName = string.Empty
                });
            }

            if (!_isCacheInitialized) await InitializeRelationshipCacheAsync(cancellationToken: cancellationToken);
            List<TableRelationship> rel;
            lock (_cacheLock) rel = _relationshipsCache;
            var fkCols = rel.Where(x => Normalize(x.FromTable).Equals(obj, StringComparison.OrdinalIgnoreCase))
                .SelectMany(x => x.KeyColumns.Select(k => k.FromColumn))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var c in cols)
            {
                if (c.IsPrimaryKey) c.KeyType |= KeyType.Primary;
                if (fkCols.Contains(c.Name)) c.KeyType |= KeyType.Foreign;
            }
            if (cols.Count(x => x.IsPrimaryKey) > 1)
            {
                foreach (var c in cols.Where(x => x.IsPrimaryKey)) c.KeyType |= KeyType.Composite;
            }
            return cols;
        }
        public async Task<List<TableRelationship>> GetEntityRelationshipDiagramAsync(string tableWithSchema, List<string>? selectedTables = null, CancellationToken cancellationToken = default)
        {
            if (!_isCacheInitialized) await InitializeRelationshipCacheAsync(cancellationToken: cancellationToken);
            List<TableRelationship> all;
            lock (_cacheLock) all = _relationshipsCache;

            var start = Normalize(tableWithSchema);
            if (selectedTables == null || selectedTables.Count == 0)
            {
                return all.Where(r => Normalize(r.FromTable).Equals(start, StringComparison.OrdinalIgnoreCase) ||
                                      Normalize(r.ToTable).Equals(start, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            var targets = selectedTables.Select(Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var adjacency = new Dictionary<string, List<TableRelationship>>(StringComparer.OrdinalIgnoreCase);
            foreach (var rel in all)
            {
                var from = Normalize(rel.FromTable);
                var to = Normalize(rel.ToTable);
                if (!adjacency.TryGetValue(from, out var fl)) { fl = new List<TableRelationship>(); adjacency[from] = fl; }
                if (!adjacency.TryGetValue(to, out var tl)) { tl = new List<TableRelationship>(); adjacency[to] = tl; }
                fl.Add(rel); tl.Add(rel);
            }

            return await Task.Run(() =>
            {
                var found = new HashSet<TableRelationship>();
                foreach (var t in targets)
                {
                    if (t.Equals(start, StringComparison.OrdinalIgnoreCase)) continue;
                    var q = new Queue<(string node, List<TableRelationship> path)>();
                    var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { start };
                    q.Enqueue((start, new List<TableRelationship>()));
                    while (q.Count > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var (node, path) = q.Dequeue();
                        if (node.Equals(t, StringComparison.OrdinalIgnoreCase)) { foreach (var p in path) found.Add(p); break; }
                        if (!adjacency.TryGetValue(node, out var edges)) continue;
                        foreach (var e in edges)
                        {
                            var n = Normalize(e.FromTable).Equals(node, StringComparison.OrdinalIgnoreCase) ? Normalize(e.ToTable) : Normalize(e.FromTable);
                            if (!visited.Add(n)) continue;
                            var np = new List<TableRelationship>(path) { e };
                            q.Enqueue((n, np));
                        }
                    }
                }
                return found.ToList();
            }, cancellationToken);
        }

        public async Task<List<Dictionary<string, object?>>> GetSampleDataAsync(string tableWithSchema, List<string> selectedColumns, int rowCount = 5, CancellationToken cancellationToken = default)
        {
            var rows = new List<Dictionary<string, object?>>();
            if (selectedColumns == null || selectedColumns.Count == 0) return rows;

            var obj = ObjectName(tableWithSchema);
            var cols = selectedColumns.Select(x => x?.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (cols.Count == 0 || string.IsNullOrWhiteSpace(obj)) return rows;

            var selectCols = string.Join(", ", cols.Select(c => QuoteIdentifier(c!)));
            var notNull = string.Join(" AND ", cols.Select(c => $"{QuoteIdentifier(c!)} IS NOT NULL"));
            var sql = new StringBuilder($"SELECT {selectCols} FROM {QuoteIdentifier(obj)}");
            if (!string.IsNullOrWhiteSpace(notNull)) sql.Append($" WHERE {notNull}");
            if (rowCount > 0 && rowCount != int.MaxValue) sql.Append(" LIMIT @RowCount");
            sql.Append(';');

            using var conn = new SqliteConnection(_connectionString);
            using var cmd = new SqliteCommand(sql.ToString(), conn);
            if (rowCount > 0 && rowCount != int.MaxValue) cmd.Parameters.AddWithValue("@RowCount", rowCount);
            await conn.OpenAsync(cancellationToken);
            using var r = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await r.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < r.FieldCount; i++) row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
                rows.Add(row);
            }
            return rows;
        }

        public async Task<long> GetTableRowCountAsync(string tableWithSchema, CancellationToken cancellationToken = default)
        {
            var obj = ObjectName(tableWithSchema);
            if (string.IsNullOrWhiteSpace(obj)) return 0;
            using var conn = new SqliteConnection(_connectionString);
            using var cmd = new SqliteCommand($"SELECT COUNT(*) FROM {QuoteIdentifier(obj)};", conn);
            await conn.OpenAsync(cancellationToken);
            var raw = await cmd.ExecuteScalarAsync(cancellationToken);
            return raw == null || raw == DBNull.Value ? 0 : Convert.ToInt64(raw);
        }

        public async Task<List<string>> GetTableNamesAsync(List<string>? tables = null, CancellationToken cancellationToken = default)
        {
            var requested = tables?.Select(Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>();
            using var conn = new SqliteConnection(_connectionString);
            using var cmd = new SqliteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;", conn);
            await conn.OpenAsync(cancellationToken);
            using var r = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await r.ReadAsync(cancellationToken))
            {
                var t = r.GetString(0);
                if (t.Equals("ExtendedProperties", StringComparison.OrdinalIgnoreCase)) continue;
                if (requested == null || requested.Contains(t)) result.Add(QualifiedName(t));
            }
            return result;
        }

        public async Task<List<string>> GetRelevantTableNamesAsync(IEnumerable<string> schemas, string extendedPropertyName, CancellationToken cancellationToken = default)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(extendedPropertyName)) return result;
            const string sql = @"
                SELECT DISTINCT ObjectName
                FROM ExtendedProperties
                WHERE ObjectType='table' AND PropertyName=@PropertyName AND PropertyValue IS NOT NULL
                ORDER BY ObjectName;";
            using var conn = new SqliteConnection(_connectionString);
            using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@PropertyName", extendedPropertyName);
            await conn.OpenAsync(cancellationToken);
            using var r = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await r.ReadAsync(cancellationToken)) result.Add(QualifiedName(r.GetString(0)));
            return result;
        }

        public async Task<List<string>> GetRelevantViewNamesAsync(IEnumerable<string> schemas, string extendedPropertyName, CancellationToken cancellationToken = default)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(extendedPropertyName)) return result;
            const string sql = @"
                SELECT DISTINCT ObjectName
                FROM ExtendedProperties
                WHERE ObjectType='view' AND PropertyName=@PropertyName AND PropertyValue IS NOT NULL
                ORDER BY ObjectName;";
            using var conn = new SqliteConnection(_connectionString);
            using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@PropertyName", extendedPropertyName);
            await conn.OpenAsync(cancellationToken);
            using var r = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await r.ReadAsync(cancellationToken)) result.Add(QualifiedName(r.GetString(0)));
            return result;
        }

        public async Task<string> DetectRootTableAsync(List<string>? tableScope = null, CancellationToken cancellationToken = default)
        {
            if (!_isCacheInitialized) await InitializeRelationshipCacheAsync(cancellationToken: cancellationToken);
            List<TableRelationship> rel;
            lock (_cacheLock) rel = _relationshipsCache;

            HashSet<string> tables;
            if (tableScope != null && tableScope.Any())
            {
                tables = tableScope.Select(QualifiedName).ToHashSet(StringComparer.OrdinalIgnoreCase);
                rel = rel.Where(r => tables.Contains(r.FromTable) && tables.Contains(r.ToTable)).ToList();
            }
            else
            {
                tables = rel.SelectMany(r => new[] { r.FromTable, r.ToTable }).ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            if (!tables.Any())
            {
                var first = await GetTableNamesAsync(cancellationToken: cancellationToken);
                return first.FirstOrDefault() ?? string.Empty;
            }

            var score = tables.ToDictionary(x => x, _ => (outCount: 0, inCount: 0, rows: 0L), StringComparer.OrdinalIgnoreCase);
            foreach (var r in rel)
            {
                if (score.ContainsKey(r.FromTable)) { var s = score[r.FromTable]; s.outCount++; score[r.FromTable] = s; }
                if (score.ContainsKey(r.ToTable)) { var s = score[r.ToTable]; s.inCount++; score[r.ToTable] = s; }
            }
            var counts = await Task.WhenAll(tables.Select(async t => new { Table = t, Count = await GetTableRowCountAsync(t, cancellationToken) }));
            foreach (var c in counts)
            {
                if (score.ContainsKey(c.Table))
                {
                    var s = score[c.Table];
                    s.rows = c.Count;
                    score[c.Table] = s;
                }
            }
            return score.Select(k => new { k.Key, Value = k.Value.outCount * 5 + k.Value.inCount + Math.Log10(k.Value.rows + 1) })
                .OrderByDescending(x => x.Value)
                .First().Key;
        }
        private async Task BuildOntologyNodeAsync(string currentTable, Dictionary<string, object> parentNode, HashSet<string> visited, IReadOnlyList<TableRelationship> relationships, CancellationToken ct)
        {
            if (visited.Contains(currentTable)) return;
            visited.Add(currentTable);

            var cols = await GetColumnDefinitionsAsync(currentTable, _settings.ColumnDescriptionProperty, ct);
            var outgoing = relationships.Where(r => r.FromTable.Equals(currentTable, StringComparison.OrdinalIgnoreCase)).ToList();
            var fkCols = outgoing.SelectMany(r => r.KeyColumns.Select(k => k.FromColumn)).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var c in cols)
            {
                if (fkCols.Contains(c.Name) || string.IsNullOrWhiteSpace(c.Description)) continue;
                var cNode = new Dictionary<string, object> { ["description"] = c.Description };
                if (c.IsEnumLikeColumn)
                {
                    var sample = await GetSampleDataAsync(currentTable, new List<string> { c.Name }, 50, ct);
                    var values = sample.Select(x => x.TryGetValue(c.Name, out var v) ? v?.ToString() : null)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(6)
                        .ToList();
                    if (values.Any()) cNode["sampleData"] = values;
                }
                parentNode[$"{c.FullQualifiedTableName}.{c.Name}"] = cNode;
            }

            foreach (var rel in outgoing)
            {
                var child = new Dictionary<string, object>();
                parentNode[rel.ToTable] = child;
                await BuildOntologyNodeAsync(rel.ToTable, child, visited, relationships, ct);
            }
        }

        public async Task<string> GenerateOntologyFromRelationshipsAsync(string rootTableName, IReadOnlyList<TableRelationship> relationships, CancellationToken cancellationToken)
        {
            var root = new Dictionary<string, object>();
            var node = new Dictionary<string, object>();
            root[rootTableName] = node;
            await BuildOntologyNodeAsync(rootTableName, node, new HashSet<string>(StringComparer.OrdinalIgnoreCase), relationships, cancellationToken);
            return JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true });
        }

        public async Task<string> GenerateTableOntologyAsJsonAsync(List<string> tableScope, string rootTableName = null, CancellationToken cancellationToken = default)
        {
            if (!_isCacheInitialized) await InitializeRelationshipCacheAsync(cancellationToken: cancellationToken);
            List<TableRelationship> rel;
            lock (_cacheLock) rel = _relationshipsCache;

            var scope = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (tableScope != null && tableScope.Any())
            {
                var filter = tableScope.Select(QualifiedName).ToHashSet(StringComparer.OrdinalIgnoreCase);
                rel = rel.Where(r => filter.Contains(r.FromTable) && filter.Contains(r.ToTable)).ToList();
                scope.UnionWith(filter);
            }
            else
            {
                scope.UnionWith(rel.Select(r => r.FromTable));
                scope.UnionWith(rel.Select(r => r.ToTable));
            }
            if (!scope.Any()) return "{}";

            var root = string.IsNullOrWhiteSpace(rootTableName) ? await DetectRootTableAsync(scope.ToList(), cancellationToken) : QualifiedName(rootTableName);
            var order = new List<string>();
            if (scope.Contains(root)) order.Add(root);
            order.AddRange(scope.Where(x => !x.Equals(root, StringComparison.OrdinalIgnoreCase)));

            var ontology = new Dictionary<string, object>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var table in order)
            {
                if (visited.Contains(table)) continue;
                var node = new Dictionary<string, object>();
                ontology[table] = node;
                var tDef = await GetTableDefinitionsAsync(new List<string> { table }, _settings.TableDescriptionProperty, cancellationToken);
                var desc = tDef.FirstOrDefault()?.Description;
                if (!string.IsNullOrWhiteSpace(desc)) node["description"] = desc;
                await BuildOntologyNodeAsync(table, node, visited, rel, cancellationToken);
            }

            return JsonSerializer.Serialize(ontology, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }

        public async Task<string> GenerateTableOntologyAsJsonAsync(List<object> tables, string rootTableName = null, CancellationToken cancellationToken = default)
        {
            if (tables == null || !tables.Any()) return string.Empty;
            var mapped = tables.OfType<Dictionary<string, object>>()
                .Select(d => new { Name = d.TryGetValue("name", out var n) ? n?.ToString() : null, Description = d.TryGetValue("description", out var x) ? x?.ToString() : null })
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .ToList();
            if (!mapped.Any()) return "{}";

            var scope = mapped.Select(x => QualifiedName(x.Name!)).ToList();
            var dmap = mapped.ToDictionary(x => QualifiedName(x.Name!), x => x.Description, StringComparer.OrdinalIgnoreCase);
            var json = await GenerateTableOntologyAsJsonAsync(scope, rootTableName, cancellationToken);
            var doc = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();
            foreach (var kv in dmap)
            {
                if (!string.IsNullOrWhiteSpace(kv.Value) && doc.TryGetValue(kv.Key, out var nodeObj) && nodeObj is JsonElement je && je.ValueKind == JsonValueKind.Object)
                {
                    // keep as-is; description from input is optional and service already hydrates DB description
                }
            }
            return json;
        }

        public async Task<string> GenerateViewOntologyAsJsonAsync(List<string> viewNames, CancellationToken cancellationToken = default)
        {
            if (viewNames == null || !viewNames.Any()) return "{}";
            var ontology = new Dictionary<string, object>();
            var vdefs = await GetViewDefinitionsAsync(viewNames, _settings.ViewDescriptionProperty, cancellationToken);
            var vmap = vdefs.ToDictionary(v => v.FullQualifiedViewName, v => v, StringComparer.OrdinalIgnoreCase);

            foreach (var v in viewNames.Select(QualifiedName))
            {
                var node = new Dictionary<string, object>();
                if (vmap.TryGetValue(v, out var vd) && !string.IsNullOrWhiteSpace(vd.Description)) node["description"] = vd.Description;
                var attrs = new Dictionary<string, object>();
                var cols = await GetColumnDefinitionsAsync(v, _settings.ColumnDescriptionProperty, cancellationToken);
                foreach (var c in cols.OrderBy(x => x.Name))
                {
                    if (string.IsNullOrWhiteSpace(c.Description)) continue;
                    attrs[$"{c.FullQualifiedTableName}.{c.Name}"] = new Dictionary<string, object> { ["description"] = c.Description };
                }
                if (attrs.Any()) node["attributes"] = attrs;
                if (node.Any()) ontology[v] = node;
            }

            return JsonSerializer.Serialize(ontology, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }

        public async Task<string> GenerateViewOntologyAsJsonAsync(List<object> views, CancellationToken cancellationToken = default)
        {
            if (views == null || !views.Any()) return string.Empty;
            var names = views.OfType<Dictionary<string, object>>()
                .Select(d => d.TryGetValue("name", out var n) ? n?.ToString() : null)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .ToList();
            return await GenerateViewOntologyAsJsonAsync(names, cancellationToken);
        }

        public async Task<List<TableDefinition>> GetTableDefinitionsAsync(List<string> tableNames, string extendedPropertyName, CancellationToken cancellationToken = default)
        {
            var result = new List<TableDefinition>();
            var requested = tableNames?.Select(Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
            using var conn = new SqliteConnection(_connectionString);
            using var cmd = new SqliteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;", conn);
            await conn.OpenAsync(cancellationToken);
            using var r = await cmd.ExecuteReaderAsync(cancellationToken);
            var all = new List<string>();
            while (await r.ReadAsync(cancellationToken))
            {
                var t = r.GetString(0);
                if (!t.Equals("ExtendedProperties", StringComparison.OrdinalIgnoreCase)) all.Add(t);
            }

            foreach (var t in all)
            {
                if (requested != null && !requested.Contains(t)) continue;
                var desc = await GetObjectPropertyAsync("table", t, extendedPropertyName, cancellationToken);
                if ((requested == null || requested.Count == 0) && string.IsNullOrWhiteSpace(desc)) continue;
                var domains = await GetObjectPropertyAsync("table", t, "Domain", cancellationToken);
                result.Add(new TableDefinition
                {
                    Id = GeneratorUtil.GenerateDeterministicUlongId(QualifiedName(t), true),
                    FullQualifiedTableName = QualifiedName(t),
                    Schema = "main",
                    Name = t,
                    Description = desc ?? string.Empty,
                    BelongsToDomains = ParseDomains(domains)
                });
            }
            return result;
        }

        public async Task<List<ViewDefinition>> GetViewDefinitionsAsync(List<string> viewNames, string extendedPropertyName, CancellationToken cancellationToken = default)
        {
            var result = new List<ViewDefinition>();
            var requested = viewNames?.Select(Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
            using var conn = new SqliteConnection(_connectionString);
            using var cmd = new SqliteCommand("SELECT name FROM sqlite_master WHERE type='view' ORDER BY name;", conn);
            await conn.OpenAsync(cancellationToken);
            using var r = await cmd.ExecuteReaderAsync(cancellationToken);
            var all = new List<string>();
            while (await r.ReadAsync(cancellationToken)) all.Add(r.GetString(0));

            foreach (var v in all)
            {
                if (requested != null && !requested.Contains(v)) continue;
                var desc = await GetObjectPropertyAsync("view", v, extendedPropertyName, cancellationToken);
                if ((requested == null || requested.Count == 0) && string.IsNullOrWhiteSpace(desc)) continue;
                var domains = await GetObjectPropertyAsync("view", v, "Domain", cancellationToken);
                result.Add(new ViewDefinition
                {
                    Id = GeneratorUtil.GenerateDeterministicUlongId(QualifiedName(v), true),
                    FullQualifiedViewName = QualifiedName(v),
                    Schema = "main",
                    Name = v,
                    Description = desc ?? string.Empty,
                    BelongsToDomains = ParseDomains(domains)
                });
            }
            return result;
        }

        public Task<List<FunctionDefinition>> GetTableValueFunctionsAsync(string msDescriptionPropertyName, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<FunctionDefinition>());

        public Task<List<FunctionDefinition>> GetStoredProceduresAsync(IEnumerable<string> schemas, string extendedPropertyName, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<FunctionDefinition>());

        public async Task<string> GenerateTableOntologyAsDDLAsync(List<string> tableNames, CancellationToken cancellationToken = default)
        {
            if (tableNames == null || !tableNames.Any()) return "-- No tables specified for DDL generation.";
            var ddl = new StringBuilder();
            var tables = await GetTableDefinitionsAsync(tableNames, _settings.TableDescriptionProperty, cancellationToken);
            foreach (var t in tables)
            {
                if (ddl.Length > 0) ddl.AppendLine().AppendLine();
                if (!string.IsNullOrWhiteSpace(t.Description)) ddl.AppendLine($"-- {t.Description.Replace("\n", "\n-- ")}");
                ddl.AppendLine($"CREATE TABLE {t.FullQualifiedTableName} (");
                var cols = await GetColumnDefinitionsAsync(t.FullQualifiedTableName, _settings.ColumnDescriptionProperty, cancellationToken);
                ddl.AppendLine(string.Join(",\n", cols.Select(c =>
                {
                    var s = new StringBuilder($"    [{c.Name}] {c.Type}");
                    if (!c.IsNullable) s.Append(" NOT NULL");
                    if (!string.IsNullOrWhiteSpace(c.Description)) s.Append($" -- {c.Description.Replace("\n", " ")}");
                    return s.ToString();
                })));
                var pk = cols.Where(c => c.IsPrimaryKey).ToList();
                if (pk.Any()) ddl.AppendLine($",\n    CONSTRAINT PK_{t.Name} PRIMARY KEY ({string.Join(", ", pk.Select(x => $"[{x.Name}]"))})");
                var fks = await GetForeignKeyRelationshipsForTableAsync(t.FullQualifiedTableName);
                foreach (var fk in fks)
                {
                    if (!fk.KeyColumns.Any()) continue;
                    ddl.AppendLine($",\n    CONSTRAINT {fk.ForeignKeyName} FOREIGN KEY ({string.Join(", ", fk.KeyColumns.Select(k => $"[{k.FromColumn}]"))}) REFERENCES {fk.ToTable}({string.Join(", ", fk.KeyColumns.Select(k => $"[{k.ToColumn}]"))})");
                }
                ddl.AppendLine("\n);");
            }
            return ddl.ToString();
        }

        public async Task<string> GenerateViewOntologyAsDDLAsync(List<string> viewNames, CancellationToken cancellationToken = default)
        {
            if (viewNames == null || !viewNames.Any()) return "-- No views specified for DDL generation.";
            var ddl = new StringBuilder();
            var views = await GetViewDefinitionsAsync(viewNames, _settings.ViewDescriptionProperty, cancellationToken);
            foreach (var v in views)
            {
                if (ddl.Length > 0) ddl.AppendLine().AppendLine();
                if (!string.IsNullOrWhiteSpace(v.Description)) ddl.AppendLine($"-- {v.Description.Replace("\n", "\n-- ")}");
                ddl.AppendLine("-- NOTE: Simplified DDL representation of a VIEW.");
                ddl.AppendLine($"CREATE VIEW {v.FullQualifiedViewName} AS");
                ddl.AppendLine("SELECT");
                var cols = await GetColumnDefinitionsAsync(v.FullQualifiedViewName, _settings.ColumnDescriptionProperty, cancellationToken);
                ddl.AppendLine(string.Join("\n", cols.Select(c =>
                {
                    var comment = string.IsNullOrWhiteSpace(c.Description) ? string.Empty : $" -- {c.Description.Replace("\n", " ")}";
                    return $"    [{c.Name}] AS [{c.Name}],{comment}";
                })).TrimEnd(','));
                ddl.AppendLine("FROM [underlying_tables...];");
            }
            return ddl.ToString();
        }

        public async Task<string> GenerateSchemaDescriptionsBackupAsync(List<string> schemas, CancellationToken cancellationToken = default)
        {
            var sb = new StringBuilder();
            sb.AppendLine("-- Backup of ExtendedProperties for SQLite");
            sb.AppendLine($"-- Generated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            const string sql = @"
                SELECT ObjectType, ObjectName, ColumnName, PropertyName, PropertyValue
                FROM ExtendedProperties
                ORDER BY ObjectType, ObjectName, ColumnName, PropertyName;";

            using var conn = new SqliteConnection(_connectionString);
            using var cmd = new SqliteCommand(sql, conn);
            await conn.OpenAsync(cancellationToken);
            using var r = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await r.ReadAsync(cancellationToken))
            {
                var objectType = r.IsDBNull(0) ? string.Empty : r.GetString(0);
                var objectName = r.IsDBNull(1) ? string.Empty : r.GetString(1);
                var columnName = r.IsDBNull(2) ? null : r.GetString(2);
                var propertyName = r.IsDBNull(3) ? string.Empty : r.GetString(3);
                var propertyValue = r.IsDBNull(4) ? null : r.GetString(4);

                var col = columnName == null ? "NULL" : $"'{columnName.Replace("'", "''")}'";
                var val = propertyValue == null ? "NULL" : $"'{propertyValue.Replace("'", "''")}'";
                sb.AppendLine($"INSERT INTO ExtendedProperties (ObjectType, ObjectName, ColumnName, PropertyName, PropertyValue) VALUES ('{objectType.Replace("'", "''")}', '{objectName.Replace("'", "''")}', {col}, '{propertyName.Replace("'", "''")}', {val});");
            }
            return sb.ToString();
        }
    }
}
