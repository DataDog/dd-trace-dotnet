using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Samples.AspNetCoreMvc.Shared;

public class CustomSpanMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _operationName;

    public CustomSpanMiddleware(RequestDelegate next, string operationName)
    {
        _next = next;
        _operationName = operationName;
    }

    public async Task Invoke(HttpContext context)
    {
        using var scope = SampleHelpers.CreateScope(_operationName);
        await _next(context);
    }
}
