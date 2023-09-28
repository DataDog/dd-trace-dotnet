// Decompiled with JetBrains decompiler
// Type: System.Collections.Immutable.ImmutableDictionaryDebuggerProxy`2
// Assembly: System.Collections.Immutable, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 5F9FF90F-0D16-4469-A104-76829D3705E2
// Assembly location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.dll
// XML documentation location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.xml

using System.Collections.Generic;


#nullable enable
namespace Datadog.System.Collections.Immutable
{
    internal sealed class ImmutableDictionaryDebuggerProxy<TKey, TValue> : 
    ImmutableEnumerableDebuggerProxy<KeyValuePair<TKey, TValue>>
    where TKey : notnull
  {
    public ImmutableDictionaryDebuggerProxy(IImmutableDictionary<TKey, TValue> dictionary)
      : base((IEnumerable<KeyValuePair<TKey, TValue>>) dictionary)
    {
    }
  }
}
