// Decompiled with JetBrains decompiler
// Type: System.Reflection.Internal.ExternalMemoryBlockProvider
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System.IO;


#nullable enable
namespace Datadog.System.Reflection.Internal
{
    /// <summary>Represents raw memory owned by an external object.</summary>
    internal sealed class ExternalMemoryBlockProvider : MemoryBlockProvider
  {

    #nullable disable
    private unsafe byte* _memory;
    private int _size;


    #nullable enable
    public unsafe ExternalMemoryBlockProvider(byte* memory, int size)
    {
      this._memory = memory;
      this._size = size;
    }

    public override int Size => this._size;

    protected override unsafe AbstractMemoryBlock GetMemoryBlockImpl(int start, int size) => (AbstractMemoryBlock) new ExternalMemoryBlock((object) this, this._memory + start, size);

    public override unsafe Stream GetStream(out StreamConstraints constraints)
    {
      constraints = new StreamConstraints((object) null, 0L, this._size);
      return (Stream) new ReadOnlyUnmanagedMemoryStream(this._memory, this._size);
    }

    protected override unsafe void Dispose(bool disposing)
    {
      this._memory = (byte*) null;
      this._size = 0;
    }

    public unsafe byte* Pointer => this._memory;
  }
}
