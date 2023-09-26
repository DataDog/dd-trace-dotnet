// <copyright file="IAppHostPropertySchema.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tools.dd_dotnet.Checks.Windows.IIS;

[NativeObject]
internal interface IAppHostPropertySchema : IUnknown
{
    string Name();

    string Type();

    object DefaultValue();

    bool IsRequired();

    bool IsUniqueKey();

    bool IsCombinedKey();

    bool IsExpanded();

    string ValidationType();

    string ValidationParameter();

    Variant GetMetadata(string metadataType);

    bool IsCaseSensitive();

    IAppHostConstantValueCollection PossibleValues();

    bool DoesAllowInfinite();

    bool IsEncrypted();

    string TimeSpanFormat();
}
