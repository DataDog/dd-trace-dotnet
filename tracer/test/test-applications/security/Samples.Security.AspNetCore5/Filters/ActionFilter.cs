using Microsoft.AspNetCore.Mvc.Filters;

namespace Samples.Security.AspNetCore5.Filters;
public class ActionFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        // Do something before the action executes.
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        var exc = context.Exception;
        
        // Do something after the action executes.
    }
}
