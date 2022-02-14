// <copyright file="IStringProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.ServiceModel;

namespace Datadog.Demos.WcfService.Library
{
    [ServiceContract(Namespace = "Datadog.Demos.WcfService")]
    public interface IStringProvider
    {
        [OperationContract]
        string GenerateRandomAsciiString(int length);

        [OperationContract]
        int ComputeStableHash(string str);

        [OperationContract]
        StringInfo GenerateRandomAsciiStringWithHash(int length);
    }
}
