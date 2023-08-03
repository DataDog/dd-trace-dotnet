using Microsoft.AspNetCore.Mvc.Filters;

namespace Samples.Security.AspNetCore5.Filters;
public class ResultExceptionFilter : ResultFilterAttribute
{
    public override void OnResultExecuting(ResultExecutingContext context)
    {
        if(context.Result != null)
        {
            // log exception
        }
        base.OnResultExecuting(context);
    }

    public override void OnResultExecuted(ResultExecutedContext context)
    {
        if(context.Exception != null)
        {
            // log exception
        }
        base.OnResultExecuted(context);
    }
}
