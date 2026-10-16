using Engine = MatchingEngine.Core.MatchingEngine;

namespace MatchingEngine.Cli;

public static class Program
{
    public static void Main() => new CommandLoop(new Engine()).Run(Console.In, Console.Out);
}
