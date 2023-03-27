// <copyright file="AsmFeaturesProduct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec.RcmModels;
using Datadog.Trace.RemoteConfigurationManagement;

namespace Datadog.Trace.AppSec;

internal class AsmFeaturesProduct : AsmRemoteConfigurationProduct
{
    public override string Name => "ASM_FEATURES";

    internal override void UpdateRemoteConfigurationStatus(List<RemoteConfiguration> files, List<RemoteConfigurationPath> removedConfigsForThisProduct, RemoteConfigurationStatus remoteConfigurationStatus)
    {
        var file = files.FirstOrDefault();
        if (file != null)
        {
            var asmFeatures = new NamedRawFile(file.Path, file.Contents).Deserialize<AsmFeatures>();
            remoteConfigurationStatus.EnableAsm = asmFeatures.TypedFile?.Asm.Enabled;
        }
    }
}
