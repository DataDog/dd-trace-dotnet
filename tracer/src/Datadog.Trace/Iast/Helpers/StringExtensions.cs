// <copyright file="StringExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Security.Cryptography;
using System.Text;

namespace Datadog.Trace.Iast.Helpers
{
    internal static class StringExtensions
    {
        public static string Quote(this string text)
        {
            if (text != null && !text.StartsWith("\"") && !text.EndsWith("\""))
            {
                text = "\"" + text + "\"";
            }

            return text ?? string.Empty;
        }
    }
}
