// <copyright file="StackWalker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Diagnostics;
using System.Linq;

namespace Datadog.Trace.Iast;

internal static class StackWalker
{
    public static readonly string[] ExcludeSpanGenerationTypes = { "Datadog.Trace.Debugger.Helpers.StringExtensions", "Microsoft.AspNetCore.Razor.Language.StreamSourceDocument", "System.Security.IdentityHelper" };
    public static readonly string[] AssemblyNamesToSkip =
    {
        "Datadog.Trace",
        "Microsoft.Data.SqlClient",
        "Microsoft.Data.Sqlite",
        "MySql.Data",
        "MySqlConnector",
        "Npgsql",
        "Oracle.DataAccess",
        "Oracle.ManagedDataAccess",
        "System.Data",
        "System",
        "System.Data.Common",
        "System.Security.Cryptography",
        "System.Security.Cryptography.Algorithms",
        "System.Security.Cryptography.Csp",
        "System.Security.Cryptography.Primitives",
        "System.Data.SqlClient",
        "System.Data.SQLite",
        "System.Diagnostics.Process",
        "System.Configuration.ConfigurationManager",
        "linq2db"
    };

    private const int DefaultSkipFrames = 2;

    public static StackFrameInfo GetFrame()
    {
        var stackTrace = new StackTrace(DefaultSkipFrames, true);

        foreach (var frame in stackTrace.GetFrames())
        {
            var declaringType = frame?.GetMethod()?.DeclaringType;
            if (ExcludeSpanGenerationTypes.Contains(declaringType?.FullName))
            {
                return new StackFrameInfo(null, false);
            }

            var assembly = declaringType?.Assembly.GetName().Name;
            if (assembly != null && !AssemblyNamesToSkip.Contains(assembly))
            {
                return new StackFrameInfo(frame, true);
            }
        }

        return new StackFrameInfo(null, true);
    }
}
