using System.Collections.Generic;
using QueryLens.Core.Aggregation;
using Xunit;

namespace QueryLens.Core.Tests.Aggregation
{
    public class PercentileCalculatorTests
    {
        [Fact]
        public void P50_OfOddSizedSortedList_ReturnsMiddleValue()
        {
            var values = new List<double> { 10, 20, 30, 40, 50 };

            Assert.Equal(30, PercentileCalculator.Percentile(values, 50));
        }

        [Fact]
        public void P0_ReturnsMinimum()
        {
            var values = new List<double> { 10, 20, 30 };

            Assert.Equal(10, PercentileCalculator.Percentile(values, 0));
        }

        [Fact]
        public void P100_ReturnsMaximum()
        {
            var values = new List<double> { 10, 20, 30 };

            Assert.Equal(30, PercentileCalculator.Percentile(values, 100));
        }

        [Fact]
        public void EmptyList_ReturnsZero()
        {
            Assert.Equal(0, PercentileCalculator.Percentile(new List<double>(), 95));
        }

        [Fact]
        public void SingleValue_ReturnsThatValue()
        {
            var values = new List<double> { 42 };

            Assert.Equal(42, PercentileCalculator.Percentile(values, 95));
        }

        [Fact]
        public void InterpolatesBetweenTwoClosestRanks()
        {
            var values = new List<double> { 0, 10 };

            // rank = 0.5 * (2-1) = 0.5 -> halfway between index 0 and 1.
            Assert.Equal(5, PercentileCalculator.Percentile(values, 50));
        }
    }
}
