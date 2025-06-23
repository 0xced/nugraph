using System;
using System.Globalization;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;

namespace nugraph;

public static class ExceptionTransformer
{
    public static (int? ExitCode, IRenderable Pretty)? GetError(Exception exception)
    {
        return exception switch
        {
            CommandAppException { Pretty: not null } commandAppException => (null, commandAppException.Pretty),
            // EX_DATAERR -- The input data was incorrect in some way. This should only be used for user's data and not system files.
            RestoreException { WorkingDirectory: not null, Command: not null, Output: not null } ex => (65, new Rows(
                Markup.FromInterpolated(CultureInfo.InvariantCulture, $"Running [b]dotnet restore[/] failed with exit code {ex.ExitCode}", Color.Red),
                new Rule("📁 Working directory").LeftJustified(),
                new TextPath(ex.WorkingDirectory).RootStyle(Color.Grey).SeparatorStyle(Color.Grey).LeafStyle(Color.Blue),
                new Rule("🛠 Full dotnet restore command").LeftJustified(),
                new Text(ex.Command),
                new Rule("👇 Output from dotnet restore").LeftJustified(),
                new Text(ex.Output, Color.Grey)
            )),
            RestoreException { Error: not null, RecoverySuggestion: not null } ex => (65, new Rows(
                new Text(ex.Error, new Style(Color.Red, decoration: Decoration.Bold)),
                new Text(ex.RecoverySuggestion)
            )),
            // EX_NOINPUT -- An input file (not a system file) did not exist or was not readable. This could also include errors like “No message” to a mailer (if it cared to catch it).
            PackageNotFoundException ex => (66, new Text(ex.Message, Color.Red)),
            _ => null,
        };
    }
}