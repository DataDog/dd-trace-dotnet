// <copyright file="AsmRemoteConfigurationProduct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.IO;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec.RcmModels;

internal abstract class AsmRemoteConfigurationProduct : Product
{
    internal abstract void UpdateRemoteConfigurationStatus(List<RemoteConfiguration> files, List<RemoteConfigurationPath> removedConfigsForThisProduct, ConfigurationStatus configurationStatus);
}
