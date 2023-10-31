// <copyright file="ApiSecurity.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Sampling;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.AppSec;

internal class ApiSecurity
{
    private readonly bool _enabled;
    private readonly OverheadController _overheadController;

    public ApiSecurity(SecuritySettings securitySettings)
    {
        _overheadController = new(1, (int)(securitySettings.ApiSecuritySampling * 100));
        // todo: later, will be enabled by default, depending on if Security is enabled
        _enabled = securitySettings.ApiSecurityEnabled;
    }

    public bool TryTellWafToAnalyzeSchema(IDictionary<string, object> args)
    {
        if (_enabled && _overheadController.AcquireRequest())
        {
            args.Add(AddressesConstants.WafContextProcessor, new Dictionary<string, bool> { { "extract-schema", true } });
            return true;
        }

        return false;
    }

    public void ReleaseRequest()
    {
        if (_enabled)
        {
            _overheadController.ReleaseRequest();
        }
    }
}
