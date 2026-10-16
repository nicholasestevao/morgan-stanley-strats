using MatchingEngine.Core.Domain;

namespace MatchingEngine.Cli;

public abstract record CliCommand
{
    private protected CliCommand() { }
}

public sealed record SubmitOrderCommand(SubmitRequest Request) : CliCommand;

public sealed record PrintBookCommand : CliCommand;
