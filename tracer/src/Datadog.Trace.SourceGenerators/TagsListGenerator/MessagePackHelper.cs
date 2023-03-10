// <copyright file="MessagePackHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Text;

namespace Datadog.Trace.SourceGenerators.TagsListGenerator;

internal class MessagePackHelper
{
    /// <summary>
    /// https://github.com/msgpack/msgpack/blob/master/spec.md#overview
    /// </summary>
    internal static class MessagePackCode
    {
        public const byte MinFixInt = 0x00; // 0
        public const byte MaxFixInt = 0x7f; // 127
        public const byte MinFixMap = 0x80; // 128
        public const byte MaxFixMap = 0x8f; // 143
        public const byte MinFixArray = 0x90; // 144
        public const byte MaxFixArray = 0x9f; // 159
        public const byte MinFixStr = 0xa0; // 160
        public const byte MaxFixStr = 0xbf; // 191
        public const byte Nil = 0xc0;
        public const byte NeverUsed = 0xc1;
        public const byte False = 0xc2;
        public const byte True = 0xc3;
        public const byte Bin8 = 0xc4;
        public const byte Bin16 = 0xc5;
        public const byte Bin32 = 0xc6;
        public const byte Ext8 = 0xc7;
        public const byte Ext16 = 0xc8;
        public const byte Ext32 = 0xc9;
        public const byte Float32 = 0xca;
        public const byte Float64 = 0xcb;
        public const byte UInt8 = 0xcc;
        public const byte UInt16 = 0xcd;
        public const byte UInt32 = 0xce;
        public const byte UInt64 = 0xcf;
        public const byte Int8 = 0xd0;
        public const byte Int16 = 0xd1;
        public const byte Int32 = 0xd2;
        public const byte Int64 = 0xd3;
        public const byte FixExt1 = 0xd4;
        public const byte FixExt2 = 0xd5;
        public const byte FixExt4 = 0xd6;
        public const byte FixExt8 = 0xd7;
        public const byte FixExt16 = 0xd8;
        public const byte Str8 = 0xd9;
        public const byte Str16 = 0xda;
        public const byte Str32 = 0xdb;
        public const byte Array16 = 0xdc;
        public const byte Array32 = 0xdd;
        public const byte Map16 = 0xde;
        public const byte Map32 = 0xdf;
        public const byte MinNegativeFixInt = 0xe0; // 224
        public const byte MaxNegativeFixInt = 0xff; // 255
    }

    internal static class MessagePackRange
    {
        public const int MinFixNegativeInt = -32;
        public const int MaxFixNegativeInt = -1;
        public const int MaxFixPositiveInt = 127;
        public const int MinFixStringLength = 0;
        public const int MaxFixStringLength = 31;
        public const int MaxFixMapCount = 15;
        public const int MaxFixArrayCount = 15;
    }

#pragma warning disable SA1201
    public static IEnumerable<byte> GetValueInRawMessagePackIEnumerable(string value)
#pragma warning restore SA1201
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var byteCount = bytes.Length;
        if (byteCount <= MessagePackRange.MaxFixStringLength)
        {
            yield return (byte)(MessagePackCode.MinFixStr | byteCount);
        }
        else if (byteCount <= byte.MaxValue)
        {
            yield return MessagePackCode.Str8;
            yield return unchecked((byte)byteCount);
        }
        else if (byteCount <= ushort.MaxValue)
        {
            yield return MessagePackCode.Str8;
            yield return unchecked((byte)(byteCount >> 8));
            yield return unchecked((byte)byteCount);
        }
        else
        {
            yield return MessagePackCode.Str32;
            yield return unchecked((byte)(byteCount >> 24));
            yield return unchecked((byte)(byteCount >> 16));
            yield return unchecked((byte)(byteCount >> 8));
            yield return unchecked((byte)byteCount);
        }

        for (var i = 0; i < byteCount; i++)
        {
            yield return bytes[i];
        }
    }
}
