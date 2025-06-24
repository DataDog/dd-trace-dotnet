// <copyright file="NoCaptureReason.cs" company="Datadog">
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
    /// Assigned as a tag on incoming spans by <see cref="ExceptionTrackManager"/> to designate why certain errors were not captured
    /// </summary>
    internal static class NoCaptureReason
    {
        public const string OnlyThirdPartyCode = nameof(OnlyThirdPartyCode);
        public const string InstrumentationFailure = nameof(InstrumentationFailure);
        public const string NonSupportedExceptionType = nameof(NonSupportedExceptionType);
        public const string FirstOccurrence = nameof(FirstOccurrence);
        public const string GeneralError = nameof(GeneralError);
    }
}
