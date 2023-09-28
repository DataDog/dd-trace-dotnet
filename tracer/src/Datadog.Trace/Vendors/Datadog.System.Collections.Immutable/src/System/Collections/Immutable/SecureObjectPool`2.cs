// Decompiled with JetBrains decompiler
// Type: System.Collections.Immutable.SecureObjectPool`2
// Assembly: System.Collections.Immutable, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 5F9FF90F-0D16-4469-A104-76829D3705E2
// Assembly location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.dll
// XML documentation location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.xml


#nullable enable
namespace Datadog.System.Collections.Immutable
{
  internal static class SecureObjectPool<T, TCaller> where TCaller : ISecurePooledObjectUser
  {
    public static void TryAdd(TCaller caller, SecurePooledObject<T> item)
    {
      if (caller.PoolUserId != item.Owner)
        return;
      item.Owner = -1;
      AllocFreeConcurrentStack<SecurePooledObject<T>>.TryAdd(item);
    }

    public static bool TryTake(TCaller caller, out SecurePooledObject<T>? item)
    {
      if (caller.PoolUserId != -1 && AllocFreeConcurrentStack<SecurePooledObject<T>>.TryTake(out item))
      {
        item.Owner = caller.PoolUserId;
        return true;
      }
      item = (SecurePooledObject<T>) null;
      return false;
    }

    public static SecurePooledObject<T> PrepNew(TCaller caller, T newValue)
    {
      Requires.NotNullAllowStructs<T>(newValue, nameof (newValue));
      return new SecurePooledObject<T>(newValue)
      {
        Owner = caller.PoolUserId
      };
    }
  }
}
