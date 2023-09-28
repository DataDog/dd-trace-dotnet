// Decompiled with JetBrains decompiler
// Type: System.Reflection.Internal.EncodingHelper
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using System.Runtime.InteropServices;
using Datadog.System.Reflection.Metadata;


#nullable enable
namespace Datadog.System.Reflection.Internal
{
    /// <summary>
    /// Provides helpers to decode strings from unmanaged memory to System.String while avoiding
    /// intermediate allocation.
    /// </summary>
    internal static class EncodingHelper
  {
    public const int PooledBufferSize = 200;

    #nullable disable
    private static readonly ObjectPool<byte[]> s_pool = new ObjectPool<byte[]>((Func<byte[]>) (() => new byte[200]));


    #nullable enable
    public static unsafe string DecodeUtf8(
      byte* bytes,
      int byteCount,
      byte[] prefix,
      MetadataStringDecoder utf8Decoder)
    {
      if (prefix != null)
        return EncodingHelper.DecodeUtf8Prefixed(bytes, byteCount, prefix, utf8Decoder);
      return byteCount == 0 ? string.Empty : utf8Decoder.GetString(bytes, byteCount);
    }


    #nullable disable
    private static unsafe string DecodeUtf8Prefixed(
      byte* bytes,
      int byteCount,
      byte[] prefix,
      MetadataStringDecoder utf8Decoder)
    {
      int byteCount1 = byteCount + prefix.Length;
      if (byteCount1 == 0)
        return string.Empty;
      byte[] numArray = EncodingHelper.AcquireBuffer(byteCount1);
      prefix.CopyTo((Array) numArray, 0);
      Marshal.Copy((IntPtr) (void*) bytes, numArray, prefix.Length, byteCount);
      string str;
      fixed (byte* bytes1 = &numArray[0])
        str = utf8Decoder.GetString(bytes1, byteCount1);
      EncodingHelper.ReleaseBuffer(numArray);
      return str;
    }

    private static byte[] AcquireBuffer(int byteCount) => byteCount > 200 ? new byte[byteCount] : EncodingHelper.s_pool.Allocate();

    private static void ReleaseBuffer(byte[] buffer)
    {
      if (buffer.Length != 200)
        return;
      EncodingHelper.s_pool.Free(buffer);
    }
  }
}
