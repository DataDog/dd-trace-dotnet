using System;

namespace Datadog.Trace.Tagging
{
    internal interface IProperty<TResult>
    {
        bool IsReadOnly { get; }

        string Key { get; }

        Func<ITags, TResult> Getter { get; }

        Action<ITags, TResult> Setter { get; }
    }
}
