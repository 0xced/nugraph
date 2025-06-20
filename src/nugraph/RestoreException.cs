using System;

namespace nugraph;

public class RestoreException : Exception
{
    internal static RestoreException Create(int exitCode, string workingDirectory, string command, string output)
    {
        if (output.Contains("MSB1001", StringComparison.OrdinalIgnoreCase))
        {
            return new RestoreException(exitCode: exitCode, error: "nugraph requires at least the .NET 8 SDK", recoverySuggestion: "Download the latest .NET SDK on https://get.dot.net");
        }

        const string runOnProject = "Please run nugraph in a directory that contains a single project file or pass an explicit project file as the first argument.";
        if (output.Contains("MSB1003", StringComparison.OrdinalIgnoreCase))
        {
            // MSB1003: Specify a project or solution file. The current working directory does not contain a project or solution file.
            return new RestoreException(exitCode: exitCode, error: "The current working directory does not contain a project file.", recoverySuggestion: runOnProject);
        }

        if (output.Contains("MSB1063", StringComparison.OrdinalIgnoreCase))
        {
            return new RestoreException(exitCode: exitCode, error: "Solution files are not supported.", recoverySuggestion: runOnProject);
        }

        return new RestoreException(exitCode: exitCode, workingDirectory: workingDirectory, command: command, output: output);
    }

    private RestoreException(int exitCode, string error, string recoverySuggestion) : base(string.Join(Environment.NewLine, error, recoverySuggestion))
    {
        ExitCode = exitCode;
        Error = error;
        RecoverySuggestion = recoverySuggestion;
    }

    private RestoreException(int exitCode, string workingDirectory, string command, string output) : base($"Running dotnet restore failed with exit code {exitCode}")
    {
        ExitCode = exitCode;
        WorkingDirectory = workingDirectory;
        Command = command;
        Output = output;
    }

    public int ExitCode { get; }

    public string? Error { get; }

    public string? RecoverySuggestion { get; }

    public string? WorkingDirectory { get; }

    public string? Command { get; }

    public string? Output { get; }
}