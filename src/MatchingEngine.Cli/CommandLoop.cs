using MatchingEngine.Core.Domain;
using Engine = MatchingEngine.Core.MatchingEngine;

namespace MatchingEngine.Cli;

public sealed class CommandLoop(Engine engine)
{
    public void Run(TextReader input, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        string? line;
        while ((line = input.ReadLine()) is not null)
            Execute(line, output);
    }

    private void Execute(string line, TextWriter output)
    {
        var parsed = CommandParser.Parse(line);
        if (!parsed.IsSuccess)
        {
            output.WriteLine(OutputFormatter.FormatError(parsed.Error!));
            return;
        }

        switch (parsed.Command)
        {
            case SubmitOrderCommand submit:
                Submit(submit.Request, output);
                break;
            case CancelOrderCommand cancel:
                Cancel(cancel.Request, output);
                break;
            case AmendOrderCommand amend:
                Amend(amend.Request, output);
                break;
            case PrintBookCommand:
                WriteLines(OutputFormatter.FormatBook(engine.GetSnapshot()), output);
                break;
        }
    }

    private void Cancel(CancelRequest request, TextWriter output)
    {
        var result = engine.Cancel(request);
        if (!result.IsSuccess)
        {
            output.WriteLine(OutputFormatter.FormatError(result.Error!));
            return;
        }

        output.WriteLine(OutputFormatter.FormatCancellation(result.Value!));
    }

    private void Amend(AmendRequest request, TextWriter output)
    {
        var result = engine.Amend(request);
        if (!result.IsSuccess)
        {
            output.WriteLine(OutputFormatter.FormatError(result.Error!));
            return;
        }

        WriteLines(OutputFormatter.FormatAmendment(result.Value!), output);
    }

    private void Submit(SubmitRequest request, TextWriter output)
    {
        var result = engine.Submit(request);
        if (!result.IsSuccess)
        {
            output.WriteLine(OutputFormatter.FormatError(result.Error!));
            return;
        }

        WriteLines(OutputFormatter.FormatSubmission(result.Value!), output);
    }

    private static void WriteLines(IEnumerable<string> lines, TextWriter output)
    {
        foreach (var line in lines) output.WriteLine(line);
    }
}
