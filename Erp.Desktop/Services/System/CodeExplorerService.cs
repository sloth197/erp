using System.IO;

namespace Erp.Desktop.Services;

public sealed class CodeExplorerService : ICodeExplorerService
{
    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".vs",
        ".vscode",
        "bin",
        "obj",
        "node_modules",
        "packages"
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs",
        ".xaml",
        ".json",
        ".yml",
        ".yaml",
        ".md",
        ".csproj",
        ".sln",
        ".props",
        ".targets",
        ".xml"
    };

    public CodeExplorerService()
    {
        WorkspaceRootPath = ResolveWorkspaceRoot(AppContext.BaseDirectory);
    }

    public string WorkspaceRootPath { get; }

    public Task<IReadOnlyList<CodeFileDescriptor>> GetCodeFilesAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<CodeFileDescriptor>>(() =>
        {
            var results = new List<CodeFileDescriptor>();

            foreach (var fullPath in Directory.EnumerateFiles(WorkspaceRootPath, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (ShouldSkipFile(fullPath))
                {
                    continue;
                }

                var fileInfo = new FileInfo(fullPath);
                var relativePath = Path.GetRelativePath(WorkspaceRootPath, fullPath).Replace('\\', '/');
                results.Add(new CodeFileDescriptor(relativePath, fileInfo.Length, fileInfo.LastWriteTimeUtc));
            }

            return results
                .OrderBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }, cancellationToken);
    }

    public async Task<string> ReadFileAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return string.Empty;
        }

        var fullPath = ResolveSafePath(relativePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("File was not found.", fullPath);
        }

        return await File.ReadAllTextAsync(fullPath, cancellationToken);
    }

    private bool ShouldSkipFile(string fullPath)
    {
        var extension = Path.GetExtension(fullPath);
        if (!AllowedExtensions.Contains(extension))
        {
            return true;
        }

        var currentDirectory = new DirectoryInfo(Path.GetDirectoryName(fullPath) ?? WorkspaceRootPath);
        var rootDirectory = new DirectoryInfo(WorkspaceRootPath);

        while (currentDirectory.FullName.Length >= rootDirectory.FullName.Length)
        {
            if (ExcludedDirectories.Contains(currentDirectory.Name))
            {
                return true;
            }

            if (string.Equals(currentDirectory.FullName, rootDirectory.FullName, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (currentDirectory.Parent is null)
            {
                break;
            }

            currentDirectory = currentDirectory.Parent;
        }

        return false;
    }

    private string ResolveSafePath(string relativePath)
    {
        var candidatePath = Path.GetFullPath(Path.Combine(WorkspaceRootPath, relativePath));
        var rootPathWithSeparator = WorkspaceRootPath.EndsWith(Path.DirectorySeparatorChar)
            ? WorkspaceRootPath
            : $"{WorkspaceRootPath}{Path.DirectorySeparatorChar}";

        if (!candidatePath.StartsWith(rootPathWithSeparator, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(candidatePath, WorkspaceRootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid file path.");
        }

        return candidatePath;
    }

    private static string ResolveWorkspaceRoot(string basePath)
    {
        DirectoryInfo? directory = new(basePath);
        while (directory is not null)
        {
            var solutionPath = Path.Combine(directory.FullName, "Erp.sln");
            if (File.Exists(solutionPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
