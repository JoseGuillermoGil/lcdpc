using Microsoft.AspNetCore.Mvc;

namespace LCDPC.API.Security;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class ApiKeyAuthAttribute : TypeFilterAttribute(typeof(ApiKeyAuthFilter))
{
    public ApiKeyAuthAttribute() : base(typeof(ApiKeyAuthFilter))
    {
    }
}
