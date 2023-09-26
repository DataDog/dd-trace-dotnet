// <copyright file="IAppHostConfigFile.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tools.dd_dotnet.Checks.Windows.IIS;

[NativeObject]
internal interface IAppHostConfigFile : IUnknown
{
    string ConfigPath();

    string FilePath();

    IAppHostConfigLocationCollection Locations();

    IAppHostElement GetAdminSection(string sectionName, string configPath);

    Variant GetMetadata(string metadataType);

    void SetMetadata(string metadataType, Variant value);

    void ClearInvalidSections();

    IAppHostSectionGroup RootSectionGroup();
}
