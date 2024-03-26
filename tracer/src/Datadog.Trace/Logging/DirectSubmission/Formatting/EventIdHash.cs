// <copyright file="EventIdHash.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// based on https://github.com/serilog/serilog-formatting-compact/blob/8393e0ab8c2bc746fc733a4f20731b9e1f20f811/src/Serilog.Formatting.Compact/Formatting/Compact/EventIdHash.cs
// Copyright 2016 Serilog Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
#nullable enable

using System;
using Datadog.Trace.Util;

namespace Datadog.Trace.Logging.DirectSubmission.Formatting
{
    /// <summary>
    /// Hash functions for message templates. See <see cref="Compute"/>.
    /// </summary>
    internal static class EventIdHash
    {
        /// <summary>
        /// Compute a 32-bit hash of the provided <paramref name="messageTemplate"/>.
        /// and optionally a <paramref name="stackTrace"/>. The
        /// resulting hash value can be uses as an event id in lieu of transmitting the
        /// full template string.
        /// </summary>
        /// <param name="messageTemplate">A message template.</param>
        /// <param name="stackTrace">An optional stack trace to consider in the calculation.</param>
        /// <returns>A 32-bit hash of the template.</returns>
        public static uint Compute(string messageTemplate, string? stackTrace = null)
        {
            if (messageTemplate == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(messageTemplate));
            }

            // Jenkins one-at-a-time https://en.wikipedia.org/wiki/Jenkins_hash_function
            unchecked
            {
                uint hash = 0;
                for (var i = 0; i < messageTemplate.Length; ++i)
                {
                    hash += messageTemplate[i];
                    hash += (hash << 10);
                    hash ^= (hash >> 6);
                }

                if (stackTrace is not null)
                {
                    for (var i = 0; i < stackTrace.Length; ++i)
                    {
                        hash += stackTrace[i];
                        hash += (hash << 10);
                        hash ^= (hash >> 6);
                    }
                }

                hash += (hash << 3);
                hash ^= (hash >> 11);
                hash += (hash << 15);
                return hash;
            }
        }
    }
}
