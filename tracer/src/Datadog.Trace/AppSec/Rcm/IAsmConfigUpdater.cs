// <copyright file="IAsmConfigUpdater.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using Datadog.Trace.RemoteConfigurationManagement;

namespace Datadog.Trace.AppSec.Rcm;

internal interface IAsmConfigUpdater
{
    void ProcessUpdates(ConfigurationState configurationStatus, List<RemoteConfiguration> files);

    void ProcessRemovals(ConfigurationState configurationStatus, List<RemoteConfigurationPath> removedConfigsForThisProduct);
}
