// <copyright file="Book.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Runtime.Serialization;

namespace Datadog.Demos.NoOpWcfService.Library
{
    [DataContract]
    public class Book
    {
        [DataMember]
        public int ID { get; set; }

        [DataMember]
        public string Name { get; set; }
    }
}
