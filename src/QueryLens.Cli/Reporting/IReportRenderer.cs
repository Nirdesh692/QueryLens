namespace QueryLens.Cli.Reporting
{
    /// <summary>
    /// Renders a report in one output format. Phase 9 plugs an "html" renderer
    /// in here (register it in <see cref="ReportRendererRegistry"/>); the
    /// <c>report</c> command needs no other change.
    /// </summary>
    public interface IReportRenderer
    {
        /// <summary>Format name as given to <c>--format</c>, e.g. "json".</summary>
        string Format { get; }

        string Render(ReportDocument report);
    }
}
