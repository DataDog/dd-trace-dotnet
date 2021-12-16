// <copyright file="IBsonElement.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb
{
    /// <summary>
    /// MongoDB.Bson.BsonDocument interface for duck-typing
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IBsonElement
    {
        /// <summary>
        /// Gets the name of the element.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the value of the element.
        /// </summary>
        public object Value { get; }
    }
}
