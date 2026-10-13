using System;
using System.Collections.Generic;

namespace QueryLens.Cli
{
    /// <summary>Minimal parsed command line: positional values plus <c>--name value</c> options.</summary>
    public sealed class CliArguments
    {
        private readonly Dictionary<string, string> _options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public List<string> Positionals { get; } = new List<string>();

        public bool TryGetOption(string name, out string value) => _options.TryGetValue(name, out value);

        public string GetOption(string name) => _options.TryGetValue(name, out var v) ? v : null;

        /// <summary>Parses args; every <c>--name</c> must be followed by a value. Throws <see cref="CliUsageException"/> otherwise.</summary>
        public static CliArguments Parse(IEnumerable<string> args, ICollection<string> allowedOptions)
        {
            var result = new CliArguments();
            var list = new List<string>(args);

            for (var i = 0; i < list.Count; i++)
            {
                var arg = list[i];
                if (!arg.StartsWith("--", StringComparison.Ordinal))
                {
                    result.Positionals.Add(arg);
                    continue;
                }

                var name = arg.Substring(2);
                string value = null;
                var eq = name.IndexOf('=');
                if (eq >= 0)
                {
                    value = name.Substring(eq + 1);
                    name = name.Substring(0, eq);
                }

                if (!Contains(allowedOptions, name))
                {
                    throw new CliUsageException($"Unknown option '--{name}'.");
                }

                if (value == null)
                {
                    if (i + 1 >= list.Count)
                    {
                        throw new CliUsageException($"Option '--{name}' requires a value.");
                    }

                    value = list[++i];
                }

                result._options[name] = value;
            }

            return result;
        }

        private static bool Contains(ICollection<string> allowed, string name)
        {
            foreach (var a in allowed)
            {
                if (string.Equals(a, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Raised for bad arguments or unusable input; mapped to <see cref="ExitCodes.UsageError"/>.</summary>
    public sealed class CliUsageException : Exception
    {
        public CliUsageException(string message) : base(message)
        {
        }
    }
}
