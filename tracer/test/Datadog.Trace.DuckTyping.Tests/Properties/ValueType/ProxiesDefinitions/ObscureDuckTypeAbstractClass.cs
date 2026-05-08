// <copyright file="ObscureDuckTypeAbstractClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;

namespace Datadog.Trace.DuckTyping.Tests.Properties.ValueType.ProxiesDefinitions
{
    public abstract class ObscureDuckTypeAbstractClass
    {
        [Duck(FallbackToBaseTypes = true)]
        public abstract int PublicStaticGetValueType { get; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract int InternalStaticGetValueType { get; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract int ProtectedStaticGetValueType { get; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract int PrivateStaticGetValueType { get; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        public abstract int PublicStaticGetSetValueType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract int InternalStaticGetSetValueType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract int ProtectedStaticGetSetValueType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract int PrivateStaticGetSetValueType { get; set; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        public abstract int PublicGetValueType { get; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract int InternalGetValueType { get; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract int ProtectedGetValueType { get; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract int PrivateGetValueType { get; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        public abstract int PublicGetSetValueType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract int InternalGetSetValueType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract int ProtectedGetSetValueType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract int PrivateGetSetValueType { get; set; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        public abstract int? PublicStaticNullableInt { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract int? PrivateStaticNullableInt { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract int? PublicNullableInt { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public abstract int? PrivateNullableInt { get; set; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        public abstract TaskStatus Status { get; set; }

        // *

        [Duck(FallbackToBaseTypes = true, Name = "PublicGetSetValueType")]
        public abstract ValueWithType<int> PublicGetSetValueTypeWithType { get; set; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        public abstract int this[int index] { get; set; }
    }
}
