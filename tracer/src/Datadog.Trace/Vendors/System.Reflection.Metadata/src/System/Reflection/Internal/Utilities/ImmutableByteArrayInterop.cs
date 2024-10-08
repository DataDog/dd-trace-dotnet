//------------------------------------------------------------------------------
// <auto-generated />
// This file was automatically generated by the UpdateVendors tool.
//------------------------------------------------------------------------------
#pragma warning disable CS0618, CS0649, CS1574, CS1580, CS1581, CS1584, CS1591, CS1573, CS8018, SYSLIB0011, SYSLIB0032
#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618, CS8620, CS8714, CS8762, CS8765, CS8766, CS8767, CS8768, CS8769, CS8612, CS8629, CS8774
// Decompiled with JetBrains decompiler
// Type: System.Reflection.Internal.ImmutableByteArrayInterop
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72

using System.Runtime.InteropServices;
using Datadog.Trace.VendoredMicrosoftCode.System.Collections.Immutable;

#nullable enable
namespace Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Internal
{
    /// <summary>
    /// Provides tools for using <see cref="T:System.Collections.Immutable.ImmutableArray`1" /> in interop scenarios.
    /// </summary>
    /// <remarks>
    /// *** WARNING ***
    /// 
    /// If you decide to copy this code elsewhere, please retain the documentation here
    /// and the Dangerous prefixes in the API names. This will help track down and audit
    /// other places where this technique (with dangerous consequences when misused) may
    /// be applied.
    /// 
    /// A generic version of this API was once public in a pre-release of immutable
    /// collections, but  it was deemed to be too subject to abuse when available publicly.
    /// 
    /// This implementation is scoped to byte arrays as that is all that the metadata reader needs.
    /// 
    /// Also, since we don't have access to immutable collection internals, we use a trick involving
    /// overlapping a <see cref="T:System.Collections.Immutable.ImmutableArray`1" /> with an array reference. While
    /// unverifiable, it is valid. See ECMA-335, section II.10.7 Controlling instance layout:
    /// 
    /// "It is possible to overlap fields in this way, though offsets occupied by an object reference
    /// shall not overlap with offsets occupied by a built-in value type or a part of
    /// another object reference. While one object reference can completely overlap another, this is
    /// unverifiable."
    /// 
    /// Furthermore, the fact that <see cref="T:System.Collections.Immutable.ImmutableArray`1" /> backed by a single byte array
    /// field is something inherent to the design of ImmutableArray in order to get its performance
    /// characteristics and therefore something we (Microsoft) are comfortable defining as a contract that
    /// can be depended upon as below.
    /// </remarks>
    internal static class ImmutableByteArrayInterop
  {
    /// <summary>
    /// Creates a new instance of <see cref="T:System.Collections.Immutable.ImmutableArray`1" /> using a given mutable array as the backing
    /// field, without creating a defensive copy. It is the responsibility of the caller to ensure no other mutable
    /// references exist to the array.  Do not mutate the array after calling this method.
    /// </summary>
    /// <param name="array">The mutable array to use as the backing field. The incoming reference is set to null
    /// since it should not be retained by the caller.</param>
    /// <remarks>
    /// Users of this method should take extra care to ensure that the mutable array given as a parameter
    /// is never modified. The returned <see cref="T:System.Collections.Immutable.ImmutableArray`1" /> will use the given array as its backing
    /// field without creating a defensive copy, so changes made to the given mutable array will be observable
    /// on the returned <see cref="T:System.Collections.Immutable.ImmutableArray`1" />.  Instance and static methods of <see cref="T:System.Collections.Immutable.ImmutableArray`1" />
    /// and <see cref="T:System.Collections.Immutable.ImmutableArray" /> may malfunction if they operate on an <see cref="T:System.Collections.Immutable.ImmutableArray`1" /> instance
    /// whose underlying backing field is modified.
    /// </remarks>
    /// <returns>An immutable array.</returns>
    internal static ImmutableArray<byte> DangerousCreateFromUnderlyingArray(ref byte[]? array)
    {
      byte[] numArray = array;
      array = (byte[]) null;
      return new ImmutableByteArrayInterop.ByteArrayUnion()
      {
        UnderlyingArray = numArray
      }.ImmutableArray;
    }

    /// <summary>
    /// Access the backing mutable array instance for the given <see cref="T:System.Collections.Immutable.ImmutableArray`1" />, without
    /// creating a defensive copy.  It is the responsibility of the caller to ensure the array is not modified
    /// through the returned mutable reference.  Do not mutate the returned array.
    /// </summary>
    /// <param name="array">The <see cref="T:System.Collections.Immutable.ImmutableArray`1" /> from which to retrieve the backing field.</param>
    /// <remarks>
    /// Users of this method should take extra care to ensure that the returned mutable array is never modified.
    /// The returned mutable array continues to be used as the backing field of the given <see cref="T:System.Collections.Immutable.ImmutableArray`1" />
    /// without creating a defensive copy, so changes made to the returned mutable array will be observable
    /// on the given <see cref="T:System.Collections.Immutable.ImmutableArray`1" />.  Instance and static methods of <see cref="T:System.Collections.Immutable.ImmutableArray`1" />
    /// and <see cref="T:System.Collections.Immutable.ImmutableArray" /> may malfunction if they operate on an <see cref="T:System.Collections.Immutable.ImmutableArray`1" /> instance
    /// whose underlying backing field is modified.
    /// </remarks>
    /// <returns>The underlying array, or null if <see cref="P:System.Collections.Immutable.ImmutableArray`1.IsDefault" /> is true.</returns>
    internal static byte[]? DangerousGetUnderlyingArray(ImmutableArray<byte> array) => new ImmutableByteArrayInterop.ByteArrayUnion()
    {
      ImmutableArray = array
    }.UnderlyingArray;


    #nullable disable
    [StructLayout(LayoutKind.Explicit)]
    private struct ByteArrayUnion
    {
      [FieldOffset(0)]
      internal byte[] UnderlyingArray;
      [FieldOffset(0)]
      internal ImmutableArray<byte> ImmutableArray;
    }
  }
}
