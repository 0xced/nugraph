using System;
using System.Globalization;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;

namespace nugraph;

public static class ExceptionPrettifier
{
    public static IRenderable? GetRenderable(Exception exception)
    {
        return exception switch
        {
            CommandAppException { Pretty: not null } commandAppException => commandAppException.Pretty,
            RestoreException { WorkingDirectory: not null, Command: not null, Output: not null } ex => new Rows(
                Markup.FromInterpolated(CultureInfo.InvariantCulture, $"Running [b]dotnet restore[/] failed with exit code {ex.ExitCode}", Color.Red),
                new Rule("📁 Working directory").LeftJustified(),
                new TextPath(ex.WorkingDirectory).RootStyle(Color.Grey).SeparatorStyle(Color.Grey).LeafStyle(Color.Blue),
                new Rule("🛠 Full dotnet restore command").LeftJustified(),
                new Text(ex.Command),
                new Rule("👇 Output from dotnet restore").LeftJustified(),
                new Text(ex.Output, Color.Grey)
            ),
            RestoreException { Error: not null, RecoverySuggestion: not null } ex => new Rows(
                new Text(ex.Error, new Style(Color.Red, decoration: Decoration.Bold)),
                new Text(ex.RecoverySuggestion)
            ),
            PackageNotFoundException ex => new Text(ex.Message, Color.Red),
            _ => null,
        };
    }
}