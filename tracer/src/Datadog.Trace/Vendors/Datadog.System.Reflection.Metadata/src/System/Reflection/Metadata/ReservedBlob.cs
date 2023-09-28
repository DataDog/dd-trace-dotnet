// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.ReservedBlob`1
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

namespace Datadog.System.Reflection.Metadata
{
  /// <summary>
  /// Represents a handle and a corresponding blob on a metadata heap that was reserved for future content update.
  /// </summary>
  public readonly struct ReservedBlob<THandle> where THandle : struct
  {
    public THandle Handle { get; }

    public Blob Content { get; }

    internal ReservedBlob(THandle handle, Blob content)
    {
      this.Handle = handle;
      this.Content = content;
    }

    /// <summary>
    /// Returns a <see cref="T:System.Reflection.Metadata.BlobWriter" /> to be used to update the content.
    /// </summary>
    public BlobWriter CreateWriter() => new BlobWriter(this.Content);
  }
}
