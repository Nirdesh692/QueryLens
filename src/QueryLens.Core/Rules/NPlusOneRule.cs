using System.Collections.Generic;
using QueryLens.Core.Models;

namespace QueryLens.Core.Rules
{
    /// <summary>
    /// Flags a query that executes repeatedly with different parameter values
    /// within a single request/operation - the classic N+1 pattern. Confidence
    /// grows with repeat count rather than treating every repeated query as a
    /// certainty, per the design goal: "use confidence scoring rather than
    /// claiming every repeated query is definitely N+1."
    /// </summary>
    public sealed class NPlusOneRule : IQueryAnalysisRule
    {
        public string RuleId => "NPLUSONE001";

        public IEnumerable<Finding> Evaluate(RuleContext context)
        {
            if (!context.Options.Analysis.NPlusOne)
            {
                yield break;
            }

            var thresholds = context.Options.Thresholds;

            foreach (var group in context.RequestGroups)
            {
                var count = group.Executions.Count;

                if (count < thresholds.NPlusOneMinExecutions)
                {
                    continue;
                }

                var confidence = ComputeConfidence(count);

                if (confidence < thresholds.NPlusOneMinConfidence)
                {
                    continue;
                }

                var potentialRoundTripsSaved = count - 1;

                yield return new Finding
                {
                    RuleId = RuleId,
                    Severity = confidence >= 0.85 ? Severity.Critical : Severity.Warning,
                    Title = "Possible N+1 query pattern",
                    Description = $"Query executed {count} times within request '{group.RequestId}', consistent with an N+1 pattern.",
                    QueryHash = context.Statistics.QueryHash,
                    Confidence = confidence,
                    Recommendation = "Consider batching (e.g. a single query with WHERE Id IN (...)) or eager-loading the related data.",
                    Evidence = new Dictionary<string, string>
                    {
                        ["requestId"] = group.RequestId,
                        ["executionsInRequest"] = count.ToString(),
                        ["potentialRoundTripsSaved"] = potentialRoundTripsSaved.ToString()
                    }
                };
            }
        }

        /// <summary>
        /// Heuristic confidence curve: 1 - 2/(count+2). Approaches 1.0 as the
        /// repeat count grows and equals exactly 0.6 at a repeat count of 3,
        /// matching the default NPlusOneMinConfidence threshold.
        /// </summary>
        private static double ComputeConfidence(int count)
        {
            var raw = 1.0 - 2.0 / (count + 2.0);
            return raw > 0.99 ? 0.99 : raw;
        }
    }
}
