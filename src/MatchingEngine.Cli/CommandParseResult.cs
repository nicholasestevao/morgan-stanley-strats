namespace MatchingEngine.Cli;

public enum CommandParseErrorCode
{
    EmptyInput,
    UnknownCommand,
    InvalidSyntax,
    InvalidSide,
    InvalidPegReference,
    InvalidPrice,
    InvalidQuantity,
    InvalidOrderId,
}

public sealed record CommandParseError(CommandParseErrorCode Code, string Field);

public sealed record CommandParseResult
{
    private CommandParseResult(CliCommand? command, CommandParseError? error)
    {
        Command = command;
        Error = error;
    }

    public bool IsSuccess => Error is null;

    public CliCommand? Command { get; }

    public CommandParseError? Error { get; }

    internal static CommandParseResult Success(CliCommand command) => new(command, null);

    internal static CommandParseResult Failure(CommandParseErrorCode code, string field) =>
        new(null, new CommandParseError(code, field));
}
