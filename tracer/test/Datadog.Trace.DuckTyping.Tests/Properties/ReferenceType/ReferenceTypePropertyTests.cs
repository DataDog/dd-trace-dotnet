// <copyright file="ReferenceTypePropertyTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.DuckTyping.Tests.Properties.ReferenceType.ProxiesDefinitions;
using NUnit.Framework;

namespace Datadog.Trace.DuckTyping.Tests.Properties.ReferenceType
{
    public class ReferenceTypePropertyTests
    {
        public static IEnumerable<object[]> Data()
        {
            return new[]
            {
                new object[] { ObscureObject.GetPropertyPublicObject() },
                new object[] { ObscureObject.GetPropertyInternalObject() },
                new object[] { ObscureObject.GetPropertyPrivateObject() },
            };
        }

        [TestCaseSource(nameof(Data))]
        public void StaticGetOnlyPropertyException(object obscureObject)
        {
            Assert.Throws<DuckTypePropertyCantBeWrittenException>(() =>
            {
                obscureObject.DuckCast<IObscureStaticErrorDuckType>();
            });
        }

        [TestCaseSource(nameof(Data))]
        public void GetOnlyPropertyException(object obscureObject)
        {
            Assert.Throws<DuckTypePropertyCantBeWrittenException>(() =>
            {
                obscureObject.DuckCast<IObscureErrorDuckType>();
            });
        }

        [TestCaseSource(nameof(Data))]
        public void StaticGetOnlyProperties(object obscureObject)
        {
            var duckInterface = obscureObject.DuckCast<IObscureDuckType>();
            var duckAbstract = obscureObject.DuckCast<ObscureDuckTypeAbstractClass>();
            var duckVirtual = obscureObject.DuckCast<ObscureDuckTypeVirtualClass>();

            // *
            Assert.AreEqual("10", duckInterface.PublicStaticGetReferenceType);
            Assert.AreEqual("10", duckAbstract.PublicStaticGetReferenceType);
            Assert.AreEqual("10", duckVirtual.PublicStaticGetReferenceType);

            // *
            Assert.AreEqual("11", duckInterface.InternalStaticGetReferenceType);
            Assert.AreEqual("11", duckAbstract.InternalStaticGetReferenceType);
            Assert.AreEqual("11", duckVirtual.InternalStaticGetReferenceType);

            // *
            Assert.AreEqual("12", duckInterface.ProtectedStaticGetReferenceType);
            Assert.AreEqual("12", duckAbstract.ProtectedStaticGetReferenceType);
            Assert.AreEqual("12", duckVirtual.ProtectedStaticGetReferenceType);

            // *
            Assert.AreEqual("13", duckInterface.PrivateStaticGetReferenceType);
            Assert.AreEqual("13", duckAbstract.PrivateStaticGetReferenceType);
            Assert.AreEqual("13", duckVirtual.PrivateStaticGetReferenceType);
        }

        [TestCaseSource(nameof(Data))]
        public void StaticProperties(object obscureObject)
        {
            var duckInterface = obscureObject.DuckCast<IObscureDuckType>();
            var duckAbstract = obscureObject.DuckCast<ObscureDuckTypeAbstractClass>();
            var duckVirtual = obscureObject.DuckCast<ObscureDuckTypeVirtualClass>();

            Assert.AreEqual("20", duckInterface.PublicStaticGetSetReferenceType);
            Assert.AreEqual("20", duckAbstract.PublicStaticGetSetReferenceType);
            Assert.AreEqual("20", duckVirtual.PublicStaticGetSetReferenceType);

            duckInterface.PublicStaticGetSetReferenceType = "42";
            Assert.AreEqual("42", duckInterface.PublicStaticGetSetReferenceType);
            Assert.AreEqual("42", duckAbstract.PublicStaticGetSetReferenceType);
            Assert.AreEqual("42", duckVirtual.PublicStaticGetSetReferenceType);

            duckAbstract.PublicStaticGetSetReferenceType = "50";
            Assert.AreEqual("50", duckInterface.PublicStaticGetSetReferenceType);
            Assert.AreEqual("50", duckAbstract.PublicStaticGetSetReferenceType);
            Assert.AreEqual("50", duckVirtual.PublicStaticGetSetReferenceType);

            duckVirtual.PublicStaticGetSetReferenceType = "60";
            Assert.AreEqual("60", duckInterface.PublicStaticGetSetReferenceType);
            Assert.AreEqual("60", duckAbstract.PublicStaticGetSetReferenceType);
            Assert.AreEqual("60", duckVirtual.PublicStaticGetSetReferenceType);

            duckInterface.PublicStaticGetSetReferenceType = "20";

            // *

            Assert.AreEqual("21", duckInterface.InternalStaticGetSetReferenceType);
            Assert.AreEqual("21", duckAbstract.InternalStaticGetSetReferenceType);
            Assert.AreEqual("21", duckVirtual.InternalStaticGetSetReferenceType);

            duckInterface.InternalStaticGetSetReferenceType = "42";
            Assert.AreEqual("42", duckInterface.InternalStaticGetSetReferenceType);
            Assert.AreEqual("42", duckAbstract.InternalStaticGetSetReferenceType);
            Assert.AreEqual("42", duckVirtual.InternalStaticGetSetReferenceType);

            duckAbstract.InternalStaticGetSetReferenceType = "50";
            Assert.AreEqual("50", duckInterface.InternalStaticGetSetReferenceType);
            Assert.AreEqual("50", duckAbstract.InternalStaticGetSetReferenceType);
            Assert.AreEqual("50", duckVirtual.InternalStaticGetSetReferenceType);

            duckVirtual.InternalStaticGetSetReferenceType = "60";
            Assert.AreEqual("60", duckInterface.InternalStaticGetSetReferenceType);
            Assert.AreEqual("60", duckAbstract.InternalStaticGetSetReferenceType);
            Assert.AreEqual("60", duckVirtual.InternalStaticGetSetReferenceType);

            duckInterface.InternalStaticGetSetReferenceType = "21";

            // *

            Assert.AreEqual("22", duckInterface.ProtectedStaticGetSetReferenceType);
            Assert.AreEqual("22", duckAbstract.ProtectedStaticGetSetReferenceType);
            Assert.AreEqual("22", duckVirtual.ProtectedStaticGetSetReferenceType);

            duckInterface.ProtectedStaticGetSetReferenceType = "42";
            Assert.AreEqual("42", duckInterface.ProtectedStaticGetSetReferenceType);
            Assert.AreEqual("42", duckAbstract.ProtectedStaticGetSetReferenceType);
            Assert.AreEqual("42", duckVirtual.ProtectedStaticGetSetReferenceType);

            duckAbstract.ProtectedStaticGetSetReferenceType = "50";
            Assert.AreEqual("50", duckInterface.ProtectedStaticGetSetReferenceType);
            Assert.AreEqual("50", duckAbstract.ProtectedStaticGetSetReferenceType);
            Assert.AreEqual("50", duckVirtual.ProtectedStaticGetSetReferenceType);

            duckVirtual.ProtectedStaticGetSetReferenceType = "60";
            Assert.AreEqual("60", duckInterface.ProtectedStaticGetSetReferenceType);
            Assert.AreEqual("60", duckAbstract.ProtectedStaticGetSetReferenceType);
            Assert.AreEqual("60", duckVirtual.ProtectedStaticGetSetReferenceType);

            duckInterface.ProtectedStaticGetSetReferenceType = "22";

            // *

            Assert.AreEqual("23", duckInterface.PrivateStaticGetSetReferenceType);
            Assert.AreEqual("23", duckAbstract.PrivateStaticGetSetReferenceType);
            Assert.AreEqual("23", duckVirtual.PrivateStaticGetSetReferenceType);

            duckInterface.PrivateStaticGetSetReferenceType = "42";
            Assert.AreEqual("42", duckInterface.PrivateStaticGetSetReferenceType);
            Assert.AreEqual("42", duckAbstract.PrivateStaticGetSetReferenceType);
            Assert.AreEqual("42", duckVirtual.PrivateStaticGetSetReferenceType);

            duckAbstract.PrivateStaticGetSetReferenceType = "50";
            Assert.AreEqual("50", duckInterface.PrivateStaticGetSetReferenceType);
            Assert.AreEqual("50", duckAbstract.PrivateStaticGetSetReferenceType);
            Assert.AreEqual("50", duckVirtual.PrivateStaticGetSetReferenceType);

            duckVirtual.PrivateStaticGetSetReferenceType = "60";
            Assert.AreEqual("60", duckInterface.PrivateStaticGetSetReferenceType);
            Assert.AreEqual("60", duckAbstract.PrivateStaticGetSetReferenceType);
            Assert.AreEqual("60", duckVirtual.PrivateStaticGetSetReferenceType);

            duckInterface.PrivateStaticGetSetReferenceType = "23";
        }

        [TestCaseSource(nameof(Data))]
        public void GetOnlyProperties(object obscureObject)
        {
            var duckInterface = obscureObject.DuckCast<IObscureDuckType>();
            var duckAbstract = obscureObject.DuckCast<ObscureDuckTypeAbstractClass>();
            var duckVirtual = obscureObject.DuckCast<ObscureDuckTypeVirtualClass>();

            // *
            Assert.AreEqual("30", duckInterface.PublicGetReferenceType);
            Assert.AreEqual("30", duckAbstract.PublicGetReferenceType);
            Assert.AreEqual("30", duckVirtual.PublicGetReferenceType);

            // *
            Assert.AreEqual("31", duckInterface.InternalGetReferenceType);
            Assert.AreEqual("31", duckAbstract.InternalGetReferenceType);
            Assert.AreEqual("31", duckVirtual.InternalGetReferenceType);

            // *
            Assert.AreEqual("32", duckInterface.ProtectedGetReferenceType);
            Assert.AreEqual("32", duckAbstract.ProtectedGetReferenceType);
            Assert.AreEqual("32", duckVirtual.ProtectedGetReferenceType);

            // *
            Assert.AreEqual("33", duckInterface.PrivateGetReferenceType);
            Assert.AreEqual("33", duckAbstract.PrivateGetReferenceType);
            Assert.AreEqual("33", duckVirtual.PrivateGetReferenceType);
        }

        [TestCaseSource(nameof(Data))]
        public void Properties(object obscureObject)
        {
            var duckInterface = obscureObject.DuckCast<IObscureDuckType>();
            var duckAbstract = obscureObject.DuckCast<ObscureDuckTypeAbstractClass>();
            var duckVirtual = obscureObject.DuckCast<ObscureDuckTypeVirtualClass>();

            Assert.AreEqual("40", duckInterface.PublicGetSetReferenceType);
            Assert.AreEqual("40", duckAbstract.PublicGetSetReferenceType);
            Assert.AreEqual("40", duckVirtual.PublicGetSetReferenceType);

            duckInterface.PublicGetSetReferenceType = "42";
            Assert.AreEqual("42", duckInterface.PublicGetSetReferenceType);
            Assert.AreEqual("42", duckAbstract.PublicGetSetReferenceType);
            Assert.AreEqual("42", duckVirtual.PublicGetSetReferenceType);

            duckAbstract.PublicGetSetReferenceType = "50";
            Assert.AreEqual("50", duckInterface.PublicGetSetReferenceType);
            Assert.AreEqual("50", duckAbstract.PublicGetSetReferenceType);
            Assert.AreEqual("50", duckVirtual.PublicGetSetReferenceType);

            duckVirtual.PublicGetSetReferenceType = "60";
            Assert.AreEqual("60", duckInterface.PublicGetSetReferenceType);
            Assert.AreEqual("60", duckAbstract.PublicGetSetReferenceType);
            Assert.AreEqual("60", duckVirtual.PublicGetSetReferenceType);

            duckInterface.PublicGetSetReferenceType = "40";

            // *

            Assert.AreEqual("41", duckInterface.InternalGetSetReferenceType);
            Assert.AreEqual("41", duckAbstract.InternalGetSetReferenceType);
            Assert.AreEqual("41", duckVirtual.InternalGetSetReferenceType);

            duckInterface.InternalGetSetReferenceType = "42";
            Assert.AreEqual("42", duckInterface.InternalGetSetReferenceType);
            Assert.AreEqual("42", duckAbstract.InternalGetSetReferenceType);
            Assert.AreEqual("42", duckVirtual.InternalGetSetReferenceType);

            duckAbstract.InternalGetSetReferenceType = "50";
            Assert.AreEqual("50", duckInterface.InternalGetSetReferenceType);
            Assert.AreEqual("50", duckAbstract.InternalGetSetReferenceType);
            Assert.AreEqual("50", duckVirtual.InternalGetSetReferenceType);

            duckVirtual.InternalGetSetReferenceType = "60";
            Assert.AreEqual("60", duckInterface.InternalGetSetReferenceType);
            Assert.AreEqual("60", duckAbstract.InternalGetSetReferenceType);
            Assert.AreEqual("60", duckVirtual.InternalGetSetReferenceType);

            duckInterface.InternalGetSetReferenceType = "41";

            // *

            Assert.AreEqual("42", duckInterface.ProtectedGetSetReferenceType);
            Assert.AreEqual("42", duckAbstract.ProtectedGetSetReferenceType);
            Assert.AreEqual("42", duckVirtual.ProtectedGetSetReferenceType);

            duckInterface.ProtectedGetSetReferenceType = "45";
            Assert.AreEqual("45", duckInterface.ProtectedGetSetReferenceType);
            Assert.AreEqual("45", duckAbstract.ProtectedGetSetReferenceType);
            Assert.AreEqual("45", duckVirtual.ProtectedGetSetReferenceType);

            duckAbstract.ProtectedGetSetReferenceType = "50";
            Assert.AreEqual("50", duckInterface.ProtectedGetSetReferenceType);
            Assert.AreEqual("50", duckAbstract.ProtectedGetSetReferenceType);
            Assert.AreEqual("50", duckVirtual.ProtectedGetSetReferenceType);

            duckVirtual.ProtectedGetSetReferenceType = "60";
            Assert.AreEqual("60", duckInterface.ProtectedGetSetReferenceType);
            Assert.AreEqual("60", duckAbstract.ProtectedGetSetReferenceType);
            Assert.AreEqual("60", duckVirtual.ProtectedGetSetReferenceType);

            duckInterface.ProtectedGetSetReferenceType = "42";

            // *

            Assert.AreEqual("43", duckInterface.PrivateGetSetReferenceType);
            Assert.AreEqual("43", duckAbstract.PrivateGetSetReferenceType);
            Assert.AreEqual("43", duckVirtual.PrivateGetSetReferenceType);

            duckInterface.PrivateGetSetReferenceType = "42";
            Assert.AreEqual("42", duckInterface.PrivateGetSetReferenceType);
            Assert.AreEqual("42", duckAbstract.PrivateGetSetReferenceType);
            Assert.AreEqual("42", duckVirtual.PrivateGetSetReferenceType);

            duckAbstract.PrivateGetSetReferenceType = "50";
            Assert.AreEqual("50", duckInterface.PrivateGetSetReferenceType);
            Assert.AreEqual("50", duckAbstract.PrivateGetSetReferenceType);
            Assert.AreEqual("50", duckVirtual.PrivateGetSetReferenceType);

            duckVirtual.PrivateGetSetReferenceType = "60";
            Assert.AreEqual("60", duckInterface.PrivateGetSetReferenceType);
            Assert.AreEqual("60", duckAbstract.PrivateGetSetReferenceType);
            Assert.AreEqual("60", duckVirtual.PrivateGetSetReferenceType);

            duckInterface.PrivateGetSetReferenceType = "43";
        }

        [TestCaseSource(nameof(Data))]
        public void Indexer(object obscureObject)
        {
            var duckInterface = obscureObject.DuckCast<IObscureDuckType>();
            var duckAbstract = obscureObject.DuckCast<ObscureDuckTypeAbstractClass>();
            var duckVirtual = obscureObject.DuckCast<ObscureDuckTypeVirtualClass>();

            duckInterface["1"] = "100";
            Assert.AreEqual("100", duckInterface["1"]);
            Assert.AreEqual("100", duckAbstract["1"]);
            Assert.AreEqual("100", duckVirtual["1"]);

            duckAbstract["2"] = "200";
            Assert.AreEqual("200", duckInterface["2"]);
            Assert.AreEqual("200", duckAbstract["2"]);
            Assert.AreEqual("200", duckVirtual["2"]);

            duckVirtual["3"] = "300";
            Assert.AreEqual("300", duckInterface["3"]);
            Assert.AreEqual("300", duckAbstract["3"]);
            Assert.AreEqual("300", duckVirtual["3"]);
        }

        [TestCaseSource(nameof(Data))]
        public void StructCopy(object obscureObject)
        {
            var duckStructCopy = obscureObject.DuckCast<ObscureDuckTypeStruct>();

            Assert.AreEqual("10", duckStructCopy.PublicStaticGetReferenceType);
            Assert.AreEqual("11", duckStructCopy.InternalStaticGetReferenceType);
            Assert.AreEqual("12", duckStructCopy.ProtectedStaticGetReferenceType);
            Assert.AreEqual("13", duckStructCopy.PrivateStaticGetReferenceType);

            Assert.AreEqual("20", duckStructCopy.PublicStaticGetSetReferenceType);
            Assert.AreEqual("21", duckStructCopy.InternalStaticGetSetReferenceType);
            Assert.AreEqual("22", duckStructCopy.ProtectedStaticGetSetReferenceType);
            Assert.AreEqual("23", duckStructCopy.PrivateStaticGetSetReferenceType);

            Assert.AreEqual("30", duckStructCopy.PublicGetReferenceType);
            Assert.AreEqual("31", duckStructCopy.InternalGetReferenceType);
            Assert.AreEqual("32", duckStructCopy.ProtectedGetReferenceType);
            Assert.AreEqual("33", duckStructCopy.PrivateGetReferenceType);

            Assert.AreEqual("40", duckStructCopy.PublicGetSetReferenceType);
            Assert.AreEqual("41", duckStructCopy.InternalGetSetReferenceType);
            Assert.AreEqual("42", duckStructCopy.ProtectedGetSetReferenceType);
            Assert.AreEqual("43", duckStructCopy.PrivateGetSetReferenceType);
        }

        [TestCaseSource(nameof(Data))]
        public void UnionTest(object obscureObject)
        {
            var duckInterface = obscureObject.DuckCast<IDuckTypeUnion>();

            Assert.AreEqual("40", duckInterface.PublicGetSetReferenceType);

            duckInterface.PublicGetSetReferenceType = "42";
            Assert.AreEqual("42", duckInterface.PublicGetSetReferenceType);

            duckInterface.PublicGetSetReferenceType = "40";

            // *

            Assert.AreEqual("41", duckInterface.InternalGetSetReferenceType);

            duckInterface.InternalGetSetReferenceType = "42";
            Assert.AreEqual("42", duckInterface.InternalGetSetReferenceType);

            duckInterface.InternalGetSetReferenceType = "41";

            // *

            Assert.AreEqual("42", duckInterface.ProtectedGetSetReferenceType);

            duckInterface.ProtectedGetSetReferenceType = "45";
            Assert.AreEqual("45", duckInterface.ProtectedGetSetReferenceType);

            duckInterface.ProtectedGetSetReferenceType = "42";

            // *

            Assert.AreEqual("43", duckInterface.PrivateGetSetReferenceType);

            duckInterface.PrivateGetSetReferenceType = "42";
            Assert.AreEqual("42", duckInterface.PrivateGetSetReferenceType);

            duckInterface.PrivateGetSetReferenceType = "43";
        }
    }
}
