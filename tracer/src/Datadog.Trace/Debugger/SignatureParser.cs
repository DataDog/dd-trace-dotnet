// <copyright file="SignatureParser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.Logging;
using Datadog.Trace.VendoredMicrosoftCode.System.Buffers;

#nullable enable
namespace Datadog.Trace.Debugger
{
    internal static class SignatureParser
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SignatureParser));

        internal static bool TryParse(string? signature, [NotNullWhen(true)] out string[]? signatures)
        {
            try
            {
                signatures = Parse(signature);
            }
            catch (Exception e)
            {
                Log.Error(e, "Error parsing the signature {Signature}. Fall back to empty signature", signature);
                signatures = null;
            }

            return !string.IsNullOrEmpty(signature);
        }

        internal static string[]? Parse(string? signature)
        {
            if (string.IsNullOrWhiteSpace(signature) || signature == "()")
            {
                return null;
            }

            signature = signature!.Replace(" ", string.Empty);

            if (signature.StartsWith("(") && signature.EndsWith(")"))
            {
                signature = signature.Substring(1, signature.Length - 2);
            }

            // Fast path if we don't deal with generics
            if (!signature.Contains("<") && !signature.Contains(">"))
            {
                return signature.Split(',');
            }

            return ParseGenericTypes(signature);
        }

        private static string[]? ParseGenericTypes(string signature)
        {
            var result = new List<string>();
            var bufferIndex = 0;
            var genericDepth = 0;
            var buffer = ArrayPool<char>.Shared.Rent(signature.Length);

            try
            {
                for (var i = 0; i < signature.Length; i++)
                {
                    var c = signature[i];

                    if (c == ',' && genericDepth == 0)
                    {
                        if (bufferIndex > 0)
                        {
                            result.Add(new string(buffer, 0, bufferIndex));
                            bufferIndex = 0;
                        }
                    }
                    else if (i < signature.Length - 1 && signature[i] == '<' && signature[i + 1] == '>')
                    {
                        buffer[bufferIndex++] = '<';
                        buffer[bufferIndex++] = '>';
                        i++; // Skip the next '>'
                    }
                    else if (c == '<')
                    {
                        genericDepth++;
                        buffer[bufferIndex++] = '[';
                    }
                    else if (c == '>')
                    {
                        if (genericDepth == 0)
                        {
                            return null; // Unmatched closing generic bracket
                        }

                        genericDepth--;
                        buffer[bufferIndex++] = ']';
                    }
                    else
                    {
                        buffer[bufferIndex++] = c;
                    }
                }

                if (genericDepth != 0)
                {
                    return null; // Unmatched opening generic bracket
                }

                if (bufferIndex > 0)
                {
                    result.Add(new string(buffer, 0, bufferIndex));
                }

                return result.Count > 0 ? result.ToArray() : null;
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer, true);
            }
        }
    }
}
