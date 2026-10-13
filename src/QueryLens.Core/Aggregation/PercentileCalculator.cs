using System;
using System.Collections.Generic;

namespace QueryLens.Core.Aggregation
{
    /// <summary>
    /// Computes percentiles over a set of samples using linear interpolation
    /// between the two closest ranks (the method used by Excel and NumPy's
    /// default "linear" interpolation).
    /// </summary>
    public static class PercentileCalculator
    {
        /// <param name="sortedValues">Values already sorted ascending.</param>
        /// <param name="percentile">0-100.</param>
        public static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
        {
            if (sortedValues == null)
            {
                throw new ArgumentNullException(nameof(sortedValues));
            }

            if (sortedValues.Count == 0)
            {
                return 0;
            }

            if (percentile <= 0)
            {
                return sortedValues[0];
            }

            if (percentile >= 100)
            {
                return sortedValues[sortedValues.Count - 1];
            }

            var rank = (percentile / 100.0) * (sortedValues.Count - 1);
            var lowerIndex = (int)Math.Floor(rank);
            var upperIndex = (int)Math.Ceiling(rank);

            if (lowerIndex == upperIndex)
            {
                return sortedValues[lowerIndex];
            }

            var weight = rank - lowerIndex;
            return sortedValues[lowerIndex] + (sortedValues[upperIndex] - sortedValues[lowerIndex]) * weight;
        }
    }
}
