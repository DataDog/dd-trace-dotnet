using System;
using System.Threading;
using Nuke.Common.Tooling;

internal static class UnitTestOutputFilter
{
    public static Action<OutputType, string> Wrap(Action<OutputType, string> logger)
    {
        var suppressedMessage = new AsyncLocal<string>();
        return (type, text) =>
        {
            // The Datadog test logger emits STARTED and SUCCESS messages for every
            // test. Arguments can span multiple lines, so suppress continuations too.
            if (text.Contains(": STARTED:", StringComparison.Ordinal))
            {
                suppressedMessage.Value = text.TrimEnd().EndsWith(")", StringComparison.Ordinal) ? null : "started";
                return;
            }

            if (text.Contains(": SUCCESS:", StringComparison.Ordinal))
            {
                suppressedMessage.Value = text.TrimEnd().EndsWith("s)", StringComparison.Ordinal) ? null : "success";
                return;
            }

            // A new xUnit event is never a continuation: preserve failures and skips.
            if (text.Contains("[xUnit.net ", StringComparison.Ordinal))
            {
                suppressedMessage.Value = null;
            }
            else if (suppressedMessage.Value is { } message)
            {
                var trimmedText = text.TrimEnd();
                if ((message == "started" && trimmedText.EndsWith(")", StringComparison.Ordinal)) ||
                    (message == "success" && trimmedText.EndsWith("s)", StringComparison.Ordinal)))
                {
                    suppressedMessage.Value = null;
                }

                return;
            }

            logger(type, text);
        };
    }
}
