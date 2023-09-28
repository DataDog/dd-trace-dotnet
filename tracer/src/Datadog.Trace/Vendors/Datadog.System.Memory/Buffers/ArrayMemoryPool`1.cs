// Decompiled with JetBrains decompiler
// Type: System.Buffers.ArrayMemoryPool`1
// Assembly: System.Memory, Version=4.0.1.2, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51
// MVID: 805945F3-27B0-47AD-B8F6-389D9D8F82C3
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.xml

using System;
using Datadog.System.Runtime.CompilerServices.Unsafe;

namespace Datadog.System.Buffers
{
    internal sealed class ArrayMemoryPool<T> : MemoryPool<T>
  {
    private const int s_maxBufferSize = 2147483647;

    public override sealed int MaxBufferSize => int.MaxValue;

    public override sealed IMemoryOwner<T> Rent(int minimumBufferSize = -1)
    {
      if (minimumBufferSize == -1)
        minimumBufferSize = 1 + 4095 / Unsafe.SizeOf<T>();
      else if ((uint) minimumBufferSize > (uint) int.MaxValue)
        ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.minimumBufferSize);
      return (IMemoryOwner<T>) new ArrayMemoryPool<T>.ArrayMemoryPoolBuffer(minimumBufferSize);
    }

    protected override sealed void Dispose(bool disposing)
    {
    }

    private sealed class ArrayMemoryPoolBuffer : IMemoryOwner<T>, IDisposable
    {
      private T[] _array;

      public ArrayMemoryPoolBuffer(int size) => this._array = ArrayPool<T>.Shared.Rent(size);

      public Memory<T> Memory
      {
        get
        {
          T[] array = this._array;
          if (array == null)
            ThrowHelper.ThrowObjectDisposedException_ArrayMemoryPoolBuffer();
          return new Memory<T>(array);
        }
      }

      public void Dispose()
      {
        T[] array = this._array;
        if (array == null)
          return;
        this._array = (T[]) null;
        ArrayPool<T>.Shared.Return(array, false);
      }
    }
  }
}
