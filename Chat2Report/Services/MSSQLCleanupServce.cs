using Chat2Report.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Chat2Report.Services
{
    public class MSSQLCleanupServce : ICleanupSQLService
    {
        private readonly ILogger<MSSQLCleanupServce> _logger;

        public MSSQLCleanupServce(ILogger<MSSQLCleanupServce> logger)
        {
            _logger = logger;
        }

        public string Clean(string rawSql, UserQueryAnalysis? analysis)
        {
            string cleanSql = CleanSqlResponse(rawSql);
            return AddTopClause(cleanSql, analysis);
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

        private string AddTopClause(string sql, UserQueryAnalysis? analysis)
        {
            bool hasTopClause = Regex.IsMatch(sql, @"\bTOP\s+\d+\b", RegexOptions.IgnoreCase);
            if (hasTopClause)
            {
                var incorrectOrderMatch = Regex.Match(sql, @"^\s*SELECT\s+(TOP\s+\d+)\s+(DISTINCT)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (incorrectOrderMatch.Success)
                {
                    string topClause = incorrectOrderMatch.Groups[1].Value;
                    string distinctClause = incorrectOrderMatch.Groups[2].Value;
                    string correctedSql = Regex.Replace(
                        sql,
                        @"^\s*SELECT\s+TOP\s+\d+\s+DISTINCT",
                        $"SELECT {distinctClause} {topClause}",
                        RegexOptions.IgnoreCase | RegexOptions.Singleline,
                        TimeSpan.FromSeconds(1));

                    _logger.LogInformation("Corrected invalid order of TOP and DISTINCT clauses.");
                    return correctedSql;
                }

                _logger.LogInformation("SQL already contains a TOP clause, skipping modification.");
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

            string finalSql = Regex.Replace(
                sql,
                @"^\s*SELECT(\s+DISTINCT)?",
                $"SELECT$1 TOP {limit}",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            _logger.LogInformation(
                limitFromIntent ? "Applied TOP {Limit} clause from user 'LIMIT' intent." : "Applied default TOP {Limit} clause.",
                limit);

            return finalSql;
        }
    }
}
