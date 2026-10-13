namespace QueryLens.Cli.Reporting
{
    public sealed class JsonReportRenderer : IReportRenderer
    {
        public string Format => "json";

        public string Render(ReportDocument report) => ReportSerializer.Serialize(report);
    }
}
