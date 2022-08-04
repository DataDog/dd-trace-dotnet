// <copyright file="RcmSimulator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.RemoteConfigurationManagement;
internal class RcmSimulator : IDisposable
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<RcmSimulator>();

    private readonly FileSystemWatcher _fileSystemWatcher;
    private readonly IEnumerable<Product> _products;

    public RcmSimulator(string appBaseDir, IEnumerable<Product> products)
    {
        _products = products;

        if (Directory.Exists(appBaseDir))
        {
            _fileSystemWatcher = new FileSystemWatcher(appBaseDir)
            {
                EnableRaisingEvents = true
            };

            _fileSystemWatcher.Changed += LetsSimulate;
            _fileSystemWatcher.Created += LetsSimulate;
        }
    }

    public void Dispose()
    {
        _fileSystemWatcher?.Dispose();
    }

    private void LetsSimulate(object sender, FileSystemEventArgs e)
    {
        Log.Information("Starting Remote Config Simulation {FullPath} {ChangeType} ", e.FullPath, e.ChangeType);
        try
        {
            switch (e.ChangeType)
            {
                case WatcherChangeTypes.Created:
                case WatcherChangeTypes.Changed:
                    var fileName = Path.GetFileNameWithoutExtension(e.FullPath);
                    var product = _products.FirstOrDefault(x => x.Name == fileName);

                    if (product != null)
                    {
                        var content = File.ReadAllText(e.FullPath, Encoding.UTF8);
                        var configs = JsonConvert.DeserializeObject<List<RcmConfig>>(content);
                        product.AssignConfigs(configs);
                    }

                    Log.Information("Finished Remote Config Simulation {FullPath} {ChangeType} ", e.FullPath, e.ChangeType);
                    break;
                case WatcherChangeTypes.Deleted:
                case WatcherChangeTypes.Renamed:
                case WatcherChangeTypes.All:
                default:
                    break;
        }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error occurred during RC simulations");
        }
    }
}
