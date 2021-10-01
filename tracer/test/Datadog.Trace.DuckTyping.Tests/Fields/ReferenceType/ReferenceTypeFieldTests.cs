// <copyright file="ReferenceTypeFieldTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.DuckTyping.Tests.Fields.ReferenceType.ProxiesDefinitions;
using NUnit.Framework;

namespace Datadog.Trace.DuckTyping.Tests.Fields.ReferenceType
{
    public class ReferenceTypeFieldTests
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
            Assert.AreEqual("10", duckInterface.PublicStaticReadonlyReferenceTypeField);
            Assert.AreEqual("10", duckAbstract.PublicStaticReadonlyReferenceTypeField);
            Assert.AreEqual("10", duckVirtual.PublicStaticReadonlyReferenceTypeField);

            // *
            Assert.AreEqual("11", duckInterface.InternalStaticReadonlyReferenceTypeField);
            Assert.AreEqual("11", duckAbstract.InternalStaticReadonlyReferenceTypeField);
            Assert.AreEqual("11", duckVirtual.InternalStaticReadonlyReferenceTypeField);

            // *
            Assert.AreEqual("12", duckInterface.ProtectedStaticReadonlyReferenceTypeField);
            Assert.AreEqual("12", duckAbstract.ProtectedStaticReadonlyReferenceTypeField);
            Assert.AreEqual("12", duckVirtual.ProtectedStaticReadonlyReferenceTypeField);

            // *
            Assert.AreEqual("13", duckInterface.PrivateStaticReadonlyReferenceTypeField);
            Assert.AreEqual("13", duckAbstract.PrivateStaticReadonlyReferenceTypeField);
            Assert.AreEqual("13", duckVirtual.PrivateStaticReadonlyReferenceTypeField);
        }

        [TestCaseSource(nameof(Data))]
        public void StaticFields(object obscureObject)
        {
            var duckInterface = obscureObject.DuckCast<IObscureDuckType>();
            var duckAbstract = obscureObject.DuckCast<ObscureDuckTypeAbstractClass>();
            var duckVirtual = obscureObject.DuckCast<ObscureDuckTypeVirtualClass>();

            Assert.AreEqual("20", duckInterface.PublicStaticReferenceTypeField);
            Assert.AreEqual("20", duckAbstract.PublicStaticReferenceTypeField);
            Assert.AreEqual("20", duckVirtual.PublicStaticReferenceTypeField);

            duckInterface.PublicStaticReferenceTypeField = "42";
            Assert.AreEqual("42", duckInterface.PublicStaticReferenceTypeField);
            Assert.AreEqual("42", duckAbstract.PublicStaticReferenceTypeField);
            Assert.AreEqual("42", duckVirtual.PublicStaticReferenceTypeField);

            duckAbstract.PublicStaticReferenceTypeField = "50";
            Assert.AreEqual("50", duckInterface.PublicStaticReferenceTypeField);
            Assert.AreEqual("50", duckAbstract.PublicStaticReferenceTypeField);
            Assert.AreEqual("50", duckVirtual.PublicStaticReferenceTypeField);

            duckVirtual.PublicStaticReferenceTypeField = "60";
            Assert.AreEqual("60", duckInterface.PublicStaticReferenceTypeField);
            Assert.AreEqual("60", duckAbstract.PublicStaticReferenceTypeField);
            Assert.AreEqual("60", duckVirtual.PublicStaticReferenceTypeField);

            // *

            Assert.AreEqual("21", duckInterface.InternalStaticReferenceTypeField);
            Assert.AreEqual("21", duckAbstract.InternalStaticReferenceTypeField);
            Assert.AreEqual("21", duckVirtual.InternalStaticReferenceTypeField);

            duckInterface.InternalStaticReferenceTypeField = "42";
            Assert.AreEqual("42", duckInterface.InternalStaticReferenceTypeField);
            Assert.AreEqual("42", duckAbstract.InternalStaticReferenceTypeField);
            Assert.AreEqual("42", duckVirtual.InternalStaticReferenceTypeField);

            duckAbstract.InternalStaticReferenceTypeField = "50";
            Assert.AreEqual("50", duckInterface.InternalStaticReferenceTypeField);
            Assert.AreEqual("50", duckAbstract.InternalStaticReferenceTypeField);
            Assert.AreEqual("50", duckVirtual.InternalStaticReferenceTypeField);

            duckVirtual.InternalStaticReferenceTypeField = "60";
            Assert.AreEqual("60", duckInterface.InternalStaticReferenceTypeField);
            Assert.AreEqual("60", duckAbstract.InternalStaticReferenceTypeField);
            Assert.AreEqual("60", duckVirtual.InternalStaticReferenceTypeField);

            // *

            Assert.AreEqual("22", duckInterface.ProtectedStaticReferenceTypeField);
            Assert.AreEqual("22", duckAbstract.ProtectedStaticReferenceTypeField);
            Assert.AreEqual("22", duckVirtual.ProtectedStaticReferenceTypeField);

            duckInterface.ProtectedStaticReferenceTypeField = "42";
            Assert.AreEqual("42", duckInterface.ProtectedStaticReferenceTypeField);
            Assert.AreEqual("42", duckAbstract.ProtectedStaticReferenceTypeField);
            Assert.AreEqual("42", duckVirtual.ProtectedStaticReferenceTypeField);

            duckAbstract.ProtectedStaticReferenceTypeField = "50";
            Assert.AreEqual("50", duckInterface.ProtectedStaticReferenceTypeField);
            Assert.AreEqual("50", duckAbstract.ProtectedStaticReferenceTypeField);
            Assert.AreEqual("50", duckVirtual.ProtectedStaticReferenceTypeField);

            duckVirtual.ProtectedStaticReferenceTypeField = "60";
            Assert.AreEqual("60", duckInterface.ProtectedStaticReferenceTypeField);
            Assert.AreEqual("60", duckAbstract.ProtectedStaticReferenceTypeField);
            Assert.AreEqual("60", duckVirtual.ProtectedStaticReferenceTypeField);

            // *

            Assert.AreEqual("23", duckInterface.PrivateStaticReferenceTypeField);
            Assert.AreEqual("23", duckAbstract.PrivateStaticReferenceTypeField);
            Assert.AreEqual("23", duckVirtual.PrivateStaticReferenceTypeField);

            duckInterface.PrivateStaticReferenceTypeField = "42";
            Assert.AreEqual("42", duckInterface.PrivateStaticReferenceTypeField);
            Assert.AreEqual("42", duckAbstract.PrivateStaticReferenceTypeField);
            Assert.AreEqual("42", duckVirtual.PrivateStaticReferenceTypeField);

            duckAbstract.PrivateStaticReferenceTypeField = "50";
            Assert.AreEqual("50", duckInterface.PrivateStaticReferenceTypeField);
            Assert.AreEqual("50", duckAbstract.PrivateStaticReferenceTypeField);
            Assert.AreEqual("50", duckVirtual.PrivateStaticReferenceTypeField);

            duckVirtual.PrivateStaticReferenceTypeField = "60";
            Assert.AreEqual("60", duckInterface.PrivateStaticReferenceTypeField);
            Assert.AreEqual("60", duckAbstract.PrivateStaticReferenceTypeField);
            Assert.AreEqual("60", duckVirtual.PrivateStaticReferenceTypeField);
        }

        [TestCaseSource(nameof(Data))]
        public void ReadonlyFields(object obscureObject)
        {
            var duckInterface = obscureObject.DuckCast<IObscureDuckType>();
            var duckAbstract = obscureObject.DuckCast<ObscureDuckTypeAbstractClass>();
            var duckVirtual = obscureObject.DuckCast<ObscureDuckTypeVirtualClass>();

            // *
            Assert.AreEqual("30", duckInterface.PublicReadonlyReferenceTypeField);
            Assert.AreEqual("30", duckAbstract.PublicReadonlyReferenceTypeField);
            Assert.AreEqual("30", duckVirtual.PublicReadonlyReferenceTypeField);

            // *
            Assert.AreEqual("31", duckInterface.InternalReadonlyReferenceTypeField);
            Assert.AreEqual("31", duckAbstract.InternalReadonlyReferenceTypeField);
            Assert.AreEqual("31", duckVirtual.InternalReadonlyReferenceTypeField);

            // *
            Assert.AreEqual("32", duckInterface.ProtectedReadonlyReferenceTypeField);
            Assert.AreEqual("32", duckAbstract.ProtectedReadonlyReferenceTypeField);
            Assert.AreEqual("32", duckVirtual.ProtectedReadonlyReferenceTypeField);

            // *
            Assert.AreEqual("33", duckInterface.PrivateReadonlyReferenceTypeField);
            Assert.AreEqual("33", duckAbstract.PrivateReadonlyReferenceTypeField);
            Assert.AreEqual("33", duckVirtual.PrivateReadonlyReferenceTypeField);
        }

        [TestCaseSource(nameof(Data))]
        public void Fields(object obscureObject)
        {
            var duckInterface = obscureObject.DuckCast<IObscureDuckType>();
            var duckAbstract = obscureObject.DuckCast<ObscureDuckTypeAbstractClass>();
            var duckVirtual = obscureObject.DuckCast<ObscureDuckTypeVirtualClass>();

            Assert.AreEqual("40", duckInterface.PublicReferenceTypeField);
            Assert.AreEqual("40", duckAbstract.PublicReferenceTypeField);
            Assert.AreEqual("40", duckVirtual.PublicReferenceTypeField);

            duckInterface.PublicReferenceTypeField = "42";
            Assert.AreEqual("42", duckInterface.PublicReferenceTypeField);
            Assert.AreEqual("42", duckAbstract.PublicReferenceTypeField);
            Assert.AreEqual("42", duckVirtual.PublicReferenceTypeField);

            duckAbstract.PublicReferenceTypeField = "50";
            Assert.AreEqual("50", duckInterface.PublicReferenceTypeField);
            Assert.AreEqual("50", duckAbstract.PublicReferenceTypeField);
            Assert.AreEqual("50", duckVirtual.PublicReferenceTypeField);

            duckVirtual.PublicReferenceTypeField = "60";
            Assert.AreEqual("60", duckInterface.PublicReferenceTypeField);
            Assert.AreEqual("60", duckAbstract.PublicReferenceTypeField);
            Assert.AreEqual("60", duckVirtual.PublicReferenceTypeField);

            // *

            Assert.AreEqual("41", duckInterface.InternalReferenceTypeField);
            Assert.AreEqual("41", duckAbstract.InternalReferenceTypeField);
            Assert.AreEqual("41", duckVirtual.InternalReferenceTypeField);

            duckInterface.InternalReferenceTypeField = "42";
            Assert.AreEqual("42", duckInterface.InternalReferenceTypeField);
            Assert.AreEqual("42", duckAbstract.InternalReferenceTypeField);
            Assert.AreEqual("42", duckVirtual.InternalReferenceTypeField);

            duckAbstract.InternalReferenceTypeField = "50";
            Assert.AreEqual("50", duckInterface.InternalReferenceTypeField);
            Assert.AreEqual("50", duckAbstract.InternalReferenceTypeField);
            Assert.AreEqual("50", duckVirtual.InternalReferenceTypeField);

            duckVirtual.InternalReferenceTypeField = "60";
            Assert.AreEqual("60", duckInterface.InternalReferenceTypeField);
            Assert.AreEqual("60", duckAbstract.InternalReferenceTypeField);
            Assert.AreEqual("60", duckVirtual.InternalReferenceTypeField);

            // *

            Assert.AreEqual("42", duckInterface.ProtectedReferenceTypeField);
            Assert.AreEqual("42", duckAbstract.ProtectedReferenceTypeField);
            Assert.AreEqual("42", duckVirtual.ProtectedReferenceTypeField);

            duckInterface.ProtectedReferenceTypeField = "45";
            Assert.AreEqual("45", duckInterface.ProtectedReferenceTypeField);
            Assert.AreEqual("45", duckAbstract.ProtectedReferenceTypeField);
            Assert.AreEqual("45", duckVirtual.ProtectedReferenceTypeField);

            duckAbstract.ProtectedReferenceTypeField = "50";
            Assert.AreEqual("50", duckInterface.ProtectedReferenceTypeField);
            Assert.AreEqual("50", duckAbstract.ProtectedReferenceTypeField);
            Assert.AreEqual("50", duckVirtual.ProtectedReferenceTypeField);

            duckVirtual.ProtectedReferenceTypeField = "60";
            Assert.AreEqual("60", duckInterface.ProtectedReferenceTypeField);
            Assert.AreEqual("60", duckAbstract.ProtectedReferenceTypeField);
            Assert.AreEqual("60", duckVirtual.ProtectedReferenceTypeField);

            // *

            Assert.AreEqual("43", duckInterface.PrivateReferenceTypeField);
            Assert.AreEqual("43", duckAbstract.PrivateReferenceTypeField);
            Assert.AreEqual("43", duckVirtual.PrivateReferenceTypeField);

            duckInterface.PrivateReferenceTypeField = "42";
            Assert.AreEqual("42", duckInterface.PrivateReferenceTypeField);
            Assert.AreEqual("42", duckAbstract.PrivateReferenceTypeField);
            Assert.AreEqual("42", duckVirtual.PrivateReferenceTypeField);

            duckAbstract.PrivateReferenceTypeField = "50";
            Assert.AreEqual("50", duckInterface.PrivateReferenceTypeField);
            Assert.AreEqual("50", duckAbstract.PrivateReferenceTypeField);
            Assert.AreEqual("50", duckVirtual.PrivateReferenceTypeField);

            duckVirtual.PrivateReferenceTypeField = "60";
            Assert.AreEqual("60", duckInterface.PrivateReferenceTypeField);
            Assert.AreEqual("60", duckAbstract.PrivateReferenceTypeField);
            Assert.AreEqual("60", duckVirtual.PrivateReferenceTypeField);
        }
    }
}
