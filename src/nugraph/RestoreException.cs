using System;

namespace nugraph;

public class RestoreException : Exception
{
    public RestoreException(int exitCode, string error, string recoverySuggestion) : base(string.Join(Environment.NewLine, error, recoverySuggestion))
    {
        ExitCode = exitCode;
        Error = error;
        RecoverySuggestion = recoverySuggestion;
    }

    public RestoreException(int exitCode, string workingDirectory, string command, string output) : base($"Running dotnet restore failed with exit code {exitCode}")
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