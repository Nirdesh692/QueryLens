using System.Linq;
using QueryLens.SqlServer.ExecutionPlans;
using Xunit;

namespace QueryLens.SqlServer.Tests.ExecutionPlans
{
    // NOTE ON VALIDATION: unlike QueryStoreReader's SQL text, this parser has
    // no SQL Server dependency at all - it's pure LINQ-to-XML over a string.
    // Every assertion below (and the XML fixtures themselves) was compiled and
    // run in a throwaway .NET 8 harness before being committed here - 56
    // assertions covering operator parsing, the "unknown wrapper element"
    // child-RelOp search, object name extraction, missing-index parsing,
    // actual-vs-estimated row discrepancies, malformed/empty XML, and the
    // full rule engine. What's NOT independently provable without a real SQL
    // Server: whether these hand-written XML fixtures are a faithful match
    // for real ShowPlan XML output (they're written carefully against
    // Microsoft's documented, stable-since-2016 schema, but this project had
    // no live server to capture real plans from). If a real plan trips up
    // this parser, it's most likely an attribute or wrapper-element shape
    // this file's fixtures didn't happen to cover.
    public class ExecutionPlanParserTests
    {
        private readonly ExecutionPlanParser _parser = new ExecutionPlanParser();

        private const string Namespace = "xmlns=\"http://schemas.microsoft.com/sqlserver/2004/07/showplan\"";

        private static string Wrap(string queryPlanInner) => $@"
<ShowPlanXML {Namespace} Version=""1.539"" Build=""16.0.1000.6"">
  <BatchSequence><Batch><Statements><StmtSimple StatementText=""x"" StatementId=""1"">
    <QueryPlan>
      {queryPlanInner}
    </QueryPlan>
  </StmtSimple></Statements></Batch></BatchSequence>
</ShowPlanXML>";

        [Fact]
        public void Parse_BasicTableScan_ExtractsAllFields()
        {
            var xml = Wrap(@"
                <RelOp NodeId=""0"" PhysicalOp=""Table Scan"" LogicalOp=""Table Scan"" EstimateRows=""50000"" EstimateExecutions=""1"" EstimatedTotalSubtreeCost=""12.5"" Parallel=""false"">
                  <TableScan Ordered=""false""><Object Database=""[MyDb]"" Schema=""[dbo]"" Table=""[Orders]"" /></TableScan>
                </RelOp>");

            var plan = _parser.Parse(xml);

            Assert.NotNull(plan.Root);
            Assert.Equal("Table Scan", plan.Root.PhysicalOp);
            Assert.Equal(50000.0, plan.Root.EstimateRows);
            Assert.Equal(50000.0, plan.Root.EffectiveEstimatedRows);
            Assert.Equal("Orders", plan.Root.ObjectName);
            Assert.Single(plan.AllOperators);
        }

        [Fact]
        public void Parse_FindsChildRelOpsRegardlessOfWrapperElementName()
        {
            var xml = Wrap(@"
                <RelOp NodeId=""0"" PhysicalOp=""Hash Match"" LogicalOp=""Inner Join"" EstimateRows=""20000"" EstimateExecutions=""1"" EstimatedTotalSubtreeCost=""45.0"" Parallel=""false"">
                  <Hash>
                    <RelOp NodeId=""1"" PhysicalOp=""Clustered Index Scan"" LogicalOp=""Clustered Index Scan"" EstimateRows=""15000"" EstimateExecutions=""1"" EstimatedTotalSubtreeCost=""10.0"" Parallel=""false"">
                      <IndexScan Ordered=""false""><Object Database=""[MyDb]"" Schema=""[dbo]"" Table=""[Orders]"" Index=""[PK_Orders]"" /></IndexScan>
                    </RelOp>
                    <RelOp NodeId=""2"" PhysicalOp=""Index Seek"" LogicalOp=""Index Seek"" EstimateRows=""1"" EstimateExecutions=""1"" EstimatedTotalSubtreeCost=""0.01"" Parallel=""false"">
                      <IndexScan Ordered=""true""><Object Database=""[MyDb]"" Schema=""[dbo]"" Table=""[Customers]"" Index=""[IX_Customers_Id]"" /></IndexScan>
                    </RelOp>
                  </Hash>
                </RelOp>");

            var plan = _parser.Parse(xml);

            Assert.Equal(2, plan.Root.Children.Count);
            Assert.Equal("Clustered Index Scan", plan.Root.Children[0].PhysicalOp);
            Assert.Equal("Orders.PK_Orders", plan.Root.Children[0].ObjectName);
            Assert.Equal("Index Seek", plan.Root.Children[1].PhysicalOp);
            Assert.Equal(3, plan.AllOperators.Count);
        }

        [Fact]
        public void Parse_KeyLookupUnderNestedLoops_ComputesEffectiveRowsFromExecutions()
        {
            var xml = Wrap(@"
                <RelOp NodeId=""0"" PhysicalOp=""Nested Loops"" LogicalOp=""Inner Join"" EstimateRows=""15000"" EstimateExecutions=""1"" EstimatedTotalSubtreeCost=""30.0"" Parallel=""false"">
                  <NestedLoops>
                    <RelOp NodeId=""1"" PhysicalOp=""Index Seek"" EstimateRows=""15000"" EstimateExecutions=""1"" EstimatedTotalSubtreeCost=""5.0"" Parallel=""false"" />
                    <RelOp NodeId=""2"" PhysicalOp=""Key Lookup"" EstimateRows=""1"" EstimateExecutions=""15000"" EstimatedTotalSubtreeCost=""25.0"" Parallel=""false"" />
                  </NestedLoops>
                </RelOp>");

            var plan = _parser.Parse(xml);
            var keyLookup = plan.AllOperators.Single(o => o.PhysicalOp == "Key Lookup");

            Assert.Equal("Index Seek", plan.Root.Children[0].PhysicalOp); // outer
            Assert.Equal("Key Lookup", plan.Root.Children[1].PhysicalOp); // inner
            Assert.Equal(15000.0, keyLookup.EstimateExecutions);
            Assert.Equal(15000.0, keyLookup.EffectiveEstimatedRows); // 1 row * 15000 executions
        }

        [Fact]
        public void Parse_MissingIndexGroup_ExtractsColumnsByUsage()
        {
            var xml = Wrap(@"
                <MissingIndexes>
                  <MissingIndexGroup Impact=""85.2"">
                    <MissingIndex Database=""[MyDb]"" Schema=""[dbo]"" Table=""[Orders]"">
                      <ColumnGroup Usage=""EQUALITY""><Column Name=""[CustomerId]"" ColumnId=""2"" /></ColumnGroup>
                      <ColumnGroup Usage=""INEQUALITY""><Column Name=""[Status]"" ColumnId=""5"" /></ColumnGroup>
                      <ColumnGroup Usage=""INCLUDE""><Column Name=""[Total]"" ColumnId=""4"" /></ColumnGroup>
                    </MissingIndex>
                  </MissingIndexGroup>
                </MissingIndexes>
                <RelOp NodeId=""0"" PhysicalOp=""Table Scan"" EstimateRows=""100"" EstimateExecutions=""1"" EstimatedTotalSubtreeCost=""1.0"" Parallel=""false"" />");

            var plan = _parser.Parse(xml);
            var suggestion = Assert.Single(plan.MissingIndexSuggestions);

            Assert.Equal("Orders", suggestion.Table);
            Assert.Equal(85.2, suggestion.Impact);
            Assert.Equal("CustomerId", Assert.Single(suggestion.EqualityColumns));
            Assert.Equal("Status", Assert.Single(suggestion.InequalityColumns));
            Assert.Equal("Total", Assert.Single(suggestion.IncludedColumns));
        }

        [Fact]
        public void Parse_ActualRowsFromRunTimeInformation_SumsAcrossThreads()
        {
            var xml = Wrap(@"
                <RelOp NodeId=""0"" PhysicalOp=""Index Seek"" EstimateRows=""10"" EstimateExecutions=""1"" EstimatedTotalSubtreeCost=""1.0"" Parallel=""true"">
                  <RunTimeInformation>
                    <RunTimeCountersPerThread Thread=""1"" ActualRows=""300000"" />
                    <RunTimeCountersPerThread Thread=""2"" ActualRows=""200000"" />
                  </RunTimeInformation>
                </RelOp>");

            var plan = _parser.Parse(xml);

            Assert.Equal(500000.0, plan.Root.ActualRows);
        }

        [Fact]
        public void Parse_NoRunTimeInformation_LeavesActualRowsNull()
        {
            var xml = Wrap(@"<RelOp NodeId=""0"" PhysicalOp=""Index Seek"" EstimateRows=""10"" EstimateExecutions=""1"" EstimatedTotalSubtreeCost=""1.0"" Parallel=""false"" />");

            var plan = _parser.Parse(xml);

            Assert.Null(plan.Root.ActualRows);
        }

        [Fact]
        public void Parse_MalformedXml_ThrowsExecutionPlanParseException()
        {
            Assert.Throws<ExecutionPlanParseException>(() => _parser.Parse("<ShowPlanXML><Unclosed>"));
        }

        [Fact]
        public void Parse_NullOrEmptyXml_ReturnsEmptyPlanWithoutThrowing()
        {
            var fromNull = _parser.Parse(null);
            var fromEmpty = _parser.Parse("");

            Assert.Null(fromNull.Root);
            Assert.Empty(fromNull.AllOperators);
            Assert.Null(fromEmpty.Root);
        }

        [Fact]
        public void Parse_ParallelAttribute_IsExtracted()
        {
            var xml = Wrap(@"<RelOp NodeId=""0"" PhysicalOp=""Hash Match"" EstimateRows=""100"" EstimateExecutions=""1"" EstimatedTotalSubtreeCost=""5.0"" Parallel=""true"" />");

            var plan = _parser.Parse(xml);

            Assert.True(plan.Root.Parallel);
        }
    }
}
