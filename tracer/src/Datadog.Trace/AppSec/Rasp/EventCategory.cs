// <copyright file="EventCategory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

#nullable enable

namespace Datadog.Trace.AppSec.Rasp
{
    internal readonly struct EventCategory
    {
         // "type": EVENT_TYPE(optional),
         // "language": (php|nodejs|java|dotnet|go|python|ruby|cpp|...) (optional),
         // "id": <string: UUID of the stack trace> (optional),
         // "message": <string: generic message> (optional),
         // "frames": [STACK_FRAME]

        private readonly string? _type;
        private readonly string? _language;
        private readonly string? _id;
        private readonly string? _message;
        private readonly List<StackFrame> _frames;

        public EventCategory(string? type, string? language, string? id, string? message, List<StackFrame> frames)
        {
            _type = type;
            _language = language;
            _id = id;
            _message = message;
            _frames = frames;
        }

        public string? Type => _type;

        public string? Language => _language;

        public string? Id => _id;

        public string? Message => _message;

        public List<StackFrame> Frames => _frames;
    }
}
