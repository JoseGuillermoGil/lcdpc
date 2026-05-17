using Microsoft.AspNetCore.Mvc;

namespace LCDPC.API.Security;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class RequireRolesAttribute(params string[] roles) : TypeFilterAttribute(typeof(RequireRolesFilter))
{
    public string[] Roles { get; } = roles;
}
