// <copyright file="ISecurityTransport.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;
using Datadog.Trace.AppSec.Waf;

namespace Datadog.Trace.AppSec.Transports;

internal interface ISecurityTransport
{
    IResult ShouldBlock();

    Dictionary<string, object> GetBasicRequestArgsForWaf();

    IResult RunWaf(Dictionary<string, object> args);

    void CheckAndBlock(IResult result);

    void Report(IResult result, bool blocked);

    void AddResponseHeaderTags(bool canAccessHeaders);

    void Cleanup();
}
