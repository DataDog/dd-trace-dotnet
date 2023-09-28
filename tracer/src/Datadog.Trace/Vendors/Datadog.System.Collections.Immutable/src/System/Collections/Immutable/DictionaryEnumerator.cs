// Decompiled with JetBrains decompiler
// Type: System.Collections.Immutable.DictionaryEnumerator`2
// Assembly: System.Collections.Immutable, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 5F9FF90F-0D16-4469-A104-76829D3705E2
// Assembly location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.dll
// XML documentation location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.xml

using System.Collections;
using System.Collections.Generic;


#nullable enable
namespace Datadog.System.Collections.Immutable
{
    internal sealed class DictionaryEnumerator<TKey, TValue> : IDictionaryEnumerator, IEnumerator where TKey : notnull
    {

#nullable disable
        private readonly IEnumerator<KeyValuePair<TKey, TValue>> _inner;


#nullable enable
        internal DictionaryEnumerator(IEnumerator<KeyValuePair<TKey, TValue>> inner)
        {
            Requires.NotNull<IEnumerator<KeyValuePair<TKey, TValue>>>(inner, nameof(inner));
            this._inner = inner;
        }

        public DictionaryEntry Entry
        {
            get
            {
                KeyValuePair<TKey, TValue> current = this._inner.Current;
                var key = (object)current.Key;
                current = this._inner.Current;
                var local = (object)current.Value;
                return new DictionaryEntry((object)key, (object)local);
            }
        }

        public object Key => (object)this._inner.Current.Key;

        public object? Value => (object)this._inner.Current.Value;

        public object Current => (object)this.Entry;

        public bool MoveNext() => this._inner.MoveNext();

        public void Reset() => this._inner.Reset();
    }
}
