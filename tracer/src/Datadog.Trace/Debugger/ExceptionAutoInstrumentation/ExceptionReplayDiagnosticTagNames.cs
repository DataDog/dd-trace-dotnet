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
        public const string Eligible = nameof(Eligible);
        public const string NotEligible = nameof(NotEligible);
        public const string ExceptionTrackManagerNotInitialized = nameof(ExceptionTrackManagerNotInitialized);
        public const string NotRootSpan = nameof(NotRootSpan);
        public const string ExceptionObjectIsNull = nameof(ExceptionObjectIsNull);
        public const string NonSupportedExceptionType = nameof(NonSupportedExceptionType);
        public const string CachedDoneExceptionCase = nameof(CachedDoneExceptionCase);
        public const string CachedInvalidatedExceptionCase = nameof(CachedInvalidatedExceptionCase);
        public const string InvalidatedExceptionCase = nameof(InvalidatedExceptionCase);
        public const string CircuitBreakerIsOpen = nameof(CircuitBreakerIsOpen);
        public const string NonCachedDoneExceptionCase = nameof(NonCachedDoneExceptionCase);
        public const string NoCustomerFrames = nameof(NoCustomerFrames);
        public const string NoFramesToInstrument = nameof(NoFramesToInstrument);
        public const string EmptyCallStackTreeWhileCollecting = nameof(EmptyCallStackTreeWhileCollecting);
        public const string InvalidatedCase = nameof(InvalidatedCase);
        public const string NewCase = nameof(NewCase);
    }
}
