// <copyright file="TypeChainingPropertyTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.DuckTyping.Tests.Properties.TypeChaining.ProxiesDefinitions;
using NUnit.Framework;

namespace Datadog.Trace.DuckTyping.Tests.Properties.TypeChaining
{
    public class TypeChainingPropertyTests
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

            Assert.AreEqual(42, duckInterface.PublicStaticGetSelfType.MagicNumber);
            Assert.AreEqual(42, duckAbstract.PublicStaticGetSelfType.MagicNumber);
            Assert.AreEqual(42, duckVirtual.PublicStaticGetSelfType.MagicNumber);

            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckInterface.PublicStaticGetSelfType).Instance);
            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckAbstract.PublicStaticGetSelfType).Instance);
            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckVirtual.PublicStaticGetSelfType).Instance);

            // *

            Assert.AreEqual(42, duckInterface.InternalStaticGetSelfType.MagicNumber);
            Assert.AreEqual(42, duckAbstract.InternalStaticGetSelfType.MagicNumber);
            Assert.AreEqual(42, duckVirtual.InternalStaticGetSelfType.MagicNumber);

            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckInterface.InternalStaticGetSelfType).Instance);
            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckAbstract.InternalStaticGetSelfType).Instance);
            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckVirtual.InternalStaticGetSelfType).Instance);

            // *

            Assert.AreEqual(42, duckInterface.ProtectedStaticGetSelfType.MagicNumber);
            Assert.AreEqual(42, duckAbstract.ProtectedStaticGetSelfType.MagicNumber);
            Assert.AreEqual(42, duckVirtual.ProtectedStaticGetSelfType.MagicNumber);

            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckInterface.ProtectedStaticGetSelfType).Instance);
            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckAbstract.ProtectedStaticGetSelfType).Instance);
            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckVirtual.ProtectedStaticGetSelfType).Instance);

            // *

            Assert.AreEqual(42, duckInterface.PrivateStaticGetSelfType.MagicNumber);
            Assert.AreEqual(42, duckAbstract.PrivateStaticGetSelfType.MagicNumber);
            Assert.AreEqual(42, duckVirtual.PrivateStaticGetSelfType.MagicNumber);

            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckInterface.PrivateStaticGetSelfType).Instance);
            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckAbstract.PrivateStaticGetSelfType).Instance);
            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckVirtual.PrivateStaticGetSelfType).Instance);
        }

        [TestCaseSource(nameof(Data))]
        public void StaticProperties(object obscureObject)
        {
            var duckInterface = obscureObject.DuckCast<IObscureDuckType>();
            var duckAbstract = obscureObject.DuckCast<ObscureDuckTypeAbstractClass>();
            var duckVirtual = obscureObject.DuckCast<ObscureDuckTypeVirtualClass>();

            IDummyFieldObject newDummy = null;

            // *
            newDummy = (new ObscureObject.DummyFieldObject { MagicNumber = 42 }).DuckCast<IDummyFieldObject>();
            duckInterface.PublicStaticGetSetSelfType = newDummy;

            Assert.AreEqual(42, duckInterface.PublicStaticGetSetSelfType.MagicNumber);
            Assert.AreEqual(42, duckAbstract.PublicStaticGetSetSelfType.MagicNumber);
            Assert.AreEqual(42, duckVirtual.PublicStaticGetSetSelfType.MagicNumber);

            // *
            newDummy = (new ObscureObject.DummyFieldObject { MagicNumber = 52 }).DuckCast<IDummyFieldObject>();
            duckInterface.InternalStaticGetSetSelfType = newDummy;

            Assert.AreEqual(52, duckInterface.InternalStaticGetSetSelfType.MagicNumber);
            Assert.AreEqual(52, duckAbstract.InternalStaticGetSetSelfType.MagicNumber);
            Assert.AreEqual(52, duckVirtual.InternalStaticGetSetSelfType.MagicNumber);

            newDummy = (new ObscureObject.DummyFieldObject { MagicNumber = 42 }).DuckCast<IDummyFieldObject>();
            duckInterface.InternalStaticGetSetSelfType = newDummy;

            // *
            newDummy = (new ObscureObject.DummyFieldObject { MagicNumber = 62 }).DuckCast<IDummyFieldObject>();
            duckAbstract.ProtectedStaticGetSetSelfType = newDummy;

            Assert.AreEqual(62, duckInterface.ProtectedStaticGetSetSelfType.MagicNumber);
            Assert.AreEqual(62, duckAbstract.ProtectedStaticGetSetSelfType.MagicNumber);
            Assert.AreEqual(62, duckVirtual.ProtectedStaticGetSetSelfType.MagicNumber);

            newDummy = (new ObscureObject.DummyFieldObject { MagicNumber = 42 }).DuckCast<IDummyFieldObject>();
            duckAbstract.ProtectedStaticGetSetSelfType = newDummy;

            // *
            newDummy = (new ObscureObject.DummyFieldObject { MagicNumber = 72 }).DuckCast<IDummyFieldObject>();
            duckAbstract.PrivateStaticGetSetSelfType = newDummy;

            Assert.AreEqual(72, duckInterface.PrivateStaticGetSetSelfType.MagicNumber);
            Assert.AreEqual(72, duckAbstract.PrivateStaticGetSetSelfType.MagicNumber);
            Assert.AreEqual(72, duckVirtual.PrivateStaticGetSetSelfType.MagicNumber);

            newDummy = (new ObscureObject.DummyFieldObject { MagicNumber = 42 }).DuckCast<IDummyFieldObject>();
            duckAbstract.PrivateStaticGetSetSelfType = newDummy;
        }

        [TestCaseSource(nameof(Data))]
        public void GetOnlyProperties(object obscureObject)
        {
            var duckInterface = obscureObject.DuckCast<IObscureDuckType>();
            var duckAbstract = obscureObject.DuckCast<ObscureDuckTypeAbstractClass>();
            var duckVirtual = obscureObject.DuckCast<ObscureDuckTypeVirtualClass>();

            // *

            Assert.AreEqual(42, duckInterface.PublicGetSelfType.MagicNumber);
            Assert.AreEqual(42, duckAbstract.PublicGetSelfType.MagicNumber);
            Assert.AreEqual(42, duckVirtual.PublicGetSelfType.MagicNumber);

            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckInterface.PublicGetSelfType).Instance);
            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckAbstract.PublicGetSelfType).Instance);
            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckVirtual.PublicGetSelfType).Instance);

            // *

            Assert.AreEqual(42, duckInterface.InternalGetSelfType.MagicNumber);
            Assert.AreEqual(42, duckAbstract.InternalGetSelfType.MagicNumber);
            Assert.AreEqual(42, duckVirtual.InternalGetSelfType.MagicNumber);

            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckInterface.InternalGetSelfType).Instance);
            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckAbstract.InternalGetSelfType).Instance);
            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckVirtual.InternalGetSelfType).Instance);

            // *

            Assert.AreEqual(42, duckInterface.ProtectedGetSelfType.MagicNumber);
            Assert.AreEqual(42, duckAbstract.ProtectedGetSelfType.MagicNumber);
            Assert.AreEqual(42, duckVirtual.ProtectedGetSelfType.MagicNumber);

            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckInterface.ProtectedGetSelfType).Instance);
            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckAbstract.ProtectedGetSelfType).Instance);
            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckVirtual.ProtectedGetSelfType).Instance);

            // *

            Assert.AreEqual(42, duckInterface.PrivateGetSelfType.MagicNumber);
            Assert.AreEqual(42, duckAbstract.PrivateGetSelfType.MagicNumber);
            Assert.AreEqual(42, duckVirtual.PrivateGetSelfType.MagicNumber);

            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckInterface.PrivateGetSelfType).Instance);
            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckAbstract.PrivateGetSelfType).Instance);
            Assert.AreEqual(ObscureObject.DummyFieldObject.Default, ((IDuckType)duckVirtual.PrivateGetSelfType).Instance);
        }

        [TestCaseSource(nameof(Data))]
        public void Properties(object obscureObject)
        {
            var duckInterface = obscureObject.DuckCast<IObscureDuckType>();
            var duckAbstract = obscureObject.DuckCast<ObscureDuckTypeAbstractClass>();
            var duckVirtual = obscureObject.DuckCast<ObscureDuckTypeVirtualClass>();

            IDummyFieldObject newDummy = null;

            // *
            newDummy = (new ObscureObject.DummyFieldObject { MagicNumber = 42 }).DuckCast<IDummyFieldObject>();
            duckInterface.PublicGetSetSelfType = newDummy;

            Assert.AreEqual(42, duckInterface.PublicGetSetSelfType.MagicNumber);
            Assert.AreEqual(42, duckAbstract.PublicGetSetSelfType.MagicNumber);
            Assert.AreEqual(42, duckVirtual.PublicGetSetSelfType.MagicNumber);

            // *
            newDummy = (new ObscureObject.DummyFieldObject { MagicNumber = 52 }).DuckCast<IDummyFieldObject>();
            duckInterface.InternalGetSetSelfType = newDummy;

            Assert.AreEqual(52, duckInterface.InternalGetSetSelfType.MagicNumber);
            Assert.AreEqual(52, duckAbstract.InternalGetSetSelfType.MagicNumber);
            Assert.AreEqual(52, duckVirtual.InternalGetSetSelfType.MagicNumber);

            newDummy = (new ObscureObject.DummyFieldObject { MagicNumber = 42 }).DuckCast<IDummyFieldObject>();
            duckInterface.InternalGetSetSelfType = newDummy;

            // *
            newDummy = (new ObscureObject.DummyFieldObject { MagicNumber = 62 }).DuckCast<IDummyFieldObject>();
            duckInterface.ProtectedGetSetSelfType = newDummy;

            Assert.AreEqual(62, duckInterface.ProtectedGetSetSelfType.MagicNumber);
            Assert.AreEqual(62, duckAbstract.ProtectedGetSetSelfType.MagicNumber);
            Assert.AreEqual(62, duckVirtual.ProtectedGetSetSelfType.MagicNumber);

            newDummy = (new ObscureObject.DummyFieldObject { MagicNumber = 42 }).DuckCast<IDummyFieldObject>();
            duckInterface.ProtectedGetSetSelfType = newDummy;

            // *
            newDummy = (new ObscureObject.DummyFieldObject { MagicNumber = 72 }).DuckCast<IDummyFieldObject>();
            duckInterface.PrivateGetSetSelfType = newDummy;

            Assert.AreEqual(72, duckInterface.PrivateGetSetSelfType.MagicNumber);
            Assert.AreEqual(72, duckAbstract.PrivateGetSetSelfType.MagicNumber);
            Assert.AreEqual(72, duckVirtual.PrivateGetSetSelfType.MagicNumber);

            newDummy = (new ObscureObject.DummyFieldObject { MagicNumber = 42 }).DuckCast<IDummyFieldObject>();
            duckInterface.PrivateGetSetSelfType = newDummy;
        }

        [TestCaseSource(nameof(Data))]
        public void StructCopy(object obscureObject)
        {
            var duckStructCopy = obscureObject.DuckCast<ObscureDuckTypeStruct>();

            Assert.AreEqual(42, duckStructCopy.PublicStaticGetSelfType.MagicNumber);
            Assert.AreEqual(42, duckStructCopy.InternalStaticGetSelfType.MagicNumber);
            Assert.AreEqual(42, duckStructCopy.ProtectedStaticGetSelfType.MagicNumber);
            Assert.AreEqual(42, duckStructCopy.PrivateStaticGetSelfType.MagicNumber);

            Assert.AreEqual(42, duckStructCopy.PublicStaticGetSetSelfType.MagicNumber);
            Assert.AreEqual(42, duckStructCopy.InternalStaticGetSetSelfType.MagicNumber);
            Assert.AreEqual(42, duckStructCopy.ProtectedStaticGetSetSelfType.MagicNumber);
            Assert.AreEqual(42, duckStructCopy.PrivateStaticGetSetSelfType.MagicNumber);

            Assert.AreEqual(42, duckStructCopy.PublicGetSelfType.MagicNumber);
            Assert.AreEqual(42, duckStructCopy.InternalGetSelfType.MagicNumber);
            Assert.AreEqual(42, duckStructCopy.ProtectedGetSelfType.MagicNumber);
            Assert.AreEqual(42, duckStructCopy.PrivateGetSelfType.MagicNumber);

            Assert.AreEqual(42, duckStructCopy.PublicGetSetSelfType.MagicNumber);
            Assert.AreEqual(42, duckStructCopy.InternalGetSetSelfType.MagicNumber);
            Assert.AreEqual(42, duckStructCopy.ProtectedGetSetSelfType.MagicNumber);
            Assert.AreEqual(42, duckStructCopy.PrivateGetSetSelfType.MagicNumber);
        }
    }
}
