// <copyright file="BsonDocumentProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb
{
    /// <summary>
    /// MongoDB.Bson.BsonDocument interface for duck-typing
    /// </summary>
    internal class BsonDocumentProxy
    {
        /// <summary>
        /// Gets an element of this document.
        /// </summary>
        /// <param name="index">The zero based index of the element.</param>
        /// <returns>The element.</returns>
        public virtual BsonElementStruct GetElement(int index)
        {
            return default;
        }
    }
}
