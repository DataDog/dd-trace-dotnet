﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Internal.PooledStringBuilder
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using System.Text;


#nullable enable
namespace Datadog.System.Reflection.Internal
{
    /// <summary>
    /// The usage is:
    ///        var inst = PooledStringBuilder.GetInstance();
    ///        var sb = inst.builder;
    ///        ... Do Stuff...
    ///        ... sb.ToString() ...
    ///        inst.Free();
    /// </summary>
    internal sealed class PooledStringBuilder
  {
    public readonly StringBuilder Builder = new StringBuilder();

    #nullable disable
    private readonly ObjectPool<PooledStringBuilder> _pool;
    private static readonly ObjectPool<PooledStringBuilder> s_poolInstance = PooledStringBuilder.CreatePool();

    private PooledStringBuilder(ObjectPool<PooledStringBuilder> pool) => this._pool = pool;

    public int Length => this.Builder.Length;

    public void Free()
    {
      StringBuilder builder = this.Builder;
      if (builder.Capacity > 1024)
        return;
      builder.Clear();
      this._pool.Free(this);
    }


    #nullable enable
    public string ToStringAndFree()
    {
      string stringAndFree = this.Builder.ToString();
      this.Free();
      return stringAndFree;
    }

    public static ObjectPool<PooledStringBuilder> CreatePool()
    {
      ObjectPool<PooledStringBuilder> pool = (ObjectPool<PooledStringBuilder>) null;
      pool = new ObjectPool<PooledStringBuilder>((Func<PooledStringBuilder>) (() => new PooledStringBuilder(pool)), 32);
      return pool;
    }

    public static PooledStringBuilder GetInstance() => PooledStringBuilder.s_poolInstance.Allocate();
  }
}
