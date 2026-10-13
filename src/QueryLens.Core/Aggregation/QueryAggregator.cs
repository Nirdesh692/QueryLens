using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using QueryLens.Core.Models;

namespace QueryLens.Core.Aggregation
{
    /// <summary>
    /// Thread-safe, in-memory aggregator that maintains running statistics per
    /// query fingerprint. A bounded, reservoir-sampled set of durations is kept
    /// per query so percentiles can be computed without retaining every
    /// execution ever observed.
    /// </summary>
    public sealed class QueryAggregator : IQueryAggregator
    {
        private readonly int _maxSamplesPerQuery;
        private readonly ConcurrentDictionary<string, QueryBucket> _buckets =
            new ConcurrentDictionary<string, QueryBucket>();

        public QueryAggregator(int maxSamplesPerQuery = 5000)
        {
            if (maxSamplesPerQuery <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSamplesPerQuery));
            }

            _maxSamplesPerQuery = maxSamplesPerQuery;
        }

        public void Add(QueryExecution execution)
        {
            if (execution == null)
            {
                throw new ArgumentNullException(nameof(execution));
            }

            if (string.IsNullOrEmpty(execution.QueryHash))
            {
                throw new ArgumentException(
                    "QueryExecution.QueryHash must be set (via the fingerprinter) before aggregation.",
                    nameof(execution));
            }

            var bucket = _buckets.GetOrAdd(
                execution.QueryHash,
                _ => new QueryBucket(execution.QueryHash, _maxSamplesPerQuery));

            bucket.Add(execution);
        }

        public QueryStatistics GetStatistics(string queryHash)
        {
            if (queryHash == null)
            {
                throw new ArgumentNullException(nameof(queryHash));
            }

            return _buckets.TryGetValue(queryHash, out var bucket) ? bucket.ToStatistics() : null;
        }

        public IReadOnlyCollection<QueryStatistics> GetAllStatistics()
        {
            return _buckets.Values.Select(b => b.ToStatistics()).ToList();
        }

        public void Reset()
        {
            _buckets.Clear();
        }

        private sealed class QueryBucket
        {
            private readonly object _sync = new object();
            private readonly int _maxSamples;
            private readonly List<double> _durationSamplesMs = new List<double>();
            private readonly Random _random = new Random();

            private string _normalizedSql;
            private string _representativeSql;

            private long _count;
            private double _totalDurationMs;
            private double _minDurationMs = double.MaxValue;
            private double _maxDurationMs;

            private double _totalCpuMs;
            private long _cpuSampleCount;

            private double _totalLogicalReads;
            private long _logicalReadsSampleCount;

            private double _totalPhysicalReads;
            private long _physicalReadsSampleCount;

            private double _totalRowsReturned;
            private long _rowsReturnedSampleCount;
            private long _maxRowsReturned;

            public QueryBucket(string queryHash, int maxSamples)
            {
                QueryHash = queryHash;
                _maxSamples = maxSamples;
            }

            public string QueryHash { get; }

            public void Add(QueryExecution execution)
            {
                var durationMs = execution.Duration.TotalMilliseconds;

                lock (_sync)
                {
                    _normalizedSql = execution.NormalizedSql ?? _normalizedSql;
                    _representativeSql = _representativeSql ?? execution.RepresentativeSql;

                    _count++;
                    _totalDurationMs += durationMs;
                    _minDurationMs = Math.Min(_minDurationMs, durationMs);
                    _maxDurationMs = Math.Max(_maxDurationMs, durationMs);

                    AddSample(durationMs);

                    if (execution.CpuTimeMs.HasValue)
                    {
                        _totalCpuMs += execution.CpuTimeMs.Value;
                        _cpuSampleCount++;
                    }

                    if (execution.LogicalReads.HasValue)
                    {
                        _totalLogicalReads += execution.LogicalReads.Value;
                        _logicalReadsSampleCount++;
                    }

                    if (execution.PhysicalReads.HasValue)
                    {
                        _totalPhysicalReads += execution.PhysicalReads.Value;
                        _physicalReadsSampleCount++;
                    }

                    if (execution.RowsReturned.HasValue)
                    {
                        _totalRowsReturned += execution.RowsReturned.Value;
                        _rowsReturnedSampleCount++;
                        _maxRowsReturned = Math.Max(_maxRowsReturned, execution.RowsReturned.Value);
                    }
                }
            }

            /// <summary>
            /// Standard reservoir sampling (algorithm R): once the cap is
            /// reached, each new sample has a shrinking chance of displacing an
            /// existing one, keeping the retained set representative without
            /// growing unbounded. Must be called while holding <see cref="_sync"/>.
            /// </summary>
            private void AddSample(double durationMs)
            {
                if (_durationSamplesMs.Count < _maxSamples)
                {
                    _durationSamplesMs.Add(durationMs);
                    return;
                }

                var upperBound = (int)Math.Min(_count, int.MaxValue);
                var replaceIndex = _random.Next(0, upperBound);
                if (replaceIndex < _maxSamples)
                {
                    _durationSamplesMs[replaceIndex] = durationMs;
                }
            }

            public QueryStatistics ToStatistics()
            {
                lock (_sync)
                {
                    var sorted = _durationSamplesMs.OrderBy(v => v).ToList();

                    return new QueryStatistics
                    {
                        QueryHash = QueryHash,
                        NormalizedSql = _normalizedSql,
                        RepresentativeSql = _representativeSql,
                        ExecutionCount = _count,
                        MinDurationMs = _count > 0 ? _minDurationMs : 0,
                        MaxDurationMs = _maxDurationMs,
                        TotalDurationMs = _totalDurationMs,
                        AverageDurationMs = _count > 0 ? _totalDurationMs / _count : 0,
                        P50DurationMs = PercentileCalculator.Percentile(sorted, 50),
                        P90DurationMs = PercentileCalculator.Percentile(sorted, 90),
                        P95DurationMs = PercentileCalculator.Percentile(sorted, 95),
                        P99DurationMs = PercentileCalculator.Percentile(sorted, 99),
                        AverageCpuMs = _cpuSampleCount > 0 ? _totalCpuMs / _cpuSampleCount : (double?)null,
                        AverageLogicalReads = _logicalReadsSampleCount > 0 ? _totalLogicalReads / _logicalReadsSampleCount : (double?)null,
                        AveragePhysicalReads = _physicalReadsSampleCount > 0 ? _totalPhysicalReads / _physicalReadsSampleCount : (double?)null,
                        AverageRowsReturned = _rowsReturnedSampleCount > 0 ? _totalRowsReturned / _rowsReturnedSampleCount : (double?)null,
                        MaxRowsReturned = _rowsReturnedSampleCount > 0 ? _maxRowsReturned : (long?)null
                    };
                }
            }
        }
    }
}
