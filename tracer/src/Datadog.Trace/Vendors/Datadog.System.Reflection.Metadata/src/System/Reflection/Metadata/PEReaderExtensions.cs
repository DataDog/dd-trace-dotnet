// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.PEReaderExtensions
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using System.ComponentModel;
using Datadog.System.Reflection.PortableExecutable;


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
    [EditorBrowsable(EditorBrowsableState.Never)]
  public static class PEReaderExtensions
  {
    /// <summary>
    /// Returns a body block of a method with specified Relative Virtual Address (RVA);
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="peReader" /> is null.</exception>
    /// <exception cref="T:System.BadImageFormatException">The body is not found in the metadata or is invalid.</exception>
    /// <exception cref="T:System.InvalidOperationException">Section where the method is stored is not available.</exception>
    /// <exception cref="T:System.IO.IOException">IO error while reading from the underlying stream.</exception>
    public static MethodBodyBlock GetMethodBody(this PEReader peReader, int relativeVirtualAddress)
    {
      if (peReader == null)
        Throw.ArgumentNull(nameof (peReader));
      PEMemoryBlock sectionData = peReader.GetSectionData(relativeVirtualAddress);
      return sectionData.Length != 0 ? MethodBodyBlock.Create(sectionData.GetReader()) : throw new BadImageFormatException();
    }

    /// <summary>
    /// Gets a <see cref="T:System.Reflection.Metadata.MetadataReader" /> from a <see cref="T:System.Reflection.PortableExecutable.PEReader" />.
    /// </summary>
    /// <remarks>
    /// The caller must keep the <see cref="T:System.Reflection.PortableExecutable.PEReader" /> alive and undisposed throughout the lifetime of the metadata reader.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="peReader" /> is null</exception>
    /// <exception cref="T:System.PlatformNotSupportedException">The current platform is big-endian.</exception>
    /// <exception cref="T:System.IO.IOException">IO error while reading from the underlying stream.</exception>
    public static MetadataReader GetMetadataReader(this PEReader peReader) => peReader.GetMetadataReader(MetadataReaderOptions.Default, (MetadataStringDecoder) null);

    /// <summary>
    /// Gets a <see cref="T:System.Reflection.Metadata.MetadataReader" /> from a <see cref="T:System.Reflection.PortableExecutable.PEReader" />.
    /// </summary>
    /// <remarks>
    /// The caller must keep the <see cref="T:System.Reflection.PortableExecutable.PEReader" /> alive and undisposed throughout the lifetime of the metadata reader.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="peReader" /> is null</exception>
    /// <exception cref="T:System.PlatformNotSupportedException">The current platform is big-endian.</exception>
    /// <exception cref="T:System.IO.IOException">IO error while reading from the underlying stream.</exception>
    public static MetadataReader GetMetadataReader(
      this PEReader peReader,
      MetadataReaderOptions options)
    {
      return peReader.GetMetadataReader(options, (MetadataStringDecoder) null);
    }

    /// <summary>
    /// Gets a <see cref="T:System.Reflection.Metadata.MetadataReader" /> from a <see cref="T:System.Reflection.PortableExecutable.PEReader" />.
    /// </summary>
    /// <remarks>
    /// The caller must keep the <see cref="T:System.Reflection.PortableExecutable.PEReader" /> undisposed throughout the lifetime of the metadata reader.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="peReader" /> is null</exception>
    /// <exception cref="T:System.ArgumentException">The encoding of <paramref name="utf8Decoder" /> is not <see cref="T:System.Text.UTF8Encoding" />.</exception>
    /// <exception cref="T:System.PlatformNotSupportedException">The current platform is big-endian.</exception>
    /// <exception cref="T:System.IO.IOException">IO error while reading from the underlying stream.</exception>
    public static unsafe MetadataReader GetMetadataReader(
      this PEReader peReader,
      MetadataReaderOptions options,
      MetadataStringDecoder? utf8Decoder)
    {
      if (peReader == null)
        Throw.ArgumentNull(nameof (peReader));
      PEMemoryBlock metadata = peReader.GetMetadata();
      return new MetadataReader(metadata.Pointer, metadata.Length, options, utf8Decoder, (object) peReader);
    }
  }
}
