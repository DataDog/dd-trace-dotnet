// Decompiled with JetBrains decompiler
// Type: System.Reflection.Internal.ByteSequenceComparer
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System.Collections.Generic;
using Datadog.System.Collections.Immutable;


#nullable enable
namespace Datadog.System.Reflection.Internal
{
    internal sealed class ByteSequenceComparer : 
    IEqualityComparer<byte[]>,
    IEqualityComparer<ImmutableArray<byte>>
  {
    internal static readonly ByteSequenceComparer Instance = new ByteSequenceComparer();

    private ByteSequenceComparer()
    {
    }

    internal static bool Equals(ImmutableArray<byte> x, ImmutableArray<byte> y) => MemoryExtensions.SequenceEqual<byte>(x.AsSpan(), y.AsSpan());

    internal static bool Equals(
      byte[] left,
      int leftStart,
      byte[] right,
      int rightStart,
      int length)
    {
      return MemoryExtensions.SequenceEqual<byte>(MemoryExtensions.AsSpan<byte>(left, leftStart, length), (MemoryExtensions.AsSpan<byte>(right, rightStart, length)));
    }

    internal static bool Equals(byte[]? left, byte[]? right) => MemoryExtensions.SequenceEqual<byte>(MemoryExtensions.AsSpan<byte>(left), (MemoryExtensions.AsSpan<byte>(right)));

    internal static int GetHashCode(byte[] x) => Hash.GetFNVHashCode((x));

    internal static int GetHashCode(ImmutableArray<byte> x) => Hash.GetFNVHashCode(x.AsSpan());


    #nullable disable
    bool IEqualityComparer<byte[]>.Equals(byte[] x, byte[] y) => ByteSequenceComparer.Equals(x, y);

    int IEqualityComparer<byte[]>.GetHashCode(byte[] x) => ByteSequenceComparer.GetHashCode(x);

    bool IEqualityComparer<ImmutableArray<byte>>.Equals(
      ImmutableArray<byte> x,
      ImmutableArray<byte> y)
    {
      return ByteSequenceComparer.Equals(x, y);
    }

    int IEqualityComparer<ImmutableArray<byte>>.GetHashCode(ImmutableArray<byte> x) => ByteSequenceComparer.GetHashCode(x);
  }
}
