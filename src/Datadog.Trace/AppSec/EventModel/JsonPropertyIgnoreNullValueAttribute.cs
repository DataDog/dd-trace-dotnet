// <copyright file="JsonPropertyIgnoreNullValueAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec.EventModel
{
    /// <summary>
    /// Attribute that allows us to declare JsonProperty attributes with
    /// NullValueHandling.Ignore, without running into the issue where the
    /// named argument is a custom type.
    /// </summary>
    internal class JsonPropertyIgnoreNullValueAttribute : JsonPropertyAttribute
    {
        internal JsonPropertyIgnoreNullValueAttribute(string propertyName)
            : base(propertyName)
        {
            NullValueHandling = NullValueHandling.Ignore;
        }
    }
}
