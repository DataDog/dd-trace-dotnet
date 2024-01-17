﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Internal.ObjectPool`1
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using System.Threading;


#nullable enable
namespace Datadog.System.Reflection.Internal
{
    /// <summary>
    /// Generic implementation of object pooling pattern with predefined pool size limit. The main
    /// purpose is that limited number of frequently used objects can be kept in the pool for
    /// further recycling.
    /// 
    /// Notes:
    /// 1) it is not the goal to keep all returned objects. Pool is not meant for storage. If there
    ///    is no space in the pool, extra returned objects will be dropped.
    /// 
    /// 2) it is implied that if object was obtained from a pool, the caller will return it back in
    ///    a relatively short time. Keeping checked out objects for long durations is ok, but
    ///    reduces usefulness of pooling. Just new up your own.
    /// 
    /// Not returning objects to the pool in not detrimental to the pool's work, but is a bad practice.
    /// Rationale:
    ///    If there is no intent for reusing the object, do not use pool - just use "new".
    /// </summary>
    internal sealed class ObjectPool<T> where T : class
  {

    #nullable disable
    private readonly ObjectPool<T>.Element[] _items;
    private readonly Func<T> _factory;


    #nullable enable
    internal ObjectPool(Func<T> factory)
      : this(factory, Environment.ProcessorCount * 2)
    {
    }

    internal ObjectPool(Func<T> factory, int size)
    {
      this._factory = factory;
      this._items = new ObjectPool<T>.Element[size];
    }


    #nullable disable
    private T CreateInstance() => this._factory();


    #nullable enable
    /// <summary>Produces an instance.</summary>
    /// <remarks>
    /// Search strategy is a simple linear probing which is chosen for it cache-friendliness.
    /// Note that Free will try to store recycled objects close to the start thus statistically
    /// reducing how far we will typically search.
    /// </remarks>
    internal T Allocate()
    {
      ObjectPool<T>.Element[] items = this._items;
      T instance;
      for (int index = 0; index < items.Length; ++index)
      {
        instance = items[index].Value;
        if ((object) instance != null && (object) instance == (object) Interlocked.CompareExchange<T>(ref items[index].Value, default (T), instance))
          goto label_5;
      }
      instance = this.CreateInstance();
label_5:
      return instance;
    }

    /// <summary>Returns objects to the pool.</summary>
    /// <remarks>
    /// Search strategy is a simple linear probing which is chosen for it cache-friendliness.
    /// Note that Free will try to store recycled objects close to the start thus statistically
    /// reducing how far we will typically search in Allocate.
    /// </remarks>
    internal void Free(T obj)
    {
      ObjectPool<T>.Element[] items = this._items;
      for (int index = 0; index < items.Length; ++index)
      {
        if ((object) items[index].Value == null)
        {
          items[index].Value = obj;
          break;
        }
      }
    }


    #nullable disable
    private struct Element
    {
      internal T Value;
    }
  }
}
