// <copyright file="Extensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Text;

namespace Datadog.Trace.SourceGenerators.NativeObjects;

internal static class Extensions
{
    public static StringBuilder AppendLineIndented(this StringBuilder stringBuilder, int indentation, string value = "")
    {
        return stringBuilder.AppendLine(Indent(indentation) + value);
    }

    public static StringBuilder AppendIndented(this StringBuilder stringBuilder, int indentation, string value = "")
    {
        return stringBuilder.Append(Indent(indentation) + value);
    }

    private static string Indent(int level) => new(' ', level * 4);
}
