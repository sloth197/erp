namespace Erp.Desktop.Navigation;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequiredPermissionAttribute : Attribute
{
    public RequiredPermissionAttribute(string code)
    {
        Code = code;
    }

    public string Code { get; }
}
