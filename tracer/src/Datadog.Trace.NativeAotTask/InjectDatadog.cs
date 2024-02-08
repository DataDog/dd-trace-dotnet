// <copyright file="InjectDatadog.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Datadog.Trace.NativeAotTask;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600
public class InjectDatadog : Task
{
    [Required]
    public string IlcRspFile { get; set; }

    [Required]
    public string IntermediateOutputPath { get; set; }

    public override bool Execute()
    {
        Log.LogWarning($"Executing InjectDatadog task - IlcRspFile: {IlcRspFile} - IntermediateOutputPath: {IntermediateOutputPath}");

        var datadogFolder = Path.Combine(IntermediateOutputPath, "datadog");
        Directory.CreateDirectory(datadogFolder);

        var ilcRsp = File.ReadAllLines(IlcRspFile);

        var assembliesToPatch = new List<string>();

        for (int i = 0; i < ilcRsp.Length; i++)
        {
            ref var line = ref ilcRsp[i];

            if (assembliesToPatch.Count == 0)
            {
                var path = line;
                var newPath = Path.Combine(datadogFolder, Path.GetFileName(path));
                File.Copy(path!, newPath, true);

                assembliesToPatch.Add(newPath);

                line = newPath;
            }
            else if (line.StartsWith("-r:"))
            {
                var path = line.Substring(3);
                var newPath = Path.Combine(datadogFolder, Path.GetFileName(path));

                File.Copy(path, newPath, true);

                assembliesToPatch.Add(newPath);

                line = $"-r:{newPath}";
            }
        }

        File.WriteAllLines(IlcRspFile, ilcRsp);

        Log.LogWarning("Calling AotProcessor.Invoke");

        try
        {
            var loadContext = new CustomAssemblyLoadContext(Log);

            var aotProcessorType = loadContext.LoadFromAssemblyName(typeof(InjectDatadog).Assembly.GetName()).GetType("Datadog.Trace.NativeAotTask.AotProcessor");
            aotProcessorType.InvokeMember(
                "Invoke",
                BindingFlags.InvokeMethod | BindingFlags.Static | BindingFlags.NonPublic,
                null,
                null,
                [assembliesToPatch, new Action<string>(s => Log.LogWarning(s))]);

            Log.LogWarning("After AotProcessor.Invoke call");
        }
        catch (Exception e)
        {
            Log.LogWarning("Caught exeption: " + e.ToString());
            Log.LogErrorFromException(e, true);
        }

        Log.LogWarning("Done");
        return true;
    }

    private class CustomAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly string _folder;
        private readonly TaskLoggingHelper _log;

        public CustomAssemblyLoadContext(TaskLoggingHelper logger)
        {
            _folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _log = logger;
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            var expectedFile = Path.Combine(_folder, $"{assemblyName.Name}.dll");

            if (File.Exists(expectedFile))
            {
                return LoadFromAssemblyPath(expectedFile);
            }

            return Default.LoadFromAssemblyName(assemblyName);
        }
    }
}
