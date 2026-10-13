using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace QueryLens.SqlServer.ExecutionPlans
{
    /// <summary>
    /// Parses SQL Server "ShowPlan XML" (the format used by Query Store's
    /// query_plan column, SET SHOWPLAN_XML, and SSMS's "Show Execution Plan").
    ///
    /// Deliberately matches elements by local name rather than requiring an
    /// exact namespace/version match, and walks the tree defensively (e.g.
    /// finding child RelOps by searching descendants and stopping at the
    /// first nested RelOp, rather than assuming a specific wrapper element
    /// name like &lt;Hash&gt; or &lt;NestedLoops&gt;) - ShowPlan XML has had
    /// version bumps over the years and this project had no live SQL Server
    /// to confirm every attribute name against. See the root README's
    /// testing notes for exactly what was and wasn't validated.
    /// </summary>
    public sealed class ExecutionPlanParser : IExecutionPlanParser
    {
        public ExecutionPlan Parse(string planXml)
        {
            if (string.IsNullOrWhiteSpace(planXml))
            {
                return new ExecutionPlan();
            }

            XDocument document;
            try
            {
                document = XDocument.Parse(planXml);
            }
            catch (XmlException ex)
            {
                throw new ExecutionPlanParseException("The execution plan XML could not be parsed.", ex);
            }

            var rootElement = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "RelOp");
            var root = rootElement != null ? ParseOperator(rootElement) : null;

            var allOperators = new List<PlanOperator>();
            if (root != null)
            {
                Flatten(root, allOperators);
            }

            return new ExecutionPlan
            {
                Root = root,
                AllOperators = allOperators,
                MissingIndexSuggestions = ParseMissingIndexes(document)
            };
        }

        private static PlanOperator ParseOperator(XElement relOp)
        {
            var op = new PlanOperator
            {
                PhysicalOp = (string)relOp.Attribute("PhysicalOp"),
                LogicalOp = (string)relOp.Attribute("LogicalOp"),
                EstimateRows = ParseDouble(relOp.Attribute("EstimateRows")),
                EstimateExecutions = ParseDouble(relOp.Attribute("EstimateExecutions"), 1),
                EstimatedTotalSubtreeCost = ParseDouble(relOp.Attribute("EstimatedTotalSubtreeCost")),
                Parallel = ParseBool(relOp.Attribute("Parallel")),
                ObjectName = ParseObjectName(relOp),
                ActualRows = ParseActualRows(relOp),
                Children = FindChildRelOps(relOp).Select(ParseOperator).ToList()
            };

            return op;
        }

        /// <summary>
        /// Finds this operator's direct child RelOp elements regardless of what
        /// operator-specific wrapper element (Hash, NestedLoops, Sort, ...) sits
        /// between them - recurses into non-RelOp children only, and stops
        /// descending as soon as a RelOp is found (that RelOp's own children are
        /// its concern, handled when we recurse into it separately).
        /// </summary>
        private static IEnumerable<XElement> FindChildRelOps(XElement parent)
        {
            foreach (var child in parent.Elements())
            {
                if (child.Name.LocalName == "RelOp")
                {
                    yield return child;
                }
                else
                {
                    foreach (var nested in FindChildRelOps(child))
                    {
                        yield return nested;
                    }
                }
            }
        }

        /// <summary>
        /// The table/index this operator touches, from its &lt;Object&gt; child -
        /// walks descendants in document order and stops at the first nested
        /// RelOp, so a child operator's Object is never misattributed to this one.
        /// </summary>
        private static string ParseObjectName(XElement relOp)
        {
            foreach (var descendant in relOp.Descendants())
            {
                if (descendant.Name.LocalName == "RelOp")
                {
                    break;
                }

                if (descendant.Name.LocalName == "Object")
                {
                    var table = CleanBrackets((string)descendant.Attribute("Table"));
                    var index = CleanBrackets((string)descendant.Attribute("Index"));
                    if (table == null)
                    {
                        return null;
                    }

                    return index != null ? $"{table}.{index}" : table;
                }
            }

            return null;
        }

        /// <summary>
        /// Sums ActualRows across RunTimeCountersPerThread entries (parallel
        /// plans report one per thread). Only present on *actual* execution
        /// plans - null for compile-time estimated plans (the common case for
        /// Query Store).
        /// </summary>
        private static double? ParseActualRows(XElement relOp)
        {
            var runtimeInfo = relOp.Elements().FirstOrDefault(e => e.Name.LocalName == "RunTimeInformation");
            if (runtimeInfo == null)
            {
                return null;
            }

            var counters = runtimeInfo.Elements().Where(e => e.Name.LocalName == "RunTimeCountersPerThread").ToList();
            if (counters.Count == 0)
            {
                return null;
            }

            return counters.Sum(c => ParseDouble(c.Attribute("ActualRows")));
        }

        private static IReadOnlyList<MissingIndexSuggestion> ParseMissingIndexes(XDocument document)
        {
            var results = new List<MissingIndexSuggestion>();

            foreach (var group in document.Descendants().Where(e => e.Name.LocalName == "MissingIndexGroup"))
            {
                var impact = ParseDouble(group.Attribute("Impact"));
                var missingIndex = group.Elements().FirstOrDefault(e => e.Name.LocalName == "MissingIndex");
                if (missingIndex == null)
                {
                    continue;
                }

                var equality = new List<string>();
                var inequality = new List<string>();
                var include = new List<string>();

                foreach (var columnGroup in missingIndex.Elements().Where(e => e.Name.LocalName == "ColumnGroup"))
                {
                    var usage = (string)columnGroup.Attribute("Usage");
                    var columns = columnGroup.Elements()
                        .Where(e => e.Name.LocalName == "Column")
                        .Select(c => CleanBrackets((string)c.Attribute("Name")))
                        .Where(c => c != null)
                        .ToList();

                    switch (usage)
                    {
                        case "EQUALITY":
                            equality.AddRange(columns);
                            break;
                        case "INEQUALITY":
                            inequality.AddRange(columns);
                            break;
                        case "INCLUDE":
                            include.AddRange(columns);
                            break;
                    }
                }

                results.Add(new MissingIndexSuggestion
                {
                    Database = CleanBrackets((string)missingIndex.Attribute("Database")),
                    Schema = CleanBrackets((string)missingIndex.Attribute("Schema")),
                    Table = CleanBrackets((string)missingIndex.Attribute("Table")),
                    Impact = impact,
                    EqualityColumns = equality,
                    InequalityColumns = inequality,
                    IncludedColumns = include
                });
            }

            return results;
        }

        private static void Flatten(PlanOperator op, List<PlanOperator> into)
        {
            into.Add(op);
            foreach (var child in op.Children)
            {
                Flatten(child, into);
            }
        }

        private static double ParseDouble(XAttribute attribute, double defaultValue = 0)
        {
            if (attribute == null)
            {
                return defaultValue;
            }

            return double.TryParse(attribute.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : defaultValue;
        }

        private static bool ParseBool(XAttribute attribute) =>
            attribute != null && bool.TryParse(attribute.Value, out var value) && value;

        private static string CleanBrackets(string value) => value?.Trim('[', ']');
    }
}
