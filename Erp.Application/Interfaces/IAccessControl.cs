namespace Erp.Application.Interfaces;

public interface IAccessControl
{
    void DemandAuthenticated();
    void DemandPermission(string permissionCode);
}
