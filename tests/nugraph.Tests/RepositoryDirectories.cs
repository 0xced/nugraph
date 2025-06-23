using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace nugraph.Tests;

public static class RepositoryDirectories
{
    public static DirectoryInfo GetDirectory(params string[] paths) => new(GetPath(paths));

    public static FileInfo GetFile(params string[] paths) => new(GetPath(paths));

    public static string GetPath(params string[] paths) => Path.GetFullPath(Path.Combine(new[] { GetThisDirectory(), "..", ".." }.Concat(paths).ToArray()));

    private static string GetThisDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
}