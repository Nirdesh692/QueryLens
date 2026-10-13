using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace QueryLens.Core.Fingerprinting
{
    /// <summary>
    /// Normalizes raw SQL text into a stable, parameter-agnostic fingerprint so
    /// that structurally identical queries with different literal values (e.g.
    /// "WHERE Id = 10" vs "WHERE Id = 20") are recognized as the same logical
    /// query, while genuinely different queries are not incorrectly merged.
    ///
    /// This is intentionally a deterministic, regex-based normalizer rather
    /// than a full SQL parser: it is fast, dependency-free, and good enough to
    /// group queries by shape. It does not attempt to validate or understand
    /// the SQL beyond that.
    /// </summary>
    public sealed class SqlQueryFingerprinter : IQueryFingerprinter
    {
        // Strip /* ... */ block comments.
        private static readonly Regex BlockComment =
            new Regex(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.Compiled);

        // Strip -- line comments (to end of line).
        private static readonly Regex LineComment =
            new Regex(@"--[^\r\n]*", RegexOptions.Compiled);

        // Collapse any run of whitespace (including newlines) to a single space.
        private static readonly Regex Whitespace =
            new Regex(@"\s+", RegexOptions.Compiled);

        // 'single-quoted string', with '' as an escaped quote inside the literal.
        // Covers plain strings, GUIDs, and dates, since T-SQL always quotes these.
        private static readonly Regex StringLiteral =
            new Regex(@"'(?:[^']|'')*'", RegexOptions.Compiled);

        // 0x-prefixed hex/binary literals.
        private static readonly Regex HexLiteral =
            new Regex(@"0x[0-9A-Fa-f]+", RegexOptions.Compiled);

        // Bare integer/decimal literals. Word-boundary lookaround keeps this
        // from touching digits embedded in identifiers such as "Table1" or "col2b".
        private static readonly Regex NumericLiteral =
            new Regex(@"(?<!\w)\d+(\.\d+)?(?!\w)", RegexOptions.Compiled);

        // Collapses a variable-length "IN (?, ?, ?, ...)" list down to "IN (?)"
        // so that IN clauses differing only in item count still fingerprint the
        // same. Scoped to IN specifically so ordinary multi-column tuples (e.g.
        // an INSERT's VALUES list) are left alone.
        private static readonly Regex InListCollapse =
            new Regex(@"(\bIN\b)\s*\(\s*(?:\?\s*,\s*)+\?\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Cosmetic tidy-up once literals are collapsed to "?".
        private static readonly Regex SpaceBeforePunctuation =
            new Regex(@"\s+([,)])", RegexOptions.Compiled);

        private static readonly Regex SpaceAfterOpenParen =
            new Regex(@"([(])\s+", RegexOptions.Compiled);

        public QueryFingerprint Fingerprint(string sql)
        {
            if (sql == null)
            {
                throw new ArgumentNullException(nameof(sql));
            }

            var representative = CleanWhitespace(StripComments(sql));
            var normalized = Normalize(representative);
            var hash = ComputeHash(normalized);

            return new QueryFingerprint(hash, normalized, representative);
        }

        private static string StripComments(string sql)
        {
            var withoutBlockComments = BlockComment.Replace(sql, " ");
            var withoutLineComments = LineComment.Replace(withoutBlockComments, " ");
            return withoutLineComments;
        }

        private static string CleanWhitespace(string sql)
        {
            return Whitespace.Replace(sql, " ").Trim();
        }

        private static string Normalize(string cleanedSql)
        {
            var withoutStrings = StringLiteral.Replace(cleanedSql, "?");
            var withoutHex = HexLiteral.Replace(withoutStrings, "?");
            var withoutNumbers = NumericLiteral.Replace(withoutHex, "?");
            var withCollapsedInLists = InListCollapse.Replace(withoutNumbers, "$1 (?)");

            var tidied = SpaceBeforePunctuation.Replace(withCollapsedInLists, "$1");
            tidied = SpaceAfterOpenParen.Replace(tidied, "$1");

            return CleanWhitespace(tidied);
        }

        private static string ComputeHash(string normalizedSql)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(normalizedSql);
                var hashBytes = sha256.ComputeHash(bytes);

                // 12 hex characters (6 bytes) is plenty of entropy for grouping
                // queries in a single application's telemetry and keeps report
                // output readable, e.g. "A71B92C4F0E1".
                var builder = new StringBuilder(12);
                for (var i = 0; i < 6; i++)
                {
                    builder.Append(hashBytes[i].ToString("X2"));
                }

                return builder.ToString();
            }
        }
    }
}
