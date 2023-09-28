// <copyright file="Application.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

namespace Datadog.Trace.Tools.dd_dotnet.Checks.Windows.IIS
{
    internal class Application : IDisposable
    {
        private readonly IAppHostElement _application;
        private readonly IAppHostAdminManager _appHostAdminManager;

        public Application(IAppHostAdminManager appHostAdminManager, IAppHostElement element)
        {
            _appHostAdminManager = appHostAdminManager;
            _application = element;
        }

        public void Dispose()
        {
            _application.Dispose();
            // Don't dispose the IAppHostAdminManager because we don't own it
        }

        public string? GetRootDirectory()
        {
            using var virtualDirectories = _application.Collection();

            var count = virtualDirectories.Count();

            for (int i = 0; i < count; i++)
            {
                using var virtualDirectory = virtualDirectories.GetItem(i);

                if (virtualDirectory.Name() != "virtualDirectory")
                {
                    continue;
                }

                if (virtualDirectory.GetStringProperty("path") == "/")
                {
                    return virtualDirectory.GetStringProperty("physicalPath");
                }
            }

            return null;
        }

        public int GetWorkerProcess()
        {
            using var applicationPool = GetApplicationPool();

            if (applicationPool != null)
            {
                using var workerProcesses = applicationPool.GetElementByName("workerProcesses");
                var workerProcessesCollection = workerProcesses.Collection();

                if (workerProcessesCollection.Count() > 0)
                {
                    using var workerProcess = workerProcessesCollection.GetItem(0);

                    if (int.TryParse(workerProcess.GetStringProperty("processId"), out var pid))
                    {
                        return pid;
                    }
                }
            }

            return default;
        }

        public string? GetManagedRuntimeVersion()
        {
            using var applicationPool = GetApplicationPool();
            return applicationPool?.GetStringProperty("managedRuntimeVersion");
        }

        public IReadOnlyDictionary<string, string> GetAppSettings()
        {
            var result = new Dictionary<string, string>();

            using var configManager = _appHostAdminManager.GetConfigManager();

            using var section = _appHostAdminManager.GetAdminSection("appSettings", $"MACHINE/WEBROOT/APPHOST{_application.GetStringProperty("path")}");

            using var collection = section.Collection();
            var count = collection.Count();

            for (int i = 0; i < count; i++)
            {
                using var item = collection.GetItem(i);
                result.Add(item.GetStringProperty("key"), item.GetStringProperty("value"));
            }

            return result;
        }

        private IAppHostElement? GetApplicationPool()
        {
            var applicationPoolName = _application.GetStringProperty("applicationPool");

            using var applicationPoolsSection = _appHostAdminManager.GetAdminSection("system.applicationHost/applicationPools", "MACHINE/WEBROOT/APPHOST");

            using var collection = applicationPoolsSection.Collection();
            var count = collection.Count();

            for (int i = 0; i < count; i++)
            {
                IAppHostElement? item = null;

                try
                {
                    item = collection.GetItem(i);

                    if (item.GetStringProperty("name") == applicationPoolName)
                    {
                        return item;
                    }

                    item.Dispose();
                }
                catch
                {
                    item?.Dispose();
                    throw;
                }
            }

            return null;
        }
    }
}
