﻿// Decompiled with JetBrains decompiler
// Type: System.Collections.Immutable.AllocFreeConcurrentStack`1
// Assembly: System.Collections.Immutable, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 5F9FF90F-0D16-4469-A104-76829D3705E2
// Assembly location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.dll
// XML documentation location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.xml

using System;
using System.Collections.Generic;
using Datadog.System.Diagnostics.CodeAnalysis;


#nullable enable
namespace Datadog.System.Collections.Immutable
{
    internal static class AllocFreeConcurrentStack<T>
  {
    private const int MaxSize = 35;

    #nullable disable
    private static readonly Type s_typeOfT = typeof (T);


    #nullable enable
    public static void TryAdd(T item)
    {
      Stack<RefAsValueType<T>> threadLocalStack = AllocFreeConcurrentStack<T>.ThreadLocalStack;
      if (threadLocalStack.Count >= 35)
        return;
      threadLocalStack.Push(new RefAsValueType<T>(item));
    }

    public static bool TryTake([MaybeNullWhen(false)] out T item)
    {
      Stack<RefAsValueType<T>> threadLocalStack = AllocFreeConcurrentStack<T>.ThreadLocalStack;
      if (threadLocalStack != null && threadLocalStack.Count > 0)
      {
        item = threadLocalStack.Pop().Value;
        return true;
      }
      item = default (T);
      return false;
    }

    private static Stack<RefAsValueType<T>> ThreadLocalStack
    {
      get
      {
        Dictionary<Type, object> dictionary = AllocFreeConcurrentStack.t_stacks ?? (AllocFreeConcurrentStack.t_stacks = new Dictionary<Type, object>());
        object threadLocalStack;
        if (!dictionary.TryGetValue(AllocFreeConcurrentStack<T>.s_typeOfT, out threadLocalStack))
        {
          threadLocalStack = (object) new Stack<RefAsValueType<T>>(35);
          dictionary.Add(AllocFreeConcurrentStack<T>.s_typeOfT, threadLocalStack);
        }
        return (Stack<RefAsValueType<T>>) threadLocalStack;
      }
    }
  }
}
