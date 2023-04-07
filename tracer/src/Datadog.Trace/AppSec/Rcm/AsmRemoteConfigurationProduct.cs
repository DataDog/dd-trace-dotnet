// <copyright file="AsmRemoteConfigurationProduct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;
using Datadog.Trace.RemoteConfigurationManagement;

namespace Datadog.Trace.AppSec.Rcm;

internal abstract class AsmRemoteConfigurationProduct
{
    internal virtual void UpdateRemoteConfigurationStatus(List<RemoteConfiguration>? files, List<RemoteConfigurationPath>? removedConfigsForThisProduct, ConfigurationStatus configurationStatus)
    {
        if (removedConfigsForThisProduct != null)
        {
            ProcessRemovals(configurationStatus, removedConfigsForThisProduct);
        }

        if (files != null)
        {
            ProcessUpdates(configurationStatus, files);
        }
    }

    protected abstract void ProcessUpdates(ConfigurationStatus configurationStatus, List<RemoteConfiguration> files);

    protected abstract void ProcessRemovals(ConfigurationStatus configurationStatus, List<RemoteConfigurationPath> removedConfigsForThisProduct);
}
