// <copyright file="ObscureDuckTypeVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;

namespace Datadog.Trace.DuckTyping.Tests.Properties.ValueType.ProxiesDefinitions
{
    public class ObscureDuckTypeVirtualClass
    {
        [Duck(FallbackToBaseTypes = true)]
        public virtual int PublicStaticGetValueType { get; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual int InternalStaticGetValueType { get; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual int ProtectedStaticGetValueType { get; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual int PrivateStaticGetValueType { get; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        public virtual int PublicStaticGetSetValueType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual int InternalStaticGetSetValueType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual int ProtectedStaticGetSetValueType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual int PrivateStaticGetSetValueType { get; set; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        public virtual int PublicGetValueType { get; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual int InternalGetValueType { get; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual int ProtectedGetValueType { get; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual int PrivateGetValueType { get; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        public virtual int PublicGetSetValueType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual int InternalGetSetValueType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual int ProtectedGetSetValueType { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual int PrivateGetSetValueType { get; set; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        public virtual int? PublicStaticNullableInt { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual int? PrivateStaticNullableInt { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual int? PublicNullableInt { get; set; }

        [Duck(FallbackToBaseTypes = true)]
        public virtual int? PrivateNullableInt { get; set; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        public virtual TaskStatus Status
        {
            get => default;
            set { }
        }

        // *

        [Duck(FallbackToBaseTypes = true, Name = "PublicGetSetValueType")]
        public virtual ValueWithType<int> PublicGetSetValueTypeWithType { get; set; }

        // *

        [Duck(FallbackToBaseTypes = true)]
        public virtual int this[int index]
        {
            get => default;
            set { }
        }
    }
}
