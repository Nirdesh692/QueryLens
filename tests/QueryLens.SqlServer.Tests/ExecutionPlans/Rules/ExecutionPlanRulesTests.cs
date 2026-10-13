using System.Collections.Generic;
using System.Linq;
using QueryLens.Core.Configuration;
using QueryLens.Core.Models;
using QueryLens.SqlServer.ExecutionPlans;
using QueryLens.SqlServer.ExecutionPlans.Rules;
using Xunit;

namespace QueryLens.SqlServer.Tests.ExecutionPlans.Rules
{
    public class LargeScanRuleTests
    {
        [Theory]
        [InlineData("Table Scan")]
        [InlineData("Clustered Index Scan")]
        [InlineData("Index Scan")]
        public void LargeScan_AboveThreshold_Fires(string physicalOp)
        {
            var plan = PlanWith(new PlanOperator { PhysicalOp = physicalOp, EstimateRows = 50000, ObjectName = "Orders" });

            var findings = new LargeScanRule().Evaluate(plan, "H1", new QueryLensOptions()).ToList();

            var finding = Assert.Single(findings);
            Assert.Equal("LARGESCAN001", finding.RuleId);
        }

        [Fact]
        public void SmallScan_BelowThreshold_DoesNotFire()
        {
            var plan = PlanWith(new PlanOperator { PhysicalOp = "Table Scan", EstimateRows = 10 });

            Assert.Empty(new LargeScanRule().Evaluate(plan, "H1", new QueryLensOptions()));
        }

        [Fact]
        public void IndexSeek_NeverFires()
        {
            var plan = PlanWith(new PlanOperator { PhysicalOp = "Index Seek", EstimateRows = 1_000_000 });

            Assert.Empty(new LargeScanRule().Evaluate(plan, "H1", new QueryLensOptions()));
        }

        private static ExecutionPlan PlanWith(PlanOperator op) => new ExecutionPlan { Root = op, AllOperators = new[] { op } };
    }

    public class KeyLookupRuleTests
    {
        [Fact]
        public void FrequentKeyLookup_Fires()
        {
            var op = new PlanOperator { PhysicalOp = "Key Lookup", EstimateRows = 1, EstimateExecutions = 5000 };
            var plan = new ExecutionPlan { Root = op, AllOperators = new[] { op } };

            var findings = new KeyLookupRule().Evaluate(plan, "H1", new QueryLensOptions()).ToList();

            var finding = Assert.Single(findings);
            Assert.Equal("KEYLOOKUP001", finding.RuleId);
        }

        [Fact]
        public void InfrequentKeyLookup_DoesNotFire()
        {
            var op = new PlanOperator { PhysicalOp = "Key Lookup", EstimateRows = 1, EstimateExecutions = 2 };
            var plan = new ExecutionPlan { Root = op, AllOperators = new[] { op } };

            Assert.Empty(new KeyLookupRule().Evaluate(plan, "H1", new QueryLensOptions()));
        }
    }

    public class ExpensiveSortRuleTests
    {
        [Fact]
        public void LargeSort_Fires()
        {
            var op = new PlanOperator { PhysicalOp = "Sort", EstimateRows = 2_400_000 };
            var plan = new ExecutionPlan { Root = op, AllOperators = new[] { op } };

            var finding = Assert.Single(new ExpensiveSortRule().Evaluate(plan, "H1", new QueryLensOptions()));
            Assert.Contains("2,400,000", finding.Description);
        }
    }

    public class ExpensiveHashMatchRuleTests
    {
        [Fact]
        public void LargeHashMatch_Fires()
        {
            var op = new PlanOperator { PhysicalOp = "Hash Match", LogicalOp = "Inner Join", EstimateRows = 20000 };
            var plan = new ExecutionPlan { Root = op, AllOperators = new[] { op } };

            Assert.Single(new ExpensiveHashMatchRule().Evaluate(plan, "H1", new QueryLensOptions()));
        }
    }

    public class NestedLoopsRuleTests
    {
        [Fact]
        public void LargeOuterInput_Fires()
        {
            var outer = new PlanOperator { PhysicalOp = "Index Seek", EstimateRows = 15000 };
            var inner = new PlanOperator { PhysicalOp = "Key Lookup", EstimateRows = 1, EstimateExecutions = 15000 };
            var loops = new PlanOperator { PhysicalOp = "Nested Loops", Children = new List<PlanOperator> { outer, inner } };
            var plan = new ExecutionPlan { Root = loops, AllOperators = new[] { loops, outer, inner } };

            var finding = Assert.Single(new NestedLoopsRule().Evaluate(plan, "H1", new QueryLensOptions()));
            Assert.Equal("NESTEDLOOPSLARGE001", finding.RuleId);
        }

        [Fact]
        public void SmallOuterInput_DoesNotFire()
        {
            var outer = new PlanOperator { PhysicalOp = "Index Seek", EstimateRows = 2 };
            var inner = new PlanOperator { PhysicalOp = "Key Lookup", EstimateRows = 1 };
            var loops = new PlanOperator { PhysicalOp = "Nested Loops", Children = new List<PlanOperator> { outer, inner } };
            var plan = new ExecutionPlan { Root = loops, AllOperators = new[] { loops, outer, inner } };

            Assert.Empty(new NestedLoopsRule().Evaluate(plan, "H1", new QueryLensOptions()));
        }
    }

    public class SpoolRuleTests
    {
        [Fact]
        public void LargeSpool_Fires()
        {
            var op = new PlanOperator { PhysicalOp = "Table Spool", EstimateRows = 20000 };
            var plan = new ExecutionPlan { Root = op, AllOperators = new[] { op } };

            var finding = Assert.Single(new SpoolRule().Evaluate(plan, "H1", new QueryLensOptions()));
            Assert.Equal("SPOOL001", finding.RuleId);
        }
    }

    public class ParallelismRuleTests
    {
        [Fact]
        public void ParallelOperator_ProducesOneInfoFinding()
        {
            var op = new PlanOperator { PhysicalOp = "Hash Match", Parallel = true };
            var plan = new ExecutionPlan { Root = op, AllOperators = new[] { op } };

            var finding = Assert.Single(new ParallelismRule().Evaluate(plan, "H1", new QueryLensOptions()));
            Assert.Equal(Severity.Info, finding.Severity);
        }

        [Fact]
        public void NoParallelOperators_ProducesNoFinding()
        {
            var op = new PlanOperator { PhysicalOp = "Index Seek", Parallel = false };
            var plan = new ExecutionPlan { Root = op, AllOperators = new[] { op } };

            Assert.Empty(new ParallelismRule().Evaluate(plan, "H1", new QueryLensOptions()));
        }
    }

    public class RowCountDiscrepancyRuleTests
    {
        [Fact]
        public void LargeDiscrepancy_Fires()
        {
            var op = new PlanOperator { PhysicalOp = "Index Seek", EstimateRows = 10, ActualRows = 500000 };
            var plan = new ExecutionPlan { Root = op, AllOperators = new[] { op } };

            var finding = Assert.Single(new RowCountDiscrepancyRule().Evaluate(plan, "H1", new QueryLensOptions()));
            Assert.Equal("50000.0", finding.Evidence["ratio"]);
        }

        [Fact]
        public void NoActualRows_DoesNotFire()
        {
            var op = new PlanOperator { PhysicalOp = "Index Seek", EstimateRows = 10, ActualRows = null };
            var plan = new ExecutionPlan { Root = op, AllOperators = new[] { op } };

            Assert.Empty(new RowCountDiscrepancyRule().Evaluate(plan, "H1", new QueryLensOptions()));
        }

        [Fact]
        public void SmallDiscrepancy_DoesNotFire()
        {
            var op = new PlanOperator { PhysicalOp = "Index Seek", EstimateRows = 100, ActualRows = 150 };
            var plan = new ExecutionPlan { Root = op, AllOperators = new[] { op } };

            Assert.Empty(new RowCountDiscrepancyRule().Evaluate(plan, "H1", new QueryLensOptions()));
        }
    }

    public class MissingIndexRuleTests
    {
        [Fact]
        public void HighImpactSuggestion_IsWarning()
        {
            var plan = new ExecutionPlan
            {
                MissingIndexSuggestions = new[]
                {
                    new MissingIndexSuggestion
                    {
                        Table = "Orders",
                        Impact = 85.2,
                        EqualityColumns = new[] { "CustomerId" },
                        InequalityColumns = new[] { "Status" },
                        IncludedColumns = new[] { "Total" }
                    }
                }
            };

            var finding = Assert.Single(new MissingIndexRule().Evaluate(plan, "H1", new QueryLensOptions()));
            Assert.Equal(Severity.Warning, finding.Severity);
            Assert.Contains("Orders", finding.Description);
            Assert.Contains("CustomerId", finding.Description);
        }

        [Fact]
        public void LowImpactSuggestion_IsInfo()
        {
            var plan = new ExecutionPlan
            {
                MissingIndexSuggestions = new[]
                {
                    new MissingIndexSuggestion { Table = "Orders", Impact = 15, EqualityColumns = new[] { "Status" } }
                }
            };

            var finding = Assert.Single(new MissingIndexRule().Evaluate(plan, "H1", new QueryLensOptions()));
            Assert.Equal(Severity.Info, finding.Severity);
        }
    }

    public class ExecutionPlanRuleEngineTests
    {
        [Fact]
        public void CreateDefault_RunsAllRulesAndRespectsDisabledToggle()
        {
            var scan = new PlanOperator { PhysicalOp = "Table Scan", EstimateRows = 50000, ObjectName = "Orders", Parallel = true };
            var plan = new ExecutionPlan { Root = scan, AllOperators = new[] { scan } };

            var options = new QueryLensOptions();
            var findings = ExecutionPlanRuleEngine.CreateDefault().Evaluate(plan, "H1", options);

            Assert.Contains(findings, f => f.RuleId == "LARGESCAN001");
            Assert.Contains(findings, f => f.RuleId == "PARALLELISM001");

            options.Analysis.ExecutionPlan = false;
            Assert.Empty(ExecutionPlanRuleEngine.CreateDefault().Evaluate(plan, "H1", options));
        }
    }
}
