// <copyright file="IRemoteConfigurationManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Numerics;

namespace Datadog.Trace.RemoteConfigurationManagement
{
    internal interface IRemoteConfigurationManager : IDisposable
    {
        void SetCapability(BigInteger index, bool available);
    }
}
