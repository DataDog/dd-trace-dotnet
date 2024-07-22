// <copyright file="ExceptionReplayDiagnosticTagNames.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    /// <summary>
    /// Assigned as a tag on incoming spans by <see cref="ExceptionTrackManager"/> for diagnostics purposes.
    /// </summary>
    internal static class ExceptionReplayDiagnosticTagNames
    {
        public const string None = nameof(None);
        public const string Eligible = nameof(Eligible);
        public const string EmptyShadowStack = nameof(EmptyShadowStack);
        public const string ExceptionTrackManagerNotInitialized = nameof(ExceptionTrackManagerNotInitialized);
        public const string NotRootSpan = nameof(NotRootSpan);
        public const string ExceptionObjectIsNull = nameof(ExceptionObjectIsNull);
        public const string NonSupportedExceptionType = nameof(NonSupportedExceptionType);
        public const string CachedDoneExceptionCase = nameof(CachedDoneExceptionCase);
        public const string InvalidatedExceptionCase = nameof(InvalidatedExceptionCase);
        public const string CircuitBreakerIsOpen = nameof(CircuitBreakerIsOpen);
    }
}
