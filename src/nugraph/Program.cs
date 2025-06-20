using nugraph;
using System;
using System.Reflection;
using System.Threading;
using NuGet.Versioning;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;

// ReSharper disable AccessToDisposedClosure

var app = new CommandApp<GraphCommand>();
using var cancellationTokenSource = new CancellationTokenSource();

Console.CancelKeyPress += (_, eventArgs) =>
{
    // Try to cancel gracefully the first time, then abort the process the second time Ctrl+C is pressed
    eventArgs.Cancel = !cancellationTokenSource.IsCancellationRequested;
    cancellationTokenSource.Cancel();
};

app.Configure(config =>
{
    config.AddExample("spectre.console/src/Spectre.Console.Cli/Spectre.Console.Cli.csproj", "--include-version");
    config.AddExample("Serilog.Sinks.MSSqlServer", "--ignore", "Microsoft.Data.SqlClient");
    config.AddExample("Newtonsoft.Json/12.0.3", "--framework", "netstandard1.0");
    config.AddExample("Azure.Core", "--direction", "TopToBottom", "--output", "Azure.Core.gv");
    config.AddExample("Polly", "--format", "dot", "--title", "\"\"");
#if DEBUG
    config.ValidateExamples();
#endif
    var assembly = typeof(Program).Assembly;
    var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "N/A";
    config.SetApplicationVersion(SemanticVersion.TryParse(version, out var semanticVersion) ? semanticVersion.ToNormalizedString() : version);
    config.ConfigureConsole(RedirectionFriendlyConsole.Out);
    config.Settings.Registrar.RegisterInstance(cancellationTokenSource.Token);
    config.SetExceptionHandler((exception, _) =>
    {
        switch (exception)
        {
            case OperationCanceledException when cancellationTokenSource.IsCancellationRequested:
                return 130; // https://github.com/spectreconsole/spectre.console/issues/701#issuecomment-2342979700
            case Exception when ExceptionPrettifier.GetRenderable(exception) is IRenderable error:
                RedirectionFriendlyConsole.Error.Write(error);
                break;
            case CommandAppException commandAppException:
                RedirectionFriendlyConsole.Error.WriteLine(commandAppException.Message, Color.Red);
                break;
            default:
                RedirectionFriendlyConsole.Error.WriteException(exception, ExceptionFormats.ShortenPaths | ExceptionFormats.ShortenTypes);
                break;
        }

        if (exception is CommandAppException)
        {
            app.Run(["--help"]);
            return 64; // EX_USAGE -- The command was used incorrectly, e.g., with the wrong number of arguments, a bad flag, a bad syntax in a parameter, or whatever.
        }

        return 70; // EX_SOFTWARE -- An internal software error has been detected.
    });
});

return await app.RunAsync(args);