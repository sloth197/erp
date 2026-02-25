using Erp.Application.DTOs;

namespace Erp.Desktop.Services;

public interface IItemCsvExportService
{
    string BuildCsv(IReadOnlyList<ItemListDto> rows);
}
