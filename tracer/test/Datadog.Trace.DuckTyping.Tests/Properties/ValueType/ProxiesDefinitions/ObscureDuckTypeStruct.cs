// <copyright file="ObscureDuckTypeStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Properties.ValueType.ProxiesDefinitions
{
#pragma warning disable 649

    [DuckCopy]
    internal struct ObscureDuckTypeStruct
    {
        [Duck(FallbackToBaseTypes = true)]
        public int PublicStaticGetValueType;

        [Duck(FallbackToBaseTypes = true)]
        public int InternalStaticGetValueType;

        [Duck(FallbackToBaseTypes = true)]
        public int ProtectedStaticGetValueType;

        [Duck(FallbackToBaseTypes = true)]
        public int PrivateStaticGetValueType;

        [Duck(FallbackToBaseTypes = true)]
        public int PublicStaticGetSetValueType;

        [Duck(FallbackToBaseTypes = true)]
        public int InternalStaticGetSetValueType;

        [Duck(FallbackToBaseTypes = true)]
        public int ProtectedStaticGetSetValueType;

        [Duck(FallbackToBaseTypes = true)]
        public int PrivateStaticGetSetValueType;

        [Duck(FallbackToBaseTypes = true)]
        public int PublicGetValueType;

        [Duck(FallbackToBaseTypes = true)]
        public int InternalGetValueType;

        [Duck(FallbackToBaseTypes = true)]
        public int ProtectedGetValueType;

        [Duck(FallbackToBaseTypes = true)]
        public int PrivateGetValueType;

        [Duck(FallbackToBaseTypes = true)]
        public int PublicGetSetValueType;

        [Duck(FallbackToBaseTypes = true)]
        public int InternalGetSetValueType;

        [Duck(FallbackToBaseTypes = true)]
        public int ProtectedGetSetValueType;

        [Duck(FallbackToBaseTypes = true)]
        public int PrivateGetSetValueType;

        [Duck(FallbackToBaseTypes = true, Name = "PublicGetSetValueType")]
        public ValueWithType<int> PublicGetSetValueTypeWithType;
    }
}
