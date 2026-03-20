using System.Globalization;
using System.Text;
using Erp.Application.DTOs;

namespace Erp.Desktop.Services;

public sealed class ItemCsvExportService : IItemCsvExportService
{
    public string BuildCsv(IReadOnlyList<ItemListDto> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Code,Name,Category,Active,Price");

        foreach (var row in rows)
        {
            var category = $"{row.CategoryCode} - {row.CategoryName}";
            var price = 0m.ToString("0.00", CultureInfo.InvariantCulture);
            builder.AppendLine(string.Join(",",
                Escape(row.ItemCode),
                Escape(row.Name),
                Escape(category),
                Escape(row.IsActive.ToString()),
                Escape(price)));
        }

        return builder.ToString();
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var escaped = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }
}
