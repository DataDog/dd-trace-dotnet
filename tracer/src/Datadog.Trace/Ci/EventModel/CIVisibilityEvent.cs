// <copyright file="CIVisibilityEvent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Ci.EventModel
{
    internal class CIVisibilityEvent<T> : IEvent
    {
        internal CIVisibilityEvent(string type, T content)
        {
            Type = type;
            Content = content;
        }

        public string Type { get; set; }

        public T Content { get; set; }
    }
}
