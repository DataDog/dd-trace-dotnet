// <copyright file="ApplicationPool.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#pragma warning disable SA1108 // BlockStatementsMustNotContainEmbeddedComments

using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Datadog.Trace.Tools.dd_dotnet.Checks.Windows.IIS
{
    internal class ApplicationPool : IDisposable
    {
        private readonly IAppHostElement _applicationPool;

        public ApplicationPool(IAppHostElement element)
        {
            _applicationPool = element;
        }

        public void Dispose()
        {
            _applicationPool.Dispose();
            // Don't dispose the IAppHostAdminManager because we don't own it
        }

        public int GetWorkerProcess()
        {
            using var workerProcesses = _applicationPool.GetElementByName("workerProcesses");
            var workerProcessesCollection = workerProcesses.Collection();

            if (workerProcessesCollection.Count() > 0)
            {
                using var workerProcess = workerProcessesCollection.GetItem(0);

                if (int.TryParse(workerProcess.GetStringProperty("processId"), out var pid))
                {
                    return pid;
                }
            }

            return default;
        }

        public string GetName()
        {
            return _applicationPool.GetStringProperty("name");
        }

        public string GetManagedRuntimeVersion()
        {
            return _applicationPool.GetStringProperty("managedRuntimeVersion");
        }

        public IReadOnlyDictionary<string, string> GetEnvironmentVariables()
        {
            var result = new Dictionary<string, string>();

            try
            {
                using var environmentVariables = _applicationPool.GetElementByName("environmentVariables");
                using var collection = environmentVariables.Collection();
                var count = collection.Count();

                for (int i = 0; i < count; i++)
                {
                    using var item = collection.GetItem(i);
                    result.Add(item.GetStringProperty("name"), item.GetStringProperty("value"));
                }
            }
            catch (Win32Exception ex) when ((uint)ex.NativeErrorCode == 0x80070585) // Invalid index
            {
                Utils.WriteWarning("Could not read IIS environment variables. This is expected if using a version of IIS prior to 10.");
            }

            return result;
        }
    }
}
