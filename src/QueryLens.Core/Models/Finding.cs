using System.Collections.Generic;

namespace QueryLens.Core.Models
{
    /// <summary>
    /// One issue or observation raised by a rule. Findings are evidence-based
    /// and carry a confidence score rather than presenting every possible issue
    /// as a certainty.
    /// </summary>
    public sealed class Finding
    {
        public string RuleId { get; set; }

        public Severity Severity { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        /// <summary>Query this finding relates to, or null for request-level findings (e.g. query explosion).</summary>
        public string QueryHash { get; set; }

        /// <summary>0.0-1.0. 1.0 for deterministic rules; lower for heuristic rules like N+1 detection.</summary>
        public double Confidence { get; set; } = 1.0;

        public string Recommendation { get; set; }

        public IReadOnlyDictionary<string, string> Evidence { get; set; } = new Dictionary<string, string>();
    }
}
