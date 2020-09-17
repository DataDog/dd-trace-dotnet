using System;

namespace Datadog.Trace.Tagging
{
    internal interface IProperty<TResult>
    {
        string Key { get; }

        Func<ITags, TResult> Getter { get; }

        Action<ITags, TResult> Setter { get; }
    }
}
