// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.ArrayShape
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Collections.Immutable;

namespace Datadog.System.Reflection.Metadata
{
  /// <summary>Represents the shape of an array type.</summary>
  public readonly struct ArrayShape
  {
    /// <summary>Gets the number of dimensions in the array.</summary>
    public int Rank { get; }

    /// <summary>
    /// Gets the sizes of each dimension. Length may be smaller than rank, in which case the trailing dimensions have unspecified sizes.
    /// </summary>
    public ImmutableArray<int> Sizes { get; }

    /// <summary>
    /// Gets the lower-bounds of each dimension. Length may be smaller than rank, in which case the trailing dimensions have unspecified lower bounds.
    /// </summary>
    public ImmutableArray<int> LowerBounds { get; }

    public ArrayShape(int rank, ImmutableArray<int> sizes, ImmutableArray<int> lowerBounds)
    {
      this.Rank = rank;
      this.Sizes = sizes;
      this.LowerBounds = lowerBounds;
    }
  }
}
