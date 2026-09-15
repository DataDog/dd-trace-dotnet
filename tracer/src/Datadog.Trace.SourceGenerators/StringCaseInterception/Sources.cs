// <copyright file="Sources.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Text;

namespace Datadog.Trace.SourceGenerators.StringCaseInterception;

internal static class Sources
{
    public static string GenerateInterceptors(IReadOnlyList<string> upperAttributes, IReadOnlyList<string> lowerAttributes)
    {
        var sb = new StringBuilder(Constants.FileHeader);

        sb.Append(
            """
            namespace System.Runtime.CompilerServices
            {
                [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]
                file sealed class InterceptsLocationAttribute : global::System.Attribute
                {
                    public InterceptsLocationAttribute(int version, string data)
                    {
                    }
                }
            }

            namespace Datadog.Trace.Generated.Interceptors
            {
                internal static class StringCaseInterceptors
                {

            """);

        AppendMethod(sb, "ToUpperInvariant", upperAttributes);
        AppendMethod(sb, "ToLowerInvariant", lowerAttributes);

        sb.Append(
            """
                }
            }
            """);

        return sb.ToString();
    }

    private static void AppendMethod(StringBuilder sb, string methodName, IReadOnlyList<string> attributes)
    {
        foreach (var attribute in attributes)
        {
            sb.Append("        ").Append(attribute).Append('\n');
        }

        sb.Append("        public static string ").Append(methodName).Append("(this string value)\n");
        sb.Append("            => global::System.StringUtil.").Append(methodName).Append("(value);\n\n");
    }
}
