namespace QueryLens.Cli
{
    /// <summary>Process exit codes. Stable: CI pipelines depend on them.</summary>
    public static class ExitCodes
    {
        /// <summary>Command succeeded; for verify, all checks passed.</summary>
        public const int Success = 0;

        /// <summary>verify ran correctly but at least one check failed.</summary>
        public const int VerificationFailed = 1;

        /// <summary>Bad arguments, unreadable/invalid input, or an unavailable feature.</summary>
        public const int UsageError = 2;
    }
}
