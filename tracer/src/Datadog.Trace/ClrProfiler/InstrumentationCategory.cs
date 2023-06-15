// <copyright file="InstrumentationCategory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler
{
    [System.Flags]
    internal enum InstrumentationCategory
    {
 // [DefinitionsId("FFAFA5168C4F4718B40CA8788875C2DA", "61BF627FA9B5477F85595A9F0D68B29C", "6410E14A2A2343BABBB45940190E1C3F")]
        Tracing = 1,
 // [DefinitionsId("8A0651DE92625A7EF3E2BBF32F0D2048", "02043D9EE45819725C08A53565EFDB14", "ED012C3038C94D4FBE65900C7C29DD16")]
        AppSec = 2,
 // [DefinitionsId("18ADCFBDCCF14141B3473C7D3B1E9494", "0C21F56FE62D49D78DF30D714F869815", "2A8E770131E64FCCB0AAC21AE3857E5E")]
        Iast = 4
    }
}
