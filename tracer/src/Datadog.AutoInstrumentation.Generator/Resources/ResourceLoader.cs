// <copyright file="ResourceLoader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using System.Reflection;

namespace Datadog.AutoInstrumentation.Generator.Resources;

internal class ResourceLoader
{
    private static readonly string Prefix = typeof(ResourceLoader).Namespace + ".Data.";

    internal static string LoadResource(string fileName)
    {
        var stream = typeof(ResourceLoader).GetTypeInfo().Assembly.GetManifestResourceStream(Prefix + fileName);
        if (stream == null)
        {
            return string.Empty;
        }

        using (stream)
        {
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
