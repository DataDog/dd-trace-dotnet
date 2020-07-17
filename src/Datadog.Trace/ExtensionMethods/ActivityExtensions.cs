using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Datadog.Trace.ExtensionMethods
{
    /// <summary>
    /// Extension methods for the <see cref="Activity"/> class.
    /// </summary>
    public static class ActivityExtensions
    {
        internal static void DecorateWebServerSpan(
            this Activity activity,
            string resourceName,
            string method,
            string host,
            string httpUrl,
            IEnumerable<KeyValuePair<string, string>> tags)
        {
            activity.SetCustomProperty("Type", SpanTypes.Web);
            activity.DisplayName = resourceName?.Trim();
            activity.AddTag(Tags.SpanKind, SpanKinds.Server);
            activity.AddTag(Tags.HttpMethod, method);
            activity.AddTag(Tags.HttpRequestHeadersHost, host);
            activity.AddTag(Tags.HttpUrl, httpUrl);
            activity.AddTag(Tags.Language, TracerConstants.Language);

            foreach (KeyValuePair<string, string> kvp in tags)
            {
                activity.AddTag(kvp.Key, kvp.Value);
            }
        }

        internal static void SetException(this Activity activity, Exception exception)
        {
            activity.SetCustomProperty("Error", true);

            if (exception != null)
            {
                // for AggregateException, use the first inner exception until we can support multiple errors.
                // there will be only one error in most cases, and even if there are more and we lose
                // the other ones, it's still better than the generic "one or more errors occurred" message.
                if (exception is AggregateException aggregateException && aggregateException.InnerExceptions.Count > 0)
                {
                    exception = aggregateException.InnerExceptions[0];
                }

                activity.AddTag(Trace.Tags.ErrorMsg, exception.Message);
                activity.AddTag(Trace.Tags.ErrorStack, exception.ToString());
                activity.AddTag(Trace.Tags.ErrorType, exception.GetType().ToString());
            }
        }
    }
}
