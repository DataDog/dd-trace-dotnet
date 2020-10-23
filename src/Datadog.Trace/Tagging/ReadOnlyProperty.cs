using System;

namespace Datadog.Trace.Tagging
{
    internal class ReadOnlyProperty<TTags, TResult> : Property<TTags, TResult>
    {
        public ReadOnlyProperty(string key, Func<TTags, TResult> getter)
            : base(key, getter, (value, tag) => throw new InvalidOperationException($"Tag {key} is read-only"))
        {
        }

        public override bool IsReadOnly => true;
    }
}
