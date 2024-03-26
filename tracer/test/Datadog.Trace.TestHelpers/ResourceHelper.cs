// <copyright file="ResourceHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.TestHelpers;

public static class ResourceHelper
{
    public static string ReadAllText<TContainerAssembly>(string resourceName, string resourceNamespace = null)
        where TContainerAssembly : class
    {
        var type = typeof(TContainerAssembly);
        Assembly assembly = type.Assembly;
        if (resourceNamespace == null)
        {
            resourceNamespace = type.Namespace;
        }

        using (Stream stream = assembly.GetManifestResourceStream(resourceNamespace + "." + resourceName))
        using (StreamReader reader = new StreamReader(stream))
        {
            return reader.ReadToEnd();
        }
    }
}
