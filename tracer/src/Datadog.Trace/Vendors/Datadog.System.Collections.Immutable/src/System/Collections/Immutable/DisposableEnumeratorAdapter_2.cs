// Decompiled with JetBrains decompiler
// Type: System.Collections.Immutable.DisposableEnumeratorAdapter`2
// Assembly: System.Collections.Immutable, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 5F9FF90F-0D16-4469-A104-76829D3705E2
// Assembly location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.dll
// XML documentation location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.xml

using System;
using System.Collections.Generic;


#nullable enable
namespace Datadog.System.Collections.Immutable
{
    internal struct DisposableEnumeratorAdapter<T, TEnumerator> : IDisposable where TEnumerator : struct, IEnumerator<T>
  {

    #nullable disable
    private readonly IEnumerator<T> _enumeratorObject;
    private TEnumerator _enumeratorStruct;


    #nullable enable
    internal DisposableEnumeratorAdapter(TEnumerator enumerator)
    {
      this._enumeratorStruct = enumerator;
      this._enumeratorObject = (IEnumerator<T>) null;
    }

    internal DisposableEnumeratorAdapter(IEnumerator<T> enumerator)
    {
      this._enumeratorStruct = default (TEnumerator);
      this._enumeratorObject = enumerator;
    }

    public T Current => this._enumeratorObject == null ? this._enumeratorStruct.Current : this._enumeratorObject.Current;

    public bool MoveNext() => this._enumeratorObject == null ? this._enumeratorStruct.MoveNext() : this._enumeratorObject.MoveNext();

    public void Dispose()
    {
      if (this._enumeratorObject != null)
        this._enumeratorObject.Dispose();
      else
        this._enumeratorStruct.Dispose();
    }

    public DisposableEnumeratorAdapter<T, TEnumerator> GetEnumerator() => this;
  }
}
