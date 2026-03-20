namespace Erp.Desktop.Services;

public interface ICodeExplorerService
{
    string WorkspaceRootPath { get; }

    Task<IReadOnlyList<CodeFileDescriptor>> GetCodeFilesAsync(CancellationToken cancellationToken = default);

    Task<string> ReadFileAsync(string relativePath, CancellationToken cancellationToken = default);
}

public sealed record CodeFileDescriptor(
    string RelativePath,
    long Length,
    DateTime LastWriteTimeUtc);
