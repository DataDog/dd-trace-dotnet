namespace ReproApp;

/// <summary>
/// Minimal async middleware pattern: try { await _next } catch (specific) { ... }.
/// The compiler-generated <c>MoveNext()</c> has EH clauses where Exception Replay's
/// debugger rewriter can inject a probe try/catch <em>inside</em> an existing catch
/// handler region. v3.41.0's EH sort missed try-in-handler nesting (fixed by SortEHClauses).
/// </summary>
public sealed class ReproMiddleware
{
    private readonly RequestDelegate _next;

    public ReproMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (UnauthorizedAccessException)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        }
        // Other exceptions propagate → ER can ReJIT this MoveNext and trigger the EH sort bug.
    }
}
