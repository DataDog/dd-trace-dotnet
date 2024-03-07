// <copyright file="IbmMqHeadersAdapter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Text;
using Datadog.Trace.Headers;
using Datadog.Trace.Util;
using Datadog.Trace.VendoredMicrosoftCode.System.Runtime.CompilerServices.Unsafe;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.IbmMq;

internal readonly struct IbmMqHeadersAdapter : IHeadersCollection
{
    private static readonly string[] EmptyValue = [];
    private readonly IMqMessage _message;

    public IbmMqHeadersAdapter(IMqMessage message)
    {
        _message = message;
    }

    /// <summary>
    /// Used to normalize the property name, since IBM MQ only allows names which are valid Java identifiers
    /// </summary>
    /// <param name="name">Originally provided value</param>
    /// <returns>Normalized name</returns>
    private static string NormalizeName(string name)
    {
        var sb = StringBuilderCache.Acquire(name.Length);
        foreach (var c in name)
        {
            sb.Append(c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') ? c : '_');
        }

        return StringBuilderCache.GetStringAndRelease(sb);
    }

    public IEnumerable<string> GetValues(string name)
    {
        try
        {
            // there's no way to check if the value exists,
            // and reading non-existent value causes an exception
            var normName = NormalizeName(name);
            var buf = _message.GetBytesProperty(normName);
            var val = StringFromSignedBytes(buf);

            return new[] { val };
        }
        catch
        {
            return EmptyValue;
        }
    }

    public void Set(string name, string value)
    {
        var normalizedName = NormalizeName(name);
        RemoveNormalized(normalizedName);
        var val = StringToUnsignedBytes(value);
        _message.SetBytesProperty(normalizedName, val);
    }

    public void Add(string name, string value)
    {
        Set(name, value);
    }

    private void RemoveNormalized(string normalizedName)
    {
        try
        {
            _message.DeleteProperty(normalizedName);
        }
        catch
        {
            // ignored
        }
    }

    private string StringFromSignedBytes(sbyte[] buf)
    {
        // since the text is ASCII signed and unsigned bytes are the same (0-127)
        return Encoding.ASCII.GetString(Unsafe.As<byte[]>(buf));
    }

    private sbyte[] StringToUnsignedBytes(string str)
    {
        var buf = Encoding.ASCII.GetBytes(str);
        // since the text is ASCII signed and unsigned bytes are the same.
        return Unsafe.As<sbyte[]>(buf);
    }

    public void Remove(string name)
    {
        RemoveNormalized(NormalizeName(name));
    }
}
