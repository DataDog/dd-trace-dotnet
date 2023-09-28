// Decompiled with JetBrains decompiler
// Type: System.Reflection.Internal.MemoryMappedFileBlock
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;


#nullable enable
namespace Datadog.System.Reflection.Internal
{
    internal sealed class MemoryMappedFileBlock : AbstractMemoryBlock
  {

    #nullable disable
    private readonly MemoryMappedFileBlock.DisposableData _data;
    private readonly int _size;


    #nullable enable
    internal MemoryMappedFileBlock(
      IDisposable accessor,
      SafeBuffer safeBuffer,
      long offset,
      int size)
    {
      this._data = new MemoryMappedFileBlock.DisposableData(accessor, safeBuffer, offset);
      this._size = size;
    }

    public override void Dispose() => this._data.Dispose();

    public override unsafe byte* Pointer => this._data.Pointer;

    public override int Size => this._size;


    #nullable disable
    private sealed class DisposableData : CriticalDisposableObject
    {
      private IDisposable _accessor;
      private SafeBuffer _safeBuffer;
      private unsafe byte* _pointer;

      public unsafe DisposableData(IDisposable accessor, SafeBuffer safeBuffer, long offset)
      {
        RuntimeHelpers.PrepareConstrainedRegions();
        try
        {
        }
        finally
        {
          byte* pointer = (byte*) null;
          safeBuffer.AcquirePointer(ref pointer);
          this._accessor = accessor;
          this._safeBuffer = safeBuffer;
          this._pointer = pointer + offset;
        }
      }

      protected override unsafe void Release()
      {
        RuntimeHelpers.PrepareConstrainedRegions();
        try
        {
        }
        finally
        {
          Interlocked.Exchange<SafeBuffer>(ref this._safeBuffer, (SafeBuffer) null)?.ReleasePointer();
          Interlocked.Exchange<IDisposable>(ref this._accessor, (IDisposable) null)?.Dispose();
        }
        this._pointer = (byte*) null;
      }

      public unsafe byte* Pointer => this._pointer;
    }
  }
}
