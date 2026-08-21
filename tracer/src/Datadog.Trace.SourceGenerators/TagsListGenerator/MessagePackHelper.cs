// <copyright file="MessagePackHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Datadog.Trace.SourceGenerators.TagsListGenerator;

internal class MessagePackHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<byte> GetValueInRawMessagePackIEnumerable(string value)
    {
        var valueBytes = Encoding.UTF8.GetBytes(value);
        var valueLength = valueBytes.Length;
        var headerLength = valueLength <= 31 ? 1 : valueLength <= byte.MaxValue ? 2 : valueLength <= ushort.MaxValue ? 3 : 5;
        var bytes = new byte[headerLength + valueLength];

        if (valueLength <= 31)
        {
            bytes[0] = (byte)(0xa0 | valueLength);
        }
        else if (valueLength <= byte.MaxValue)
        {
            bytes[0] = 0xd9;
            bytes[1] = (byte)valueLength;
        }
        else if (valueLength <= ushort.MaxValue)
        {
            bytes[0] = 0xda;
            bytes[1] = (byte)(valueLength >> 8);
            bytes[2] = (byte)valueLength;
        }
        else
        {
            bytes[0] = 0xdb;
            bytes[1] = (byte)(valueLength >> 24);
            bytes[2] = (byte)(valueLength >> 16);
            bytes[3] = (byte)(valueLength >> 8);
            bytes[4] = (byte)valueLength;
        }

        Buffer.BlockCopy(valueBytes, 0, bytes, headerLength, valueLength);
        return bytes;
    }
}
