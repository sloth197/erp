namespace Erp.Application.Commands;

public sealed class CloseShipmentDayCommand
{
    public DateTime Date { get; init; } = DateTime.Today;
}
