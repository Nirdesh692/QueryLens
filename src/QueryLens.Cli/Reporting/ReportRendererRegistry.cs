using System;
using System.Collections.Generic;
using System.Linq;

namespace QueryLens.Cli.Reporting
{
    /// <summary>Looks up <see cref="IReportRenderer"/>s by format name.</summary>
    public sealed class ReportRendererRegistry
    {
        /// <summary>Formats the CLI advertises even when no renderer is registered yet.</summary>
        public static readonly string[] PlannedFormats = { "json", "html" };

        private readonly Dictionary<string, IReportRenderer> _renderers =
            new Dictionary<string, IReportRenderer>(StringComparer.OrdinalIgnoreCase);

        public static ReportRendererRegistry CreateDefault()
        {
            var registry = new ReportRendererRegistry();
            registry.Register(new JsonReportRenderer());
            registry.Register(new HtmlReportRenderer());
            return registry;
        }

        public void Register(IReportRenderer renderer)
        {
            if (renderer == null) throw new ArgumentNullException(nameof(renderer));
            _renderers[renderer.Format] = renderer;
        }

        public bool TryGet(string format, out IReportRenderer renderer) => _renderers.TryGetValue(format, out renderer);

        public bool IsPlanned(string format) =>
            PlannedFormats.Contains(format, StringComparer.OrdinalIgnoreCase);
    }
}
