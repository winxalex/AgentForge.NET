using Chat2Report.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Chat2Report.Services
{
    public class SQLiteCleanup : ICleanupSQLService
    {
        private readonly ILogger<SQLiteCleanup> _logger;

        public SQLiteCleanup(ILogger<SQLiteCleanup> logger)
        {
            _logger = logger;
        }

        public string Clean(string rawSql, UserQueryAnalysis? analysis)
        {
            string cleanSql = CleanSqlResponse(rawSql);
            return AddLimitClause(cleanSql, analysis);
        }

        private string CleanSqlResponse(string rawSql)
        {
            if (string.IsNullOrWhiteSpace(rawSql)) return string.Empty;

            var match = Regex.Match(rawSql, @"(?s)```(?:sql\s*)?(.*?)```");
            if (match.Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
            {
                return match.Groups[1].Value.Trim();
            }

            _logger.LogWarning("LLM response did not contain a markdown code block. Using fallback cleaning. Raw: '{RawSql}'", rawSql);
            int selectIndex = rawSql.IndexOf("SELECT", StringComparison.OrdinalIgnoreCase);
            return selectIndex == -1 ? rawSql.Trim() : rawSql.Substring(selectIndex).Trim();
        }

        private string AddLimitClause(string sql, UserQueryAnalysis? analysis)
        {
            bool hasLimitClause = Regex.IsMatch(sql, @"\bLIMIT\s+\d+(\s+OFFSET\s+\d+)?\b", RegexOptions.IgnoreCase);
            if (hasLimitClause)
            {
                _logger.LogInformation("SQL already contains a LIMIT clause, skipping modification.");
                return sql;
            }

            int limit = 10;
            bool limitFromIntent = false;

            if (analysis != null)
            {
                var limitAttribute = analysis.ExtractedAttributes?
                    .FirstOrDefault(attr => "LIMIT".Equals(attr.Intent, StringComparison.OrdinalIgnoreCase));

                if (limitAttribute?.Value?.Data?.FirstOrDefault() != null
                    && int.TryParse(limitAttribute.Value.Data.First(), out int parsedLimit)
                    && parsedLimit > 0)
                {
                    limit = parsedLimit;
                    limitFromIntent = true;
                }
            }

            string finalSql = sql.TrimEnd();
            bool hasTerminatingSemicolon = finalSql.EndsWith(";", StringComparison.Ordinal);
            if (hasTerminatingSemicolon)
            {
                finalSql = finalSql.Substring(0, finalSql.Length - 1).TrimEnd();
            }

            finalSql = $"{finalSql} LIMIT {limit}";
            if (hasTerminatingSemicolon)
            {
                finalSql += ";";
            }

            _logger.LogInformation(
                limitFromIntent ? "Applied LIMIT {Limit} clause from user 'LIMIT' intent." : "Applied default LIMIT {Limit} clause.",
                limit);

            return finalSql;
        }
    }
}
