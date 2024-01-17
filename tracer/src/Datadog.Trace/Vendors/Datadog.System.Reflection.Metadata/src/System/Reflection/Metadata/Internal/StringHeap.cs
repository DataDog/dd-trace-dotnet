﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.StringHeap
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Datadog.System.Reflection.Internal;


#nullable enable
namespace Datadog.System.Reflection.Metadata.Ecma335
{
    internal struct StringHeap
  {

    #nullable disable
    private static string[] s_virtualValues;
    internal readonly MemoryBlock Block;
    private VirtualHeap _lazyVirtualHeap;

    internal StringHeap(MemoryBlock block, MetadataKind metadataKind)
    {
      this._lazyVirtualHeap = (VirtualHeap) null;
      if (StringHeap.s_virtualValues == null && metadataKind != MetadataKind.Ecma335)
        StringHeap.s_virtualValues = new string[71]
        {
          "System.Runtime.WindowsRuntime",
          "System.Runtime",
          "System.ObjectModel",
          "System.Runtime.WindowsRuntime.UI.Xaml",
          "System.Runtime.InteropServices.WindowsRuntime",
          "System.Numerics.Vectors",
          "Dispose",
          "AttributeTargets",
          "AttributeUsageAttribute",
          "Color",
          "CornerRadius",
          "DateTimeOffset",
          "Duration",
          "DurationType",
          "EventHandler`1",
          "EventRegistrationToken",
          "Exception",
          "GeneratorPosition",
          "GridLength",
          "GridUnitType",
          "ICommand",
          "IDictionary`2",
          "IDisposable",
          "IEnumerable",
          "IEnumerable`1",
          "IList",
          "IList`1",
          "INotifyCollectionChanged",
          "INotifyPropertyChanged",
          "IReadOnlyDictionary`2",
          "IReadOnlyList`1",
          "KeyTime",
          "KeyValuePair`2",
          "Matrix",
          "Matrix3D",
          "Matrix3x2",
          "Matrix4x4",
          "NotifyCollectionChangedAction",
          "NotifyCollectionChangedEventArgs",
          "NotifyCollectionChangedEventHandler",
          "Nullable`1",
          "Plane",
          "Point",
          "PropertyChangedEventArgs",
          "PropertyChangedEventHandler",
          "Quaternion",
          "Rect",
          "RepeatBehavior",
          "RepeatBehaviorType",
          "Size",
          "System",
          "System.Collections",
          "System.Collections.Generic",
          "System.Collections.Specialized",
          "System.ComponentModel",
          "System.Numerics",
          "System.Windows.Input",
          "Thickness",
          "TimeSpan",
          "Type",
          "Uri",
          "Vector2",
          "Vector3",
          "Vector4",
          "Windows.Foundation",
          "Windows.UI",
          "Windows.UI.Xaml",
          "Windows.UI.Xaml.Controls.Primitives",
          "Windows.UI.Xaml.Media",
          "Windows.UI.Xaml.Media.Animation",
          "Windows.UI.Xaml.Media.Media3D"
        };
      this.Block = StringHeap.TrimEnd(block);
    }

    [Conditional("DEBUG")]
    private static void AssertFilled()
    {
      int num = 0;
      while (num < StringHeap.s_virtualValues.Length)
        ++num;
    }

    private static MemoryBlock TrimEnd(MemoryBlock block)
    {
      if (block.Length == 0)
        return block;
      int offset = block.Length - 1;
      while (offset >= 0 && block.PeekByte(offset) == (byte) 0)
        --offset;
      return offset == block.Length - 1 ? block : block.GetMemoryBlockAt(0, offset + 2);
    }


    #nullable enable
    internal string GetString(StringHandle handle, MetadataStringDecoder utf8Decoder) => !handle.IsVirtual ? this.GetNonVirtualString(handle, utf8Decoder, (byte[]) null) : this.GetVirtualHandleString(handle, utf8Decoder);

    internal MemoryBlock GetMemoryBlock(StringHandle handle) => !handle.IsVirtual ? this.GetNonVirtualStringMemoryBlock(handle) : this.GetVirtualHandleMemoryBlock(handle);

    internal static string GetVirtualString(StringHandle.VirtualIndex index) => StringHeap.s_virtualValues[(int) index];


    #nullable disable
    private string GetNonVirtualString(
      StringHandle handle,
      MetadataStringDecoder utf8Decoder,
      byte[] prefixOpt)
    {
      char terminator = handle.StringKind == StringKind.DotTerminated ? '.' : char.MinValue;
      return this.Block.PeekUtf8NullTerminated(handle.GetHeapOffset(), prefixOpt, utf8Decoder, out int _, terminator);
    }

    private unsafe MemoryBlock GetNonVirtualStringMemoryBlock(StringHandle handle)
    {
      char terminator = handle.StringKind == StringKind.DotTerminated ? '.' : char.MinValue;
      int heapOffset = handle.GetHeapOffset();
      int terminatedLength = this.Block.GetUtf8NullTerminatedLength(heapOffset, out int _, terminator);
      return new MemoryBlock(this.Block.Pointer + heapOffset, terminatedLength);
    }

    private unsafe byte[] GetNonVirtualStringBytes(StringHandle handle, byte[] prefix)
    {
      MemoryBlock stringMemoryBlock = this.GetNonVirtualStringMemoryBlock(handle);
      byte[] virtualStringBytes = new byte[prefix.Length + stringMemoryBlock.Length];
      Buffer.BlockCopy((Array) prefix, 0, (Array) virtualStringBytes, 0, prefix.Length);
      Marshal.Copy((IntPtr) (void*) stringMemoryBlock.Pointer, virtualStringBytes, prefix.Length, stringMemoryBlock.Length);
      return virtualStringBytes;
    }

    private string GetVirtualHandleString(StringHandle handle, MetadataStringDecoder utf8Decoder)
    {
      switch (handle.StringKind)
      {
        case StringKind.Virtual:
          return StringHeap.GetVirtualString(handle.GetVirtualIndex());
        case StringKind.WinRTPrefixed:
          return this.GetNonVirtualString(handle, utf8Decoder, MetadataReader.WinRTPrefix);
        default:
          throw ExceptionUtilities.UnexpectedValue((object) handle.StringKind);
      }
    }

    private MemoryBlock GetVirtualHandleMemoryBlock(StringHandle handle)
    {
      VirtualHeap virtualHeap = VirtualHeap.GetOrCreateVirtualHeap(ref this._lazyVirtualHeap);
      lock (virtualHeap)
      {
        MemoryBlock block;
        if (!virtualHeap.TryGetMemoryBlock(handle.RawValue, out block))
        {
          byte[] numArray1;
          switch (handle.StringKind)
          {
            case StringKind.Virtual:
              numArray1 = Encoding.UTF8.GetBytes(StringHeap.GetVirtualString(handle.GetVirtualIndex()));
              break;
            case StringKind.WinRTPrefixed:
              numArray1 = this.GetNonVirtualStringBytes(handle, MetadataReader.WinRTPrefix);
              break;
            default:
              throw ExceptionUtilities.UnexpectedValue((object) handle.StringKind);
          }
          byte[] numArray2 = numArray1;
          block = virtualHeap.AddBlob(handle.RawValue, numArray2);
        }
        return block;
      }
    }

    internal BlobReader GetBlobReader(StringHandle handle) => new BlobReader(this.GetMemoryBlock(handle));

    internal StringHandle GetNextHandle(StringHandle handle)
    {
      if (handle.IsVirtual)
        return new StringHandle();
      int num = this.Block.IndexOf((byte) 0, handle.GetHeapOffset());
      return num == -1 || num == this.Block.Length - 1 ? new StringHandle() : StringHandle.FromOffset(num + 1);
    }


    #nullable enable
    internal bool Equals(
      StringHandle handle,
      string value,
      MetadataStringDecoder utf8Decoder,
      bool ignoreCase)
    {
      if (handle.IsVirtual)
        return string.Equals(this.GetString(handle, utf8Decoder), value, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
      if (handle.IsNil)
        return value.Length == 0;
      char terminator = handle.StringKind == StringKind.DotTerminated ? '.' : char.MinValue;
      return this.Block.Utf8NullTerminatedEquals(handle.GetHeapOffset(), value, utf8Decoder, terminator, ignoreCase);
    }

    internal bool StartsWith(
      StringHandle handle,
      string value,
      MetadataStringDecoder utf8Decoder,
      bool ignoreCase)
    {
      if (handle.IsVirtual)
        return this.GetString(handle, utf8Decoder).StartsWith(value, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
      if (handle.IsNil)
        return value.Length == 0;
      char terminator = handle.StringKind == StringKind.DotTerminated ? '.' : char.MinValue;
      return this.Block.Utf8NullTerminatedStartsWith(handle.GetHeapOffset(), value, utf8Decoder, terminator, ignoreCase);
    }

    /// <summary>
    /// Returns true if the given raw (non-virtual) handle represents the same string as given ASCII string.
    /// </summary>
    internal bool EqualsRaw(StringHandle rawHandle, string asciiString) => this.Block.CompareUtf8NullTerminatedStringWithAsciiString(rawHandle.GetHeapOffset(), asciiString) == 0;

    /// <summary>
    /// Returns the heap index of the given ASCII character or -1 if not found prior null terminator or end of heap.
    /// </summary>
    internal int IndexOfRaw(int startIndex, char asciiChar) => this.Block.Utf8NullTerminatedOffsetOfAsciiChar(startIndex, asciiChar);

    /// <summary>
    /// Returns true if the given raw (non-virtual) handle represents a string that starts with given ASCII prefix.
    /// </summary>
    internal bool StartsWithRaw(StringHandle rawHandle, string asciiPrefix) => this.Block.Utf8NullTerminatedStringStartsWithAsciiPrefix(rawHandle.GetHeapOffset(), asciiPrefix);

    /// <summary>
    /// Equivalent to Array.BinarySearch, searches for given raw (non-virtual) handle in given array of ASCII strings.
    /// </summary>
    internal int BinarySearchRaw(string[] asciiKeys, StringHandle rawHandle) => this.Block.BinarySearch(asciiKeys, rawHandle.GetHeapOffset());
  }
}
