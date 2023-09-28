// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.MetadataStringDecoder
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System.Text;


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
    /// <summary>
    /// Provides the <see cref="T:System.Reflection.Metadata.MetadataReader" /> with a custom mechanism for decoding
    /// byte sequences in metadata that represent text.
    /// </summary>
    /// <remarks>
    /// This can be used for the following purposes:
    /// 
    /// 1) To customize the treatment of invalid input. When no decoder is provided,
    ///    the <see cref="T:System.Reflection.Metadata.MetadataReader" /> uses the default fallback replacement
    ///    with \uFFFD)
    /// 
    /// 2) To reuse existing strings instead of allocating a new one for each decoding
    ///    operation.
    /// </remarks>
    public class MetadataStringDecoder
  {
    /// <summary>Gets the encoding used by this instance.</summary>
    public Encoding Encoding { get; }

    /// <summary>
    /// The default decoder used by <see cref="T:System.Reflection.Metadata.MetadataReader" /> to decode UTF-8 when
    /// no decoder is provided to the constructor.
    /// </summary>
    public static MetadataStringDecoder DefaultUTF8 { get; } = new MetadataStringDecoder(Encoding.UTF8);

    /// <summary>
    /// Creates a <see cref="T:System.Reflection.Metadata.MetadataStringDecoder" /> for the given encoding.
    /// </summary>
    /// <param name="encoding">The encoding to use.</param>
    /// <remarks>
    /// To cache and reuse existing strings. Create a derived class and override <see cref="M:System.Reflection.Metadata.MetadataStringDecoder.GetString(System.Byte*,System.Int32)" />
    /// </remarks>
    public MetadataStringDecoder(Encoding encoding)
    {
      if (encoding == null)
        Throw.ArgumentNull(nameof (encoding));
      this.Encoding = encoding;
    }

    /// <summary>
    /// The mechanism through which the <see cref="T:System.Reflection.Metadata.MetadataReader" /> obtains strings
    /// for byte sequences in metadata. Override this to cache strings if required.
    /// Otherwise, it is implemented by forwarding straight to <see cref="P:System.Reflection.Metadata.MetadataStringDecoder.Encoding" />
    /// and every call will allocate a new string.
    /// </summary>
    /// <param name="bytes">Pointer to bytes to decode.</param>
    /// <param name="byteCount">Number of bytes to decode.</param>
    /// <returns>The decoded string.</returns>
    public virtual unsafe string GetString(byte* bytes, int byteCount) => this.Encoding.GetString(bytes, byteCount);
  }
}
