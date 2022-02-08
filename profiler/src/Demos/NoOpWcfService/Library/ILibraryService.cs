// <copyright file="ILibraryService.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.ServiceModel;

namespace Datadog.Demos.NoOpWcfService.Library
{
    [ServiceContract]
    public interface ILibraryService
    {
        [OperationContract]
        Book SearchBook(string bookName);
    }
}
