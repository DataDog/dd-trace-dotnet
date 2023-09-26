// <copyright file="IAppHostElement.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tools.dd_dotnet.Checks.Windows.IIS
{
    [NativeObject]
    internal interface IAppHostElement : IUnknown
    {
        string Name();

        IAppHostElementCollection Collection();

        IAppHostPropertyCollection Properties();

        IAppHostChildElementCollection ChildElements();

        Variant GetMetadata(string metadataType);

        void SetMetadata(string metadataType, Variant value);

        IAppHostElementSchema Schema();

        IAppHostElement GetElementByName(string subName);

        IAppHostProperty GetPropertyByName(string subName);

        void Clear();

        IAppHostMethodCollection Methods();
    }
}
