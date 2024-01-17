﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.TypeDefinitionHandleCollection
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using System.Collections;
using System.Collections.Generic;


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
    /// <summary>
    /// Represents a collection of <see cref="T:System.Reflection.Metadata.TypeDefinitionHandle" />.
    /// </summary>
    public readonly struct TypeDefinitionHandleCollection : 
    IReadOnlyCollection<TypeDefinitionHandle>,
    IEnumerable<TypeDefinitionHandle>,
    IEnumerable
  {
    private readonly int _lastRowId;

    internal TypeDefinitionHandleCollection(int lastRowId) => this._lastRowId = lastRowId;

    public int Count => this._lastRowId;

    public TypeDefinitionHandleCollection.Enumerator GetEnumerator() => new TypeDefinitionHandleCollection.Enumerator(this._lastRowId);


    #nullable disable
    IEnumerator<TypeDefinitionHandle> IEnumerable<TypeDefinitionHandle>.GetEnumerator() => (IEnumerator<TypeDefinitionHandle>) this.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => (IEnumerator) this.GetEnumerator();


    #nullable enable
    public struct Enumerator : IEnumerator<TypeDefinitionHandle>, IDisposable, IEnumerator
    {
      private readonly int _lastRowId;
      private int _currentRowId;
      private const int EnumEnded = 16777216;

      internal Enumerator(int lastRowId)
      {
        this._lastRowId = lastRowId;
        this._currentRowId = 0;
      }

      public TypeDefinitionHandle Current => TypeDefinitionHandle.FromRowId((int) ((long) this._currentRowId & 16777215L));

      public bool MoveNext()
      {
        if (this._currentRowId >= this._lastRowId)
        {
          this._currentRowId = 16777216;
          return false;
        }
        ++this._currentRowId;
        return true;
      }

      object IEnumerator.Current => (object) this.Current;

      void IEnumerator.Reset() => throw new NotSupportedException();

      void IDisposable.Dispose()
      {
      }
    }
  }
}
