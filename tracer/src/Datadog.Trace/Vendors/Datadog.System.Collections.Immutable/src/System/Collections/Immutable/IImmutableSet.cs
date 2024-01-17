﻿// Decompiled with JetBrains decompiler
// Type: System.Collections.Immutable.IImmutableSet`1
// Assembly: System.Collections.Immutable, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 5F9FF90F-0D16-4469-A104-76829D3705E2
// Assembly location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.dll
// XML documentation location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.xml

using System.Collections;
using System.Collections.Generic;


#nullable enable
namespace Datadog.System.Collections.Immutable
{
    /// <summary>Represents a set of elements that can only be modified by creating a new instance of the set.
    /// 
    /// NuGet package: System.Collections.Immutable (about immutable collections and how to install)</summary>
    /// <typeparam name="T">The type of element stored in the set.</typeparam>
    public interface IImmutableSet<T> : IReadOnlyCollection<T>, IEnumerable<T>, IEnumerable
  {
    /// <summary>Retrieves an empty immutable set that has the same sorting and ordering semantics as this instance.</summary>
    /// <returns>An empty set that has the same sorting and ordering semantics as this instance.</returns>
    IImmutableSet<T> Clear();

    /// <summary>Determines whether this immutable set contains a specified element.</summary>
    /// <param name="value">The element to locate in the set.</param>
    /// <returns>
    /// <see langword="true" /> if the set contains the specified value; otherwise, <see langword="false" />.</returns>
    bool Contains(T value);

    /// <summary>Adds the specified element to this immutable set.</summary>
    /// <param name="value">The element to add.</param>
    /// <returns>A new set with the element added, or this set if the element is already in the set.</returns>
    IImmutableSet<T> Add(T value);

    /// <summary>Removes the specified element from this immutable set.</summary>
    /// <param name="value">The element to remove.</param>
    /// <returns>A new set with the specified element removed, or the current set if the element cannot be found in the set.</returns>
    IImmutableSet<T> Remove(T value);

    /// <summary>Determines whether the set contains a specified value.</summary>
    /// <param name="equalValue">The value to search for.</param>
    /// <param name="actualValue">The matching value from the set, if found, or <c>equalvalue</c> if there are no matches.</param>
    /// <returns>
    /// <see langword="true" /> if a matching value was found; otherwise, <see langword="false" />.</returns>
    bool TryGetValue(T equalValue, out T actualValue);

    /// <summary>Creates an immutable set that contains only elements that exist in this set and the specified set.</summary>
    /// <param name="other">The collection to compare to the current <see cref="T:System.Collections.Immutable.IImmutableSet`1" />.</param>
    /// <returns>A new immutable set that contains elements that exist in both sets.</returns>
    IImmutableSet<T> Intersect(IEnumerable<T> other);

    /// <summary>Removes the elements in the specified collection from the current immutable set.</summary>
    /// <param name="other">The collection of items to remove from this set.</param>
    /// <returns>A new set with the items removed; or the original set if none of the items were in the set.</returns>
    IImmutableSet<T> Except(IEnumerable<T> other);

    /// <summary>Creates an immutable set that contains only elements that are present either in the current set or in the specified collection, but not both.</summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <returns>A new set that contains the elements that are present only in the current set or in the specified collection, but not both.</returns>
    IImmutableSet<T> SymmetricExcept(IEnumerable<T> other);

    /// <summary>Creates a new immutable set that contains all elements that are present in either the current set or in the specified collection.</summary>
    /// <param name="other">The collection to add elements from.</param>
    /// <returns>A new immutable set with the items added; or the original set if all the items were already in the set.</returns>
    IImmutableSet<T> Union(IEnumerable<T> other);

    /// <summary>Determines whether the current immutable set and the specified collection contain the same elements.</summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <returns>
    /// <see langword="true" /> if the sets are equal; otherwise, <see langword="false" />.</returns>
    bool SetEquals(IEnumerable<T> other);

    /// <summary>Determines whether the current immutable set is a proper (strict) subset of the specified collection.</summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <returns>
    /// <see langword="true" /> if the current set is a proper subset of the specified collection; otherwise, <see langword="false" />.</returns>
    bool IsProperSubsetOf(IEnumerable<T> other);

    /// <summary>Determines whether the current immutable set is a proper (strict) superset of the specified collection.</summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <returns>
    /// <see langword="true" /> if the current set is a proper superset of the specified collection; otherwise, <see langword="false" />.</returns>
    bool IsProperSupersetOf(IEnumerable<T> other);

    /// <summary>Determines whether the current immutable set is a subset of a specified collection.</summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <returns>
    /// <see langword="true" /> if the current set is a subset of the specified collection; otherwise, <see langword="false" />.</returns>
    bool IsSubsetOf(IEnumerable<T> other);

    /// <summary>Determines whether the current immutable set is a superset of a specified collection.</summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <returns>
    /// <see langword="true" /> if the current set is a superset of the specified collection; otherwise, <see langword="false" />.</returns>
    bool IsSupersetOf(IEnumerable<T> other);

    /// <summary>Determines whether the current immutable set overlaps with the specified collection.</summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <returns>
    /// <see langword="true" /> if the current set and the specified collection share at least one common element; otherwise, <see langword="false" />.</returns>
    bool Overlaps(IEnumerable<T> other);
  }
}
