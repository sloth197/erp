using Microsoft.Win32;

namespace Erp.Desktop.Services;

public sealed class FileSaveDialogService : IFileSaveDialogService
{
    public string? ShowCsvSaveDialog(string defaultFileName)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            DefaultExt = ".csv",
            AddExtension = true,
            FileName = defaultFileName,
            OverwritePrompt = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
