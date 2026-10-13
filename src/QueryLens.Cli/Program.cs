using System;

namespace QueryLens.Cli
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            return new CliApp().Run(args, Console.Out, Console.Error);
        }
    }
}
