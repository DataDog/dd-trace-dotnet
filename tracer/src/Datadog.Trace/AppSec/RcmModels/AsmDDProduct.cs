// <copyright file="AsmDDProduct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Trace.AppSec.RcmModels;
using Datadog.Trace.RemoteConfigurationManagement;

namespace Datadog.Trace.AppSec;

internal class AsmDdProduct : AsmRemoteConfigurationProduct
{
    public override string Name => "ASM_DD";

    internal override void UpdateRemoteConfigurationStatus(List<RemoteConfiguration> files, List<RemoteConfigurationPath>? removedConfigsForThisProduct, RemoteConfigurationStatus remoteConfigurationStatus)
    {
        var firstFile = files.FirstOrDefault();
        var asmDd = new NamedRawFile(firstFile!.Path, firstFile.Contents);
        using var stream = new MemoryStream(asmDd.RawFile);
        using var streamReader = new StreamReader(stream);
        var contents = streamReader.ReadToEnd();
        if (!string.IsNullOrEmpty(contents))
        {
            remoteConfigurationStatus.RemoteRulesJson = contents;
        }
    }
}
