namespace QueryLens.Core.Fingerprinting
{
    /// <summary>
    /// Result of fingerprinting one raw SQL string.
    /// </summary>
    public sealed class QueryFingerprint
    {
        public QueryFingerprint(string hash, string normalizedSql, string representativeSql)
        {
            Hash = hash;
            NormalizedSql = normalizedSql;
            RepresentativeSql = representativeSql;
        }

        /// <summary>Stable hash of <see cref="NormalizedSql"/>.</summary>
        public string Hash { get; }

        /// <summary>SQL with literal values replaced by "?" placeholders.</summary>
        public string NormalizedSql { get; }

        /// <summary>Whitespace-cleaned SQL with the original literal values retained.</summary>
        public string RepresentativeSql { get; }
    }
}
