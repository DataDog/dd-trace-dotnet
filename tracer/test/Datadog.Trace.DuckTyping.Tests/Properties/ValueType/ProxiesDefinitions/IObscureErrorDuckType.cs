// <copyright file="IObscureErrorDuckType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Properties.ValueType.ProxiesDefinitions
{
    public interface IObscureErrorDuckType
    {
        int PublicGetValueType { get; set; }
    }
}
