using System;

namespace Datadog.Trace.TestHelpers.NamedPipes.Utilities
{
    internal static class EventHandlerExtensions
    {
        public static void SafeInvoke<T>(this EventHandler<T> @event, object sender, T eventArgs)
            where T : EventArgs
        {
            @event?.Invoke(sender, eventArgs);
        }
    }
}
