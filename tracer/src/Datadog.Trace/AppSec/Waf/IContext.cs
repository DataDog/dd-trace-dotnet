// <copyright file="IContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

#nullable enable

namespace Datadog.Trace.AppSec.Waf;

internal interface IContext : IDisposable
{
    IResult? Run(IDictionary<string, object> addressData, ulong timeoutMicroSeconds);

    IResult? RunWithEphemeral(IDictionary<string, object> ephemeralAddressData, ulong timeoutMicroSeconds, bool isRasp);

    /// <summary>
    /// Here we compare with potential previous runs, and make sure we don't override previous values provided by the SDK
    /// </summary>
    /// <param name="security">security main instance</param>
    /// <param name="userId">user id</param>
    /// <param name="userLogin">user login</param>
    /// <param name="userSessionId">user session id</param>
    /// <param name="fromSdk">is this coming from sdk</param>
    /// <returns>filtered addresses, potentially empty if nothing should precede over sdk or same values than before</returns>
    Dictionary<string, object> FilterAddresses(IDatadogSecurity security, string? userId = null, string? userLogin = null, string? userSessionId = null, bool fromSdk = false);

    /// <summary>
    /// Special method called by RunWaf to make sure we don't override a session id provided by the sdk or run with the same value.
    /// It's similar as FilterAddresses but only for session id, behind the scenes they both call ShouldRunWith()
    /// </summary>
    /// <param name="security">security main instance</param>
    /// <param name="userSessionId">user session id</param>
    /// <param name="fromSdk">is this coming from sdk</param>
    /// <returns>whether we should add this address to the waf run</returns>
    bool ShouldRunWithSession(IDatadogSecurity security, string? userSessionId = null, bool fromSdk = false);

    void CommitUserRuns(Dictionary<string, object> addresses, bool fromSdk);
}
