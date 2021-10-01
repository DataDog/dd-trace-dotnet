// <copyright file="TypeChainingFieldTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.DuckTyping.Tests.Fields.TypeChaining.ProxiesDefinitions;
using NUnit.Framework;

#pragma warning disable SA1201 // Elements must appear in the correct order

namespace Datadog.Trace.DuckTyping.Tests.Fields.TypeChaining
{
    public class TypeChainingFieldTests
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

            Assert.AreEqual(42, duckInterface.PublicStaticReadonlySelfTypeField.MagicNumber);
            Assert.AreEqual(42, duckAbstract.PublicStaticReadonlySelfTypeField.MagicNumber);
            Assert.AreEqual(42, duckVirtual.PublicStaticReadonlySelfTypeField.MagicNumber);

            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckInterface.PublicStaticReadonlySelfTypeField).Instance);
            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckAbstract.PublicStaticReadonlySelfTypeField).Instance);
            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckVirtual.PublicStaticReadonlySelfTypeField).Instance);

            // *

            Assert.AreEqual(42, duckInterface.InternalStaticReadonlySelfTypeField.MagicNumber);
            Assert.AreEqual(42, duckAbstract.InternalStaticReadonlySelfTypeField.MagicNumber);
            Assert.AreEqual(42, duckVirtual.InternalStaticReadonlySelfTypeField.MagicNumber);

            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckInterface.InternalStaticReadonlySelfTypeField).Instance);
            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckAbstract.InternalStaticReadonlySelfTypeField).Instance);
            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckVirtual.InternalStaticReadonlySelfTypeField).Instance);

            // *

            Assert.AreEqual(42, duckInterface.ProtectedStaticReadonlySelfTypeField.MagicNumber);
            Assert.AreEqual(42, duckAbstract.ProtectedStaticReadonlySelfTypeField.MagicNumber);
            Assert.AreEqual(42, duckVirtual.ProtectedStaticReadonlySelfTypeField.MagicNumber);

            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckInterface.ProtectedStaticReadonlySelfTypeField).Instance);
            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckAbstract.ProtectedStaticReadonlySelfTypeField).Instance);
            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckVirtual.ProtectedStaticReadonlySelfTypeField).Instance);

            // *

            Assert.AreEqual(42, duckInterface.PrivateStaticReadonlySelfTypeField.MagicNumber);
            Assert.AreEqual(42, duckAbstract.PrivateStaticReadonlySelfTypeField.MagicNumber);
            Assert.AreEqual(42, duckVirtual.PrivateStaticReadonlySelfTypeField.MagicNumber);

            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckInterface.PrivateStaticReadonlySelfTypeField).Instance);
            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckAbstract.PrivateStaticReadonlySelfTypeField).Instance);
            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckVirtual.PrivateStaticReadonlySelfTypeField).Instance);
        }

        [TestCaseSource(nameof(Data))]
        public void StaticFields(object obscureObject)
        {
            var duckInterface = obscureObject.DuckCast<IObscureDuckType>();
            var duckAbstract = obscureObject.DuckCast<ObscureDuckTypeAbstractClass>();
            var duckVirtual = obscureObject.DuckCast<ObscureDuckTypeVirtualClass>();

            IDummyFieldObject newDummy = null;

            // *
            newDummy = (new ObscureObject.DummyFieldObject { MagicNumber = 42 }).DuckCast<IDummyFieldObject>();
            duckInterface.PublicStaticSelfTypeField = newDummy;

            Assert.AreEqual(42, duckInterface.PublicStaticSelfTypeField.MagicNumber);
            Assert.AreEqual(42, duckAbstract.PublicStaticSelfTypeField.MagicNumber);
            Assert.AreEqual(42, duckVirtual.PublicStaticSelfTypeField.MagicNumber);

            // *
            newDummy = (new ObscureObject.DummyFieldObject { MagicNumber = 52 }).DuckCast<IDummyFieldObject>();
            duckInterface.InternalStaticSelfTypeField = newDummy;

            Assert.AreEqual(52, duckInterface.InternalStaticSelfTypeField.MagicNumber);
            Assert.AreEqual(52, duckAbstract.InternalStaticSelfTypeField.MagicNumber);
            Assert.AreEqual(52, duckVirtual.InternalStaticSelfTypeField.MagicNumber);

            // *
            newDummy = (new ObscureObject.DummyFieldObject { MagicNumber = 62 }).DuckCast<IDummyFieldObject>();
            duckAbstract.ProtectedStaticSelfTypeField = newDummy;

            Assert.AreEqual(62, duckInterface.ProtectedStaticSelfTypeField.MagicNumber);
            Assert.AreEqual(62, duckAbstract.ProtectedStaticSelfTypeField.MagicNumber);
            Assert.AreEqual(62, duckVirtual.ProtectedStaticSelfTypeField.MagicNumber);

            // *
            newDummy = (new ObscureObject.DummyFieldObject { MagicNumber = 72 }).DuckCast<IDummyFieldObject>();
            duckAbstract.PrivateStaticSelfTypeField = newDummy;

            Assert.AreEqual(72, duckInterface.PrivateStaticSelfTypeField.MagicNumber);
            Assert.AreEqual(72, duckAbstract.PrivateStaticSelfTypeField.MagicNumber);
            Assert.AreEqual(72, duckVirtual.PrivateStaticSelfTypeField.MagicNumber);
        }

        [TestCaseSource(nameof(Data))]
        public void ReadonlyFields(object obscureObject)
        {
            var duckInterface = obscureObject.DuckCast<IObscureDuckType>();
            var duckAbstract = obscureObject.DuckCast<ObscureDuckTypeAbstractClass>();
            var duckVirtual = obscureObject.DuckCast<ObscureDuckTypeVirtualClass>();

            // *

            Assert.AreEqual(42, duckInterface.PublicReadonlySelfTypeField.MagicNumber);
            Assert.AreEqual(42, duckAbstract.PublicReadonlySelfTypeField.MagicNumber);
            Assert.AreEqual(42, duckVirtual.PublicReadonlySelfTypeField.MagicNumber);

            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckInterface.PublicReadonlySelfTypeField).Instance);
            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckAbstract.PublicReadonlySelfTypeField).Instance);
            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckVirtual.PublicReadonlySelfTypeField).Instance);

            // *

            Assert.AreEqual(42, duckInterface.InternalReadonlySelfTypeField.MagicNumber);
            Assert.AreEqual(42, duckAbstract.InternalReadonlySelfTypeField.MagicNumber);
            Assert.AreEqual(42, duckVirtual.InternalReadonlySelfTypeField.MagicNumber);

            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckInterface.InternalReadonlySelfTypeField).Instance);
            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckAbstract.InternalReadonlySelfTypeField).Instance);
            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckVirtual.InternalReadonlySelfTypeField).Instance);

            // *

            Assert.AreEqual(42, duckInterface.ProtectedReadonlySelfTypeField.MagicNumber);
            Assert.AreEqual(42, duckAbstract.ProtectedReadonlySelfTypeField.MagicNumber);
            Assert.AreEqual(42, duckVirtual.ProtectedReadonlySelfTypeField.MagicNumber);

            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckInterface.ProtectedReadonlySelfTypeField).Instance);
            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckAbstract.ProtectedReadonlySelfTypeField).Instance);
            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckVirtual.ProtectedReadonlySelfTypeField).Instance);

            // *

            Assert.AreEqual(42, duckInterface.PrivateReadonlySelfTypeField.MagicNumber);
            Assert.AreEqual(42, duckAbstract.PrivateReadonlySelfTypeField.MagicNumber);
            Assert.AreEqual(42, duckVirtual.PrivateReadonlySelfTypeField.MagicNumber);

            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckInterface.PrivateReadonlySelfTypeField).Instance);
            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckAbstract.PrivateReadonlySelfTypeField).Instance);
            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckVirtual.PrivateReadonlySelfTypeField).Instance);
        }

        [TestCaseSource(nameof(Data))]
        public void Fields(object obscureObject)
        {
            var duckInterface = obscureObject.DuckCast<IObscureDuckType>();
            var duckAbstract = obscureObject.DuckCast<ObscureDuckTypeAbstractClass>();
            var duckVirtual = obscureObject.DuckCast<ObscureDuckTypeVirtualClass>();

            IDummyFieldObject newDummy = null;

            // *
            newDummy = (new ObscureObject.DummyFieldObject { MagicNumber = 42 }).DuckCast<IDummyFieldObject>();
            duckInterface.PublicSelfTypeField = newDummy;

            Assert.AreEqual(42, duckInterface.PublicSelfTypeField.MagicNumber);
            Assert.AreEqual(42, duckAbstract.PublicSelfTypeField.MagicNumber);
            Assert.AreEqual(42, duckVirtual.PublicSelfTypeField.MagicNumber);

            // *
            newDummy = (new ObscureObject.DummyFieldObject { MagicNumber = 52 }).DuckCast<IDummyFieldObject>();
            duckInterface.InternalSelfTypeField = newDummy;

            Assert.AreEqual(52, duckInterface.InternalSelfTypeField.MagicNumber);
            Assert.AreEqual(52, duckAbstract.InternalSelfTypeField.MagicNumber);
            Assert.AreEqual(52, duckVirtual.InternalSelfTypeField.MagicNumber);

            // *
            newDummy = (new ObscureObject.DummyFieldObject { MagicNumber = 62 }).DuckCast<IDummyFieldObject>();
            duckInterface.ProtectedSelfTypeField = newDummy;

            Assert.AreEqual(62, duckInterface.ProtectedSelfTypeField.MagicNumber);
            Assert.AreEqual(62, duckAbstract.ProtectedSelfTypeField.MagicNumber);
            Assert.AreEqual(62, duckVirtual.ProtectedSelfTypeField.MagicNumber);

            // *
            newDummy = (new ObscureObject.DummyFieldObject { MagicNumber = 72 }).DuckCast<IDummyFieldObject>();
            duckInterface.PrivateSelfTypeField = newDummy;

            Assert.AreEqual(72, duckInterface.PrivateSelfTypeField.MagicNumber);
            Assert.AreEqual(72, duckAbstract.PrivateSelfTypeField.MagicNumber);
            Assert.AreEqual(72, duckVirtual.PrivateSelfTypeField.MagicNumber);
        }
    }
}
