namespace QueryLens.SqlServer.ExecutionPlans
{
    public interface IExecutionPlanParser
    {
        ExecutionPlan Parse(string planXml);
    }
}
