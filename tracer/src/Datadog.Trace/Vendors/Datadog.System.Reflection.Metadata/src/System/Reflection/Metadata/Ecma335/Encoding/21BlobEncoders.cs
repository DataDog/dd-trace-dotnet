// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.ArrayShapeEncoder
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Collections.Immutable;


#nullable enable
namespace Datadog.System.Reflection.Metadata.Ecma335
{
  public readonly struct ArrayShapeEncoder
  {
    public BlobBuilder Builder { get; }

    public ArrayShapeEncoder(BlobBuilder builder) => this.Builder = builder;

    /// <summary>Encodes array shape.</summary>
    /// <param name="rank">The number of dimensions in the array (shall be 1 or more).</param>
    /// <param name="sizes">
    /// Dimension sizes. The array may be shorter than <paramref name="rank" /> but not longer.
    /// </param>
    /// <param name="lowerBounds">
    /// Dimension lower bounds, or <c>default(<see cref="T:System.Collections.Immutable.ImmutableArray`1" />)</c> to set all <paramref name="rank" /> lower bounds to 0.
    /// The array may be shorter than <paramref name="rank" /> but not longer.
    /// </param>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    /// <paramref name="rank" /> is outside of range [1, 0xffff],
    /// smaller than <paramref name="sizes" />.Length, or
    /// smaller than <paramref name="lowerBounds" />.Length.
    /// </exception>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="sizes" /> is null.</exception>
    public void Shape(int rank, ImmutableArray<int> sizes, ImmutableArray<int> lowerBounds)
    {
      if ((uint) (rank - 1) > 65534U)
        Throw.ArgumentOutOfRange(nameof (rank));
      if (sizes.IsDefault)
        Throw.ArgumentNull(nameof (sizes));
      this.Builder.WriteCompressedInteger(rank);
      if (sizes.Length > rank)
        Throw.ArgumentOutOfRange(nameof (rank));
      this.Builder.WriteCompressedInteger(sizes.Length);
      foreach (int siz in sizes)
        this.Builder.WriteCompressedInteger(siz);
      if (lowerBounds.IsDefault)
      {
        this.Builder.WriteCompressedInteger(rank);
        for (int index = 0; index < rank; ++index)
          this.Builder.WriteCompressedSignedInteger(0);
      }
      else
      {
        if (lowerBounds.Length > rank)
          Throw.ArgumentOutOfRange(nameof (rank));
        this.Builder.WriteCompressedInteger(lowerBounds.Length);
        foreach (int lowerBound in lowerBounds)
          this.Builder.WriteCompressedSignedInteger(lowerBound);
      }
    }
  }
}
