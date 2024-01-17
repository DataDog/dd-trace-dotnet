﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Internal.EnumerableExtensions
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using System.Collections.Generic;
using Datadog.System.Collections.Immutable;


#nullable enable
namespace Datadog.System.Reflection.Internal
{
    /// <summary>
    /// Replacements for System.Linq to avoid an unnecessary dependency.
    /// Parameter and return types strengthened to actual internal usage as an optimization.
    /// </summary>
    internal static class EnumerableExtensions
  {
    public static T? FirstOrDefault<T>(this ImmutableArray<T> collection, Func<T, bool> predicate)
    {
      foreach (T obj in collection)
      {
        if (predicate(obj))
          return obj;
      }
      return default (T);
    }

    public static IEnumerable<TResult> Select<TSource, TResult>(
      this IEnumerable<TSource> source,
      Func<TSource, TResult> selector)
    {
      foreach (TSource source1 in source)
        yield return selector(source1);
    }

    public static T Last<T>(this ImmutableArray<T>.Builder source) => source[source.Count - 1];

    public static IEnumerable<T> OrderBy<T>(this List<T> source, Comparison<T> comparison)
    {
      int[] array = new int[source.Count];
      for (int index = 0; index < array.Length; ++index)
        array[index] = index;
      Array.Sort<int>(array, (Comparison<int>) ((left, right) =>
      {
        if (left == right)
          return 0;
        int num = comparison(source[left], source[right]);
        return num == 0 ? left - right : num;
      }));
      int[] numArray = array;
      for (int index = 0; index < numArray.Length; ++index)
        yield return source[numArray[index]];
      numArray = (int[]) null;
    }
  }
}
