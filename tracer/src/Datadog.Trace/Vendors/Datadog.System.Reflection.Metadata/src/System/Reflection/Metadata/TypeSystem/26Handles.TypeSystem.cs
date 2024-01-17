﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.StringHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using Datadog.System.Reflection.Metadata.Ecma335;


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  public readonly struct StringHandle : IEquatable<StringHandle>
  {
    private readonly uint _value;

    private StringHandle(uint value) => this._value = value;

    internal static StringHandle FromOffset(int heapOffset) => new StringHandle((uint) (0 | heapOffset));

    internal static StringHandle FromVirtualIndex(StringHandle.VirtualIndex virtualIndex) => new StringHandle((uint) TokenTypeIds.VirtualTokenMask | TokenTypeIds.String | (uint)virtualIndex);

    internal static StringHandle FromWriterVirtualIndex(int virtualIndex) => new StringHandle((uint) (int.MinValue | virtualIndex));

    internal StringHandle WithWinRTPrefix() => new StringHandle(2684354560U | this._value);

    internal StringHandle WithDotTermination() => new StringHandle(536870912U | this._value);

    internal StringHandle SuffixRaw(int prefixByteLength) => new StringHandle((uint) (0 | (int) this._value + prefixByteLength));

    public static implicit operator Handle(StringHandle handle) => new Handle((byte) ((handle._value & 2147483648U) >> 24 | 120U | (handle._value & 1610612736U) >> 29), (int) handle._value & 536870911);

    public static explicit operator StringHandle(Handle handle)
    {
      if (((int) handle.VType & -132) != 120)
        Throw.InvalidCast();
      return new StringHandle((uint) (((int) handle.VType & 128) << 24 | ((int) handle.VType & 3) << 29 | handle.Offset));
    }

    internal uint RawValue => this._value;

    internal bool IsVirtual => (this._value & 2147483648U) > 0U;

    public bool IsNil => ((int) this._value & -1610612737) == 0;

    internal int GetHeapOffset() => (int) this._value & 536870911;

    internal StringHandle.VirtualIndex GetVirtualIndex() => (StringHandle.VirtualIndex) ((int) this._value & 536870911);

    internal int GetWriterVirtualIndex() => (int) this._value & 536870911;

    internal StringKind StringKind => (StringKind) (this._value >> 29);

    public override bool Equals(object? obj) => obj is StringHandle other && this.Equals(other);

    public bool Equals(StringHandle other) => (int) this._value == (int) other._value;

    public override int GetHashCode() => (int) this._value;

    public static bool operator ==(StringHandle left, StringHandle right) => left.Equals(right);

    public static bool operator !=(StringHandle left, StringHandle right) => !left.Equals(right);

    internal enum VirtualIndex
    {
      System_Runtime_WindowsRuntime,
      System_Runtime,
      System_ObjectModel,
      System_Runtime_WindowsRuntime_UI_Xaml,
      System_Runtime_InteropServices_WindowsRuntime,
      System_Numerics_Vectors,
      Dispose,
      AttributeTargets,
      AttributeUsageAttribute,
      Color,
      CornerRadius,
      DateTimeOffset,
      Duration,
      DurationType,
      EventHandler1,
      EventRegistrationToken,
      Exception,
      GeneratorPosition,
      GridLength,
      GridUnitType,
      ICommand,
      IDictionary2,
      IDisposable,
      IEnumerable,
      IEnumerable1,
      IList,
      IList1,
      INotifyCollectionChanged,
      INotifyPropertyChanged,
      IReadOnlyDictionary2,
      IReadOnlyList1,
      KeyTime,
      KeyValuePair2,
      Matrix,
      Matrix3D,
      Matrix3x2,
      Matrix4x4,
      NotifyCollectionChangedAction,
      NotifyCollectionChangedEventArgs,
      NotifyCollectionChangedEventHandler,
      Nullable1,
      Plane,
      Point,
      PropertyChangedEventArgs,
      PropertyChangedEventHandler,
      Quaternion,
      Rect,
      RepeatBehavior,
      RepeatBehaviorType,
      Size,
      System,
      System_Collections,
      System_Collections_Generic,
      System_Collections_Specialized,
      System_ComponentModel,
      System_Numerics,
      System_Windows_Input,
      Thickness,
      TimeSpan,
      Type,
      Uri,
      Vector2,
      Vector3,
      Vector4,
      Windows_Foundation,
      Windows_UI,
      Windows_UI_Xaml,
      Windows_UI_Xaml_Controls_Primitives,
      Windows_UI_Xaml_Media,
      Windows_UI_Xaml_Media_Animation,
      Windows_UI_Xaml_Media_Media3D,
      Count,
    }
  }
}
