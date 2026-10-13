namespace QueryLens.Core.Models
{
    /// <summary>
    /// Metadata about one command parameter - name, type, and size only. There is
    /// deliberately no Value property: capturing parameter values by default would
    /// risk capturing sensitive data, which QueryLens opts out of unless a capture
    /// adapter is explicitly configured to do otherwise (see
    /// <see cref="Configuration.CaptureOptions.Parameters"/> and
    /// <see cref="Configuration.PrivacyOptions.CaptureParameterValues"/>). Leaving
    /// Value out of the type entirely is a stronger guarantee than a runtime check.
    /// </summary>
    public sealed class QueryParameter
    {
        public string Name { get; set; }

        public string DbType { get; set; }

        public int Size { get; set; }
    }
}
