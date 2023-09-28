// Decompiled with JetBrains decompiler
// Type: System.Collections.Immutable.ValuesCollectionAccessor`2
// Assembly: System.Collections.Immutable, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 5F9FF90F-0D16-4469-A104-76829D3705E2
// Assembly location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.dll
// XML documentation location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.xml


#nullable enable
using System;

namespace Datadog.System.Collections.Immutable
{
  internal sealed class ValuesCollectionAccessor<TKey, TValue> : 
    KeysOrValuesCollectionAccessor<TKey, TValue, TValue>
    where TKey : notnull
  {
    internal ValuesCollectionAccessor(IImmutableDictionary<TKey, TValue> dictionary)
      : base(dictionary, dictionary.Values)
    {
    }

    public override bool Contains(TValue item)
    {
      if (this.Dictionary is ImmutableSortedDictionary<TKey, TValue> dictionary1)
        return dictionary1.ContainsValue(item);
      return this.Dictionary is IImmutableDictionaryInternal<TKey, TValue> dictionary2 ? dictionary2.ContainsValue(item) : throw new NotSupportedException();
    }
  }
}
