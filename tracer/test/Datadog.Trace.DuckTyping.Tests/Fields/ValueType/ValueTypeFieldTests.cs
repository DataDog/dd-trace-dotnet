// <copyright file="ValueTypeFieldTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.DuckTyping.Tests.Fields.ValueType.ProxiesDefinitions;
using NUnit.Framework;

namespace Datadog.Trace.DuckTyping.Tests.Fields.ValueType
{
    public class ValueTypeFieldTests
    {
        public static IEnumerable<object[]> Data()
        {
            return new[]
            {
                new object[] { ObscureObject.GetFieldPublicObject() },
                new object[] { ObscureObject.GetFieldInternalObject() },
                new object[] { ObscureObject.GetFieldPrivateObject() },
            };
        }

        [TestCaseSource(nameof(Data))]
        public void StaticReadonlyFieldsSetException(object obscureObject)
        {
            Assert.Throws<DuckTypeFieldIsReadonlyException>(() =>
            {
                obscureObject.DuckCast<IObscureStaticReadonlyErrorDuckType>();
            });
        }

        [TestCaseSource(nameof(Data))]
        public void ReadonlyFieldsSetException(object obscureObject)
        {
            Assert.Throws<DuckTypeFieldIsReadonlyException>(() =>
            {
                obscureObject.DuckCast<IObscureReadonlyErrorDuckType>();
            });
        }

        [TestCaseSource(nameof(Data))]
        public void StaticReadonlyFields(object obscureObject)
        {
            var duckInterface = obscureObject.DuckCast<IObscureDuckType>();
            var duckAbstract = obscureObject.DuckCast<ObscureDuckTypeAbstractClass>();
            var duckVirtual = obscureObject.DuckCast<ObscureDuckTypeVirtualClass>();

            // *
            Assert.AreEqual(10, duckInterface.PublicStaticReadonlyValueTypeField);
            Assert.AreEqual(10, duckAbstract.PublicStaticReadonlyValueTypeField);
            Assert.AreEqual(10, duckVirtual.PublicStaticReadonlyValueTypeField);

            // *
            Assert.AreEqual(11, duckInterface.InternalStaticReadonlyValueTypeField);
            Assert.AreEqual(11, duckAbstract.InternalStaticReadonlyValueTypeField);
            Assert.AreEqual(11, duckVirtual.InternalStaticReadonlyValueTypeField);

            // *
            Assert.AreEqual(12, duckInterface.ProtectedStaticReadonlyValueTypeField);
            Assert.AreEqual(12, duckAbstract.ProtectedStaticReadonlyValueTypeField);
            Assert.AreEqual(12, duckVirtual.ProtectedStaticReadonlyValueTypeField);

            // *
            Assert.AreEqual(13, duckInterface.PrivateStaticReadonlyValueTypeField);
            Assert.AreEqual(13, duckAbstract.PrivateStaticReadonlyValueTypeField);
            Assert.AreEqual(13, duckVirtual.PrivateStaticReadonlyValueTypeField);
        }

        [TestCaseSource(nameof(Data))]
        public void StaticFields(object obscureObject)
        {
            var duckInterface = obscureObject.DuckCast<IObscureDuckType>();
            var duckAbstract = obscureObject.DuckCast<ObscureDuckTypeAbstractClass>();
            var duckVirtual = obscureObject.DuckCast<ObscureDuckTypeVirtualClass>();

            Assert.AreEqual(20, duckInterface.PublicStaticValueTypeField);
            Assert.AreEqual(20, duckAbstract.PublicStaticValueTypeField);
            Assert.AreEqual(20, duckVirtual.PublicStaticValueTypeField);

            duckInterface.PublicStaticValueTypeField = 42;
            Assert.AreEqual(42, duckInterface.PublicStaticValueTypeField);
            Assert.AreEqual(42, duckAbstract.PublicStaticValueTypeField);
            Assert.AreEqual(42, duckVirtual.PublicStaticValueTypeField);

            duckAbstract.PublicStaticValueTypeField = 50;
            Assert.AreEqual(50, duckInterface.PublicStaticValueTypeField);
            Assert.AreEqual(50, duckAbstract.PublicStaticValueTypeField);
            Assert.AreEqual(50, duckVirtual.PublicStaticValueTypeField);

            duckVirtual.PublicStaticValueTypeField = 60;
            Assert.AreEqual(60, duckInterface.PublicStaticValueTypeField);
            Assert.AreEqual(60, duckAbstract.PublicStaticValueTypeField);
            Assert.AreEqual(60, duckVirtual.PublicStaticValueTypeField);

            // *

            Assert.AreEqual(21, duckInterface.InternalStaticValueTypeField);
            Assert.AreEqual(21, duckAbstract.InternalStaticValueTypeField);
            Assert.AreEqual(21, duckVirtual.InternalStaticValueTypeField);

            duckInterface.InternalStaticValueTypeField = 42;
            Assert.AreEqual(42, duckInterface.InternalStaticValueTypeField);
            Assert.AreEqual(42, duckAbstract.InternalStaticValueTypeField);
            Assert.AreEqual(42, duckVirtual.InternalStaticValueTypeField);

            duckAbstract.InternalStaticValueTypeField = 50;
            Assert.AreEqual(50, duckInterface.InternalStaticValueTypeField);
            Assert.AreEqual(50, duckAbstract.InternalStaticValueTypeField);
            Assert.AreEqual(50, duckVirtual.InternalStaticValueTypeField);

            duckVirtual.InternalStaticValueTypeField = 60;
            Assert.AreEqual(60, duckInterface.InternalStaticValueTypeField);
            Assert.AreEqual(60, duckAbstract.InternalStaticValueTypeField);
            Assert.AreEqual(60, duckVirtual.InternalStaticValueTypeField);

            // *

            Assert.AreEqual(22, duckInterface.ProtectedStaticValueTypeField);
            Assert.AreEqual(22, duckAbstract.ProtectedStaticValueTypeField);
            Assert.AreEqual(22, duckVirtual.ProtectedStaticValueTypeField);

            duckInterface.ProtectedStaticValueTypeField = 42;
            Assert.AreEqual(42, duckInterface.ProtectedStaticValueTypeField);
            Assert.AreEqual(42, duckAbstract.ProtectedStaticValueTypeField);
            Assert.AreEqual(42, duckVirtual.ProtectedStaticValueTypeField);

            duckAbstract.ProtectedStaticValueTypeField = 50;
            Assert.AreEqual(50, duckInterface.ProtectedStaticValueTypeField);
            Assert.AreEqual(50, duckAbstract.ProtectedStaticValueTypeField);
            Assert.AreEqual(50, duckVirtual.ProtectedStaticValueTypeField);

            duckVirtual.ProtectedStaticValueTypeField = 60;
            Assert.AreEqual(60, duckInterface.ProtectedStaticValueTypeField);
            Assert.AreEqual(60, duckAbstract.ProtectedStaticValueTypeField);
            Assert.AreEqual(60, duckVirtual.ProtectedStaticValueTypeField);

            // *

            Assert.AreEqual(23, duckInterface.PrivateStaticValueTypeField);
            Assert.AreEqual(23, duckAbstract.PrivateStaticValueTypeField);
            Assert.AreEqual(23, duckVirtual.PrivateStaticValueTypeField);

            duckInterface.PrivateStaticValueTypeField = 42;
            Assert.AreEqual(42, duckInterface.PrivateStaticValueTypeField);
            Assert.AreEqual(42, duckAbstract.PrivateStaticValueTypeField);
            Assert.AreEqual(42, duckVirtual.PrivateStaticValueTypeField);

            duckAbstract.PrivateStaticValueTypeField = 50;
            Assert.AreEqual(50, duckInterface.PrivateStaticValueTypeField);
            Assert.AreEqual(50, duckAbstract.PrivateStaticValueTypeField);
            Assert.AreEqual(50, duckVirtual.PrivateStaticValueTypeField);

            duckVirtual.PrivateStaticValueTypeField = 60;
            Assert.AreEqual(60, duckInterface.PrivateStaticValueTypeField);
            Assert.AreEqual(60, duckAbstract.PrivateStaticValueTypeField);
            Assert.AreEqual(60, duckVirtual.PrivateStaticValueTypeField);
        }

        [TestCaseSource(nameof(Data))]
        public void ReadonlyFields(object obscureObject)
        {
            var duckInterface = obscureObject.DuckCast<IObscureDuckType>();
            var duckAbstract = obscureObject.DuckCast<ObscureDuckTypeAbstractClass>();
            var duckVirtual = obscureObject.DuckCast<ObscureDuckTypeVirtualClass>();

            // *
            Assert.AreEqual(30, duckInterface.PublicReadonlyValueTypeField);
            Assert.AreEqual(30, duckAbstract.PublicReadonlyValueTypeField);
            Assert.AreEqual(30, duckVirtual.PublicReadonlyValueTypeField);

            // *
            Assert.AreEqual(31, duckInterface.InternalReadonlyValueTypeField);
            Assert.AreEqual(31, duckAbstract.InternalReadonlyValueTypeField);
            Assert.AreEqual(31, duckVirtual.InternalReadonlyValueTypeField);

            // *
            Assert.AreEqual(32, duckInterface.ProtectedReadonlyValueTypeField);
            Assert.AreEqual(32, duckAbstract.ProtectedReadonlyValueTypeField);
            Assert.AreEqual(32, duckVirtual.ProtectedReadonlyValueTypeField);

            // *
            Assert.AreEqual(33, duckInterface.PrivateReadonlyValueTypeField);
            Assert.AreEqual(33, duckAbstract.PrivateReadonlyValueTypeField);
            Assert.AreEqual(33, duckVirtual.PrivateReadonlyValueTypeField);
        }

        [TestCaseSource(nameof(Data))]
        public void Fields(object obscureObject)
        {
            var duckInterface = obscureObject.DuckCast<IObscureDuckType>();
            var duckAbstract = obscureObject.DuckCast<ObscureDuckTypeAbstractClass>();
            var duckVirtual = obscureObject.DuckCast<ObscureDuckTypeVirtualClass>();

            Assert.AreEqual(40, duckInterface.PublicValueTypeField);
            Assert.AreEqual(40, duckAbstract.PublicValueTypeField);
            Assert.AreEqual(40, duckVirtual.PublicValueTypeField);

            duckInterface.PublicValueTypeField = 42;
            Assert.AreEqual(42, duckInterface.PublicValueTypeField);
            Assert.AreEqual(42, duckAbstract.PublicValueTypeField);
            Assert.AreEqual(42, duckVirtual.PublicValueTypeField);

            duckAbstract.PublicValueTypeField = 50;
            Assert.AreEqual(50, duckInterface.PublicValueTypeField);
            Assert.AreEqual(50, duckAbstract.PublicValueTypeField);
            Assert.AreEqual(50, duckVirtual.PublicValueTypeField);

            duckVirtual.PublicValueTypeField = 60;
            Assert.AreEqual(60, duckInterface.PublicValueTypeField);
            Assert.AreEqual(60, duckAbstract.PublicValueTypeField);
            Assert.AreEqual(60, duckVirtual.PublicValueTypeField);

            // *

            Assert.AreEqual(41, duckInterface.InternalValueTypeField);
            Assert.AreEqual(41, duckAbstract.InternalValueTypeField);
            Assert.AreEqual(41, duckVirtual.InternalValueTypeField);

            duckInterface.InternalValueTypeField = 42;
            Assert.AreEqual(42, duckInterface.InternalValueTypeField);
            Assert.AreEqual(42, duckAbstract.InternalValueTypeField);
            Assert.AreEqual(42, duckVirtual.InternalValueTypeField);

            duckAbstract.InternalValueTypeField = 50;
            Assert.AreEqual(50, duckInterface.InternalValueTypeField);
            Assert.AreEqual(50, duckAbstract.InternalValueTypeField);
            Assert.AreEqual(50, duckVirtual.InternalValueTypeField);

            duckVirtual.InternalValueTypeField = 60;
            Assert.AreEqual(60, duckInterface.InternalValueTypeField);
            Assert.AreEqual(60, duckAbstract.InternalValueTypeField);
            Assert.AreEqual(60, duckVirtual.InternalValueTypeField);

            // *

            Assert.AreEqual(42, duckInterface.ProtectedValueTypeField);
            Assert.AreEqual(42, duckAbstract.ProtectedValueTypeField);
            Assert.AreEqual(42, duckVirtual.ProtectedValueTypeField);

            duckInterface.ProtectedValueTypeField = 45;
            Assert.AreEqual(45, duckInterface.ProtectedValueTypeField);
            Assert.AreEqual(45, duckAbstract.ProtectedValueTypeField);
            Assert.AreEqual(45, duckVirtual.ProtectedValueTypeField);

            duckAbstract.ProtectedValueTypeField = 50;
            Assert.AreEqual(50, duckInterface.ProtectedValueTypeField);
            Assert.AreEqual(50, duckAbstract.ProtectedValueTypeField);
            Assert.AreEqual(50, duckVirtual.ProtectedValueTypeField);

            duckVirtual.ProtectedValueTypeField = 60;
            Assert.AreEqual(60, duckInterface.ProtectedValueTypeField);
            Assert.AreEqual(60, duckAbstract.ProtectedValueTypeField);
            Assert.AreEqual(60, duckVirtual.ProtectedValueTypeField);

            // *

            Assert.AreEqual(43, duckInterface.PrivateValueTypeField);
            Assert.AreEqual(43, duckAbstract.PrivateValueTypeField);
            Assert.AreEqual(43, duckVirtual.PrivateValueTypeField);

            duckInterface.PrivateValueTypeField = 42;
            Assert.AreEqual(42, duckInterface.PrivateValueTypeField);
            Assert.AreEqual(42, duckAbstract.PrivateValueTypeField);
            Assert.AreEqual(42, duckVirtual.PrivateValueTypeField);

            duckAbstract.PrivateValueTypeField = 50;
            Assert.AreEqual(50, duckInterface.PrivateValueTypeField);
            Assert.AreEqual(50, duckAbstract.PrivateValueTypeField);
            Assert.AreEqual(50, duckVirtual.PrivateValueTypeField);

            duckVirtual.PrivateValueTypeField = 60;
            Assert.AreEqual(60, duckInterface.PrivateValueTypeField);
            Assert.AreEqual(60, duckAbstract.PrivateValueTypeField);
            Assert.AreEqual(60, duckVirtual.PrivateValueTypeField);
        }

        [TestCaseSource(nameof(Data))]
        public void NullableOfKnown(object obscureObject)
        {
            var duckInterface = obscureObject.DuckCast<IObscureDuckType>();
            var duckAbstract = obscureObject.DuckCast<ObscureDuckTypeAbstractClass>();
            var duckVirtual = obscureObject.DuckCast<ObscureDuckTypeVirtualClass>();

            Assert.Null(duckInterface.PublicStaticNullableIntField);
            Assert.Null(duckAbstract.PublicStaticNullableIntField);
            Assert.Null(duckVirtual.PublicStaticNullableIntField);

            duckInterface.PublicStaticNullableIntField = 42;
            Assert.AreEqual(42, duckInterface.PublicStaticNullableIntField);
            Assert.AreEqual(42, duckAbstract.PublicStaticNullableIntField);
            Assert.AreEqual(42, duckVirtual.PublicStaticNullableIntField);

            duckAbstract.PublicStaticNullableIntField = 50;
            Assert.AreEqual(50, duckInterface.PublicStaticNullableIntField);
            Assert.AreEqual(50, duckAbstract.PublicStaticNullableIntField);
            Assert.AreEqual(50, duckVirtual.PublicStaticNullableIntField);

            duckVirtual.PublicStaticNullableIntField = null;
            Assert.Null(duckInterface.PublicStaticNullableIntField);
            Assert.Null(duckAbstract.PublicStaticNullableIntField);
            Assert.Null(duckVirtual.PublicStaticNullableIntField);

            // *

            Assert.Null(duckInterface.PrivateStaticNullableIntField);
            Assert.Null(duckAbstract.PrivateStaticNullableIntField);
            Assert.Null(duckVirtual.PrivateStaticNullableIntField);

            duckInterface.PrivateStaticNullableIntField = 42;
            Assert.AreEqual(42, duckInterface.PrivateStaticNullableIntField);
            Assert.AreEqual(42, duckAbstract.PrivateStaticNullableIntField);
            Assert.AreEqual(42, duckVirtual.PrivateStaticNullableIntField);

            duckAbstract.PrivateStaticNullableIntField = 50;
            Assert.AreEqual(50, duckInterface.PrivateStaticNullableIntField);
            Assert.AreEqual(50, duckAbstract.PrivateStaticNullableIntField);
            Assert.AreEqual(50, duckVirtual.PrivateStaticNullableIntField);

            duckVirtual.PrivateStaticNullableIntField = null;
            Assert.Null(duckInterface.PrivateStaticNullableIntField);
            Assert.Null(duckAbstract.PrivateStaticNullableIntField);
            Assert.Null(duckVirtual.PrivateStaticNullableIntField);

            // *

            Assert.Null(duckInterface.PublicNullableIntField);
            Assert.Null(duckAbstract.PublicNullableIntField);
            Assert.Null(duckVirtual.PublicNullableIntField);

            duckInterface.PublicNullableIntField = 42;
            Assert.AreEqual(42, duckInterface.PublicNullableIntField);
            Assert.AreEqual(42, duckAbstract.PublicNullableIntField);
            Assert.AreEqual(42, duckVirtual.PublicNullableIntField);

            duckAbstract.PublicNullableIntField = 50;
            Assert.AreEqual(50, duckInterface.PublicNullableIntField);
            Assert.AreEqual(50, duckAbstract.PublicNullableIntField);
            Assert.AreEqual(50, duckVirtual.PublicNullableIntField);

            duckVirtual.PublicNullableIntField = null;
            Assert.Null(duckInterface.PublicNullableIntField);
            Assert.Null(duckAbstract.PublicNullableIntField);
            Assert.Null(duckVirtual.PublicNullableIntField);

            // *

            Assert.Null(duckInterface.PrivateNullableIntField);
            Assert.Null(duckAbstract.PrivateNullableIntField);
            Assert.Null(duckVirtual.PrivateNullableIntField);

            duckInterface.PrivateNullableIntField = 42;
            Assert.AreEqual(42, duckInterface.PrivateNullableIntField);
            Assert.AreEqual(42, duckAbstract.PrivateNullableIntField);
            Assert.AreEqual(42, duckVirtual.PrivateNullableIntField);

            duckAbstract.PrivateNullableIntField = 50;
            Assert.AreEqual(50, duckInterface.PrivateNullableIntField);
            Assert.AreEqual(50, duckAbstract.PrivateNullableIntField);
            Assert.AreEqual(50, duckVirtual.PrivateNullableIntField);

            duckVirtual.PrivateNullableIntField = null;
            Assert.Null(duckInterface.PrivateNullableIntField);
            Assert.Null(duckAbstract.PrivateNullableIntField);
            Assert.Null(duckVirtual.PrivateNullableIntField);
        }
    }
}
