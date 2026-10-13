using System.Collections.Generic;

namespace QueryLens.Cli.Commands
{
    public interface ICliCommand
    {
        string Name { get; }

        string Summary { get; }

        string Usage { get; }

        /// <summary>Runs the command. May throw <see cref="CliUsageException"/>; the host maps it to exit code 2.</summary>
        int Execute(IReadOnlyList<string> args, CliContext context);
    }
}
