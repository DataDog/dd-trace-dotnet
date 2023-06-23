// <copyright file="JsonWriterContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using MongoDB.Bson.IO;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb
{
    internal class JsonWriterContext
    {
        // private fields
        private JsonWriterContext _parentContext;
        private ContextType _contextType;
        private string _indentation;
        private bool _hasElements = false;

        // constructors
        internal JsonWriterContext(JsonWriterContext parentContext, ContextType contextType, string indentChars)
        {
            _parentContext = parentContext;
            _contextType = contextType;
            _indentation = (parentContext == null) ? indentChars : parentContext.Indentation + indentChars;
        }

        // internal properties
        internal JsonWriterContext ParentContext
        {
            get { return _parentContext; }
        }

        internal ContextType ContextType
        {
            get { return _contextType; }
        }

        internal string Indentation
        {
            get { return _indentation; }
        }

        internal bool HasElements
        {
            get { return _hasElements; }
            set { _hasElements = value; }
        }
    }
}
