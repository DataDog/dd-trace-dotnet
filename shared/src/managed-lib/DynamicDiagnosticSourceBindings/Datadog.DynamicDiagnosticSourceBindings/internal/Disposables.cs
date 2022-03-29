using Datadog.Util;
using System;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    internal static class Disposables
    {
        public sealed class NoOp : IDisposable
        {
            public static readonly NoOp SingeltonInstance = new NoOp();

            public void Dispose()
            {
            }
        }

        public sealed class Action : IDisposable
        {
            private readonly System.Action _action;

            public Action(System.Action action)
            {
                Validate.NotNull(action, nameof(action));
                _action = action;
            }

            public void Dispose()
            {
                _action();
            }
        }
    }
}
