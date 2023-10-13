// <copyright file="IRegistryService.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tools.dd_dotnet.Checks
{
    internal interface IRegistryService
    {
        string[] GetLocalMachineValueNames(string key);

        string? GetLocalMachineValue(string key);

        string[] GetLocalMachineKeyNames(string key);

        string? GetLocalMachineKeyNameValue(string key, string subKeyName, string name);
    }
}
