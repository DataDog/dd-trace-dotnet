// <copyright file="IAppHostProperty.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tools.dd_dotnet.Checks.Windows.IIS;

[NativeObject]
internal interface IAppHostProperty : IUnknown
{
    string Name();

    Variant GetValue();

    void SetValue(Variant value);

    void Clear();

    string StringValue();

    IAppHostPropertyException Exception();

    Variant GetMetadata(string metadataType);

    void SetMetadata(string metadataType, Variant value);

    IAppHostPropertySchema Schema();
}
