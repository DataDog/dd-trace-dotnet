using System;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    internal static class AwsHelpers
    {
        public static string TrimRequestFromEnd(string input)
        {
            const string requestPostfix = "Request";

            if (input != null && input.EndsWith(requestPostfix))
            {
                return input.Substring(0, input.LastIndexOf(requestPostfix, StringComparison.Ordinal));
            }

            return input;
        }

        public static string TrimAmazonPrefix(string input)
        {
            const string amazonPrefix = "Amazon.";

            if (input != null && input.StartsWith(amazonPrefix))
            {
                return input.Substring(amazonPrefix.Length, input.Length - amazonPrefix.Length);
            }

            return input;
        }
    }
}
