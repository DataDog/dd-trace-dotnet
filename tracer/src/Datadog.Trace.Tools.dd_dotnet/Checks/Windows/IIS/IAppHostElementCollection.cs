// <copyright file="IAppHostElementCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tools.dd_dotnet.Checks.Windows.IIS;

[NativeObject]
internal interface IAppHostElementCollection : IUnknown
{
    uint Count();

    IAppHostElement GetItem(Variant index);

    void AddElement(IAppHostElement element, int position);

    void DeleteElement(Variant index);

    void Clear();

    IAppHostElement CreateNewElement(string elementName);

    IAppHostCollectionSchema Schema();
}
