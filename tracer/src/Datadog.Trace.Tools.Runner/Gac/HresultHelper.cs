// <copyright file="HresultHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tools.Runner.Gac;

internal static class HresultHelper
{
    public static string ToStringOrHex(this Hresult hresult)
    {
        var str = hresult.ToString();
        if (int.TryParse(str, out _))
        {
            str = $"0x{((int)hresult):x8}";
        }
        else
        {
            str += $" [0x{(int)hresult:x8}]";
        }

        return str;
    }
}
