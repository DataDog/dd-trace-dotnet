using System;
using Datadog.Trace.Interfaces;

namespace Datadog.Trace.ExtensionMethods
{
    internal static class SpanExtensions
    {
        internal static string GetHttpMethod(this ISpan span)
            => span.GetTag(Tags.HttpMethod);

        internal static void SetException(this ISpan span, Exception exception)
        {
            span.Error = true;

            // for AggregateException, use the first inner exception until we can support multiple errors.
            // there will be only one error in most cases, and even if there are more and we lose
            // the other ones, it's still better than the generic "one or more errors occurred" message.
            if (exception is AggregateException aggregateException && aggregateException.InnerExceptions.Count > 0)
            {
                exception = aggregateException.InnerExceptions[0];
            }

            span.Tag(Tags.ErrorMsg, exception.Message);
            span.Tag(Tags.ErrorStack, exception.StackTrace);
            span.Tag(Tags.ErrorType, exception.GetType().ToString());
        }

        internal static bool SetExceptionAndReturnFalse(this ISpan span, Exception exception)
        {
            // Why would you have a method that always just returns false you may ask...it's useful for one scenario, and
            // that is using this as an exception filter to actually effect that handling of setting the exception info in
            // the span in question while not unwinding the call stack in a catch block, and letting the exception simply
            // propogate back out to the caller naturally.

            if (span == null)
            {
                // Purposely handle null here to avoid having to null-coalesce a static "SetExceptionAndReturnFalse(x) ?? false" everywhere
                // this would ever get used (as trying to use this without it would result in a bool? and not a bool....
                return false;
            }

            SetException(span, exception);

            return false;
        }
    }
}
