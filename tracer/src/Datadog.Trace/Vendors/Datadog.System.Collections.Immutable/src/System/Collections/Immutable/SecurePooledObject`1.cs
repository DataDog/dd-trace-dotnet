// Decompiled with JetBrains decompiler
// Type: System.Collections.Immutable.SecurePooledObject`1
// Assembly: System.Collections.Immutable, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 5F9FF90F-0D16-4469-A104-76829D3705E2
// Assembly location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.dll
// XML documentation location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.xml

using System.Runtime.CompilerServices;
using Datadog.System.Diagnostics.CodeAnalysis;


#nullable enable
namespace Datadog.System.Collections.Immutable
{
    internal sealed class SecurePooledObject<T>
  {

    #nullable disable
    private readonly T _value;
    private int _owner;


    #nullable enable
    internal SecurePooledObject(T newValue)
    {
      Requires.NotNullAllowStructs<T>(newValue, nameof (newValue));
      this._value = newValue;
    }

    internal int Owner
    {
      get => this._owner;
      set => this._owner = value;
    }

    internal T Use<TCaller>(ref TCaller caller) where TCaller : struct, ISecurePooledObjectUser
    {
      if (!this.IsOwned<TCaller>(ref caller))
        Requires.FailObjectDisposed<TCaller>(caller);
      return this._value;
    }

    internal bool TryUse<TCaller>(ref TCaller caller, [MaybeNullWhen(false)] out T value) where TCaller : struct, ISecurePooledObjectUser
    {
      if (this.IsOwned<TCaller>(ref caller))
      {
        value = this._value;
        return true;
      }
      value = default (T);
      return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsOwned<TCaller>(ref TCaller caller) where TCaller : struct, ISecurePooledObjectUser => caller.PoolUserId == this._owner;
  }
}
