// Decompiled with JetBrains decompiler
// Type: System.Reflection.Internal.MemoryBlockProvider
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using System.IO;


#nullable enable
namespace Datadog.System.Reflection.Internal
{
    internal abstract class MemoryBlockProvider : IDisposable
  {
    /// <summary>
    /// Creates and hydrates a memory block representing all data.
    /// </summary>
    /// <exception cref="T:System.IO.IOException">Error while reading from the memory source.</exception>
    public AbstractMemoryBlock GetMemoryBlock() => this.GetMemoryBlockImpl(0, this.Size);

    /// <summary>
    /// Creates and hydrates a memory block representing data in the specified range.
    /// </summary>
    /// <param name="start">Starting offset relative to the beginning of the data represented by this provider.</param>
    /// <param name="size">Size of the resulting block.</param>
    /// <exception cref="T:System.IO.IOException">Error while reading from the memory source.</exception>
    public AbstractMemoryBlock GetMemoryBlock(int start, int size)
    {
      if ((ulong) (uint) start + (ulong) (uint) size > (ulong) this.Size)
        Throw.ImageTooSmallOrContainsInvalidOffsetOrCount();
      return this.GetMemoryBlockImpl(start, size);
    }

    /// <exception cref="T:System.IO.IOException">IO error while reading from the underlying stream.</exception>
    protected abstract AbstractMemoryBlock GetMemoryBlockImpl(int start, int size);

    /// <summary>
    /// Gets a seekable and readable <see cref="T:System.IO.Stream" /> that can be used to read all data.
    /// The operations on the stream has to be done under a lock of <see cref="F:System.Reflection.Internal.StreamConstraints.GuardOpt" /> if non-null.
    /// The image starts at <see cref="F:System.Reflection.Internal.StreamConstraints.ImageStart" /> and has size <see cref="F:System.Reflection.Internal.StreamConstraints.ImageSize" />.
    /// It is the caller's responsibility not to read outside those bounds.
    /// </summary>
    public abstract Stream GetStream(out StreamConstraints constraints);

    /// <summary>The size of the data.</summary>
    public abstract int Size { get; }

    protected abstract void Dispose(bool disposing);

    public void Dispose()
    {
      this.Dispose(true);
      GC.SuppressFinalize((object) this);
    }
  }
}
