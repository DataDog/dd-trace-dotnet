// Decompiled with JetBrains decompiler
// Type: System.Collections.Immutable.ImmutableDictionaryBuilderDebuggerProxy`2
// Assembly: System.Collections.Immutable, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 5F9FF90F-0D16-4469-A104-76829D3705E2
// Assembly location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.dll
// XML documentation location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.xml

using System.Collections.Generic;
using System.Diagnostics;


#nullable enable
namespace Datadog.System.Collections.Immutable
{
    internal sealed class ImmutableDictionaryBuilderDebuggerProxy<TKey, TValue> where TKey : notnull
  {

    #nullable disable
    private readonly ImmutableDictionary<TKey, TValue>.Builder _map;
    private KeyValuePair<TKey, TValue>[] _contents;


    #nullable enable
    public ImmutableDictionaryBuilderDebuggerProxy(ImmutableDictionary<TKey, TValue>.Builder map)
    {
      Requires.NotNull<ImmutableDictionary<TKey, TValue>.Builder>(map, nameof (map));
      this._map = map;
    }

    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public KeyValuePair<TKey, TValue>[] Contents => this._contents ?? (this._contents = this._map.ToArray<KeyValuePair<TKey, TValue>>(this._map.Count));
  }
}
