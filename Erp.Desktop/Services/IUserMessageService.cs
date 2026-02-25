namespace Erp.Desktop.Services;

public interface IUserMessageService
{
    void ShowInfo(string message, string title = "ERP");
    void ShowWarning(string message, string title = "ERP");
    void ShowError(string message, string title = "ERP");
}
