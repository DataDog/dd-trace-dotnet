// <copyright file="VendoredSqlHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;

namespace Datadog.Trace.DatabaseMonitoring
{
    internal static class VendoredSqlHelpers
    {
        // parse an string of the form db.schema.name where any of the three components
        // might have "[" "]" and dots within it.
        // returns:
        //   [0] dbname (or null)
        //   [1] schema (or null)
        //   [2] name
        // NOTE: if perf/space implications of Regex is not a problem, we can get rid
        // of this and use a simple regex to do the parsing
        // https://github.com/dotnet/SqlClient/blob/414f016540932d339054c61abc5ae838401cdb06/src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/SqlParameter.cs#L2433
        internal static string[] ParseTypeName(string typeName, bool isUdtTypeName)
        {
            try
            {
                string errorMsg = string.Empty;
                return MultipartIdentifier.ParseMultipartIdentifier(typeName, "[\"", "]\"", '.', 3, true, errorMsg, true);
            }
            catch (Exception)
            {
                return []; // return empty array if we can't parse the typeName
            }
        }

        // https://github.com/dotnet/SqlClient/blob/414f016540932d339054c61abc5ae838401cdb06/src/Microsoft.Data.SqlClient/netcore/src/Microsoft/Data/SqlClient/SqlCommand.cs#L6496
        // Adds quotes to each part of a SQL identifier that may be multi-part, while leaving
        //  the result as a single composite name.
        internal static string ParseAndQuoteIdentifier(string identifier, bool isUdtTypeName)
        {
            string[] strings = ParseTypeName(identifier, isUdtTypeName);

            if (strings.Length == 0)
            {
                // if we can't parse the identifier, return it as an empty string, we'll check this and not propagate if so
                return string.Empty;
            }

            return QuoteIdentifier(strings);
        }

        // https://github.com/dotnet/SqlClient/blob/414f016540932d339054c61abc5ae838401cdb06/src/Microsoft.Data.SqlClient/netcore/src/Microsoft/Data/SqlClient/SqlCommand.cs#L6502
        private static string QuoteIdentifier(ReadOnlySpan<string> strings)
        {
            StringBuilder bld = new StringBuilder();

            // Stitching back together is a little tricky. Assume we want to build a full multi-part name
            //  with all parts except trimming separators for leading empty names (null or empty strings,
            //  but not whitespace). Separators in the middle should be added, even if the name part is
            //  null/empty, to maintain proper location of the parts.
            for (int i = 0; i < strings.Length; i++)
            {
                if (0 < bld.Length)
                {
                    bld.Append('.');
                }

                if (strings[i] != null && 0 != strings[i].Length)
                {
                    AppendQuotedString(bld, "[", "]", strings[i]);
                }
            }

            return bld.ToString();
        }

        // https://github.com/dotnet/SqlClient/blob/414f016540932d339054c61abc5ae838401cdb06/src/Microsoft.Data.SqlClient/src/Microsoft/Data/Common/AdapterUtil.cs#L547
        internal static string AppendQuotedString(StringBuilder buffer, string quotePrefix, string quoteSuffix, string unQuotedString)
        {
            if (!string.IsNullOrEmpty(quotePrefix))
            {
                buffer.Append(quotePrefix);
            }

            // Assuming that the suffix is escaped by doubling it. i.e. foo"bar becomes "foo""bar".
            if (!string.IsNullOrEmpty(quoteSuffix))
            {
                int start = buffer.Length;
                buffer.Append(unQuotedString);
                buffer.Replace(quoteSuffix, quoteSuffix + quoteSuffix, start, unQuotedString.Length);
                buffer.Append(quoteSuffix);
            }
            else
            {
                buffer.Append(unQuotedString);
            }

            return buffer.ToString();
        }
    }
}
