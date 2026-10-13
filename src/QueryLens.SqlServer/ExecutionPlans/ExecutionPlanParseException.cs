using System;

namespace QueryLens.SqlServer.ExecutionPlans
{
    /// <summary>Thrown when execution plan XML can't be parsed as XML at all.</summary>
    public sealed class ExecutionPlanParseException : Exception
    {
        public ExecutionPlanParseException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
