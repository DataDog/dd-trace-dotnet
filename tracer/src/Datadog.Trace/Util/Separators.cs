// <copyright file="Separators.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Util;

internal static class Separators
{
    public static readonly char[] Space = [' '];
    public static readonly char[] NewLine = ['\n'];
    public static readonly char[] SemiColon = [';'];
    public static readonly char[] Comma = [','];
    public static readonly char[] ForwardSlash = ['/'];
    public static readonly char[] Ampersand = ['&'];
    public static readonly char[] EqualsSign = ['='];
}
