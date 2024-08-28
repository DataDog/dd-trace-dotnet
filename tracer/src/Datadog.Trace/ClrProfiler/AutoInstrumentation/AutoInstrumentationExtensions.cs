// <copyright file="AutoInstrumentationExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation
{
    internal static class AutoInstrumentationExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void DisposeWithException(this Scope? scope, Exception? exception)
        {
            if (scope != null)
            {
                try
                {
                    if (exception != null)
                    {
                        scope.Span?.SetException(exception);
                    }
                }
                finally
                {
                    scope.Dispose();
                }
            }
        }

        public static bool TryGetAssemblyFileVersionFromType(Type type, out Version? version)
        {
            if (type is null)
            {
                version = null;
                return false;
            }

            // Get the assembly containing this type
            var assembly = type.Assembly;

            // Get the AssemblyFileVersion attribute from the assembly
            // Parse the file version string to a Version object and return it
            if (assembly.GetCustomAttribute<AssemblyFileVersionAttribute>() is { } attribute &&
                Version.TryParse(attribute.Version, out version))
            {
                return true;
            }

            version = null;
            return false;
        }
    }
}
