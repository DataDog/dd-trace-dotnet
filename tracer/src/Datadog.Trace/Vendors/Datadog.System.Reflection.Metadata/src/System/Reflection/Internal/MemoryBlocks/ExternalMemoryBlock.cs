// Decompiled with JetBrains decompiler
// Type: System.Reflection.Internal.ExternalMemoryBlock
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
namespace Datadog.System.Reflection.Internal
{
  /// <summary>
  /// Class representing raw memory but not owning the memory.
  /// </summary>
  internal sealed class ExternalMemoryBlock : AbstractMemoryBlock
  {

    #nullable disable
    private readonly object _memoryOwner;
    private unsafe byte* _buffer;
    private int _size;


    #nullable enable
    public unsafe ExternalMemoryBlock(object memoryOwner, byte* buffer, int size)
    {
      this._memoryOwner = memoryOwner;
      this._buffer = buffer;
      this._size = size;
    }

    public override unsafe void Dispose()
    {
      this._buffer = (byte*) null;
      this._size = 0;
    }

    public override unsafe byte* Pointer => this._buffer;

    public override int Size => this._size;
  }
}
