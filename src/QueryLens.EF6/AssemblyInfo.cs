using System.Runtime.CompilerServices;

// Lets QueryLens.EF6.Tests exercise the internal QueryLensEf6Interceptor
// directly, without making it part of the public API.
[assembly: InternalsVisibleTo("QueryLens.EF6.Tests")]
