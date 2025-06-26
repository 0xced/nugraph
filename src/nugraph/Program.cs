using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Versioning;
using Spectre.Console;
using Spectre.Console.Cli;

namespace nugraph;

public record ProgramEnvironment(DirectoryInfo CurrentWorkingDirectory, IAnsiConsole ConsoleOut, IAnsiConsole ConsoleErr, TextWriter StdOut);

public class Program(ProgramEnvironment environment)
{
    public Program() : this(new ProgramEnvironment(new DirectoryInfo(Environment.CurrentDirectory), RedirectionFriendlyConsole.Out, RedirectionFriendlyConsole.Error, Console.Out))
    {
    }

    public static async Task<int> Main(string[] args)
    {
        var program = new Program();
        return await program.RunAsync(args);
    }

    public async Task<int> RunAsync(string[] args)
    {
        var app = new CommandApp<GraphCommand>();
        using var cancellationTokenSource = new CancellationTokenSource();

        // ReSharper disable AccessToDisposedClosure
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            // Try to cancel gracefully the first time, then abort the process the second time Ctrl+C is pressed
            eventArgs.Cancel = !cancellationTokenSource.IsCancellationRequested;
            cancellationTokenSource.Cancel();
        };

        app.Configure(config =>
        {
            config.AddExample("spectre.console/src/Spectre.Console.Cli/Spectre.Console.Cli.csproj", "--include-version");
            config.AddExample("Serilog.Sinks.MSSqlServer", "--ignore", "Microsoft.Data.SqlClient", "--ignore", "\"System.*\"");
            config.AddExample("Newtonsoft.Json/12.0.3", "--framework", "netstandard1.0");
            config.AddExample("Azure.Core", "--direction", "TopToBottom", "--output", "Azure.Core.gv");
            config.AddExample("Polly", "--format", "dot", "--title", "\"\"");
#if DEBUG
            config.ValidateExamples();
#endif
            var assembly = typeof(Program).Assembly;
            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "N/A";
            config.SetApplicationName(OperatingSystem.IsWindows() ? "nugraph.exe" : "nugraph");
            config.SetApplicationVersion(SemanticVersion.TryParse(version, out var semanticVersion) ? semanticVersion.ToNormalizedString() : version);
            config.ConfigureConsole(environment.ConsoleOut);
            config.Settings.Registrar.RegisterInstance(environment);
            config.Settings.Registrar.RegisterInstance(cancellationTokenSource.Token);
            config.SetExceptionHandler((exception, _) =>
            {
                var consoleErr = environment.ConsoleErr;
                switch (exception)
                {
                    case OperationCanceledException when cancellationTokenSource.IsCancellationRequested:
                        return 130; // https://github.com/spectreconsole/spectre.console/issues/701#issuecomment-2342979700
                    case Exception when ExceptionTransformer.GetError(exception) is { } error:
                        consoleErr.Write(error.Pretty);
                        if (error.ExitCode.HasValue)
                        {
                            return error.ExitCode.Value;
                        }

                        break;
                    case CommandAppException commandAppException:
                        consoleErr.WriteLine(commandAppException.Message, Color.Red);
                        break;
                    default:
                        consoleErr.WriteLine("An unexpected error has occurred.", new Style(Color.Red, decoration: Decoration.Bold));
                        consoleErr.WriteLine("Please file a bug report on https://github.com/0xced/nugraph/issues/new and include the stack trace below along with instructions to reproduce this issue.");
                        consoleErr.Write(new Rule());
                        consoleErr.WriteException(exception, ExceptionFormats.ShortenTypes);
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
    }
}