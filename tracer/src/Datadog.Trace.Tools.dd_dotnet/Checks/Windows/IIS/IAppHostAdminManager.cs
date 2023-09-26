// <copyright file="IAppHostAdminManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Tools.dd_dotnet.Checks.Windows.IIS;

[NativeObject]
internal interface IAppHostAdminManager : IUnknown
{
    IAppHostElement GetAdminSection(string sectionName, string path);

    IntPtr GetMetadata(string metadataType);

    void SetMetadata(string metadataType, Variant value);

    IAppHostConfigManager GetConfigManager();
}
