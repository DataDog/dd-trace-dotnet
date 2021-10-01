// <copyright file="ValueTypePropertyTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.DuckTyping.Tests.Properties.ValueType.ProxiesDefinitions;
using NUnit.Framework;

namespace Datadog.Trace.DuckTyping.Tests.Properties.ValueType
{
    public class ValueTypePropertyTests
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
            Assert.AreEqual(10, duckInterface.PublicStaticGetValueType);
            Assert.AreEqual(10, duckAbstract.PublicStaticGetValueType);
            Assert.AreEqual(10, duckVirtual.PublicStaticGetValueType);

            // *
            Assert.AreEqual(11, duckInterface.InternalStaticGetValueType);
            Assert.AreEqual(11, duckAbstract.InternalStaticGetValueType);
            Assert.AreEqual(11, duckVirtual.InternalStaticGetValueType);

            // *
            Assert.AreEqual(12, duckInterface.ProtectedStaticGetValueType);
            Assert.AreEqual(12, duckAbstract.ProtectedStaticGetValueType);
            Assert.AreEqual(12, duckVirtual.ProtectedStaticGetValueType);

            // *
            Assert.AreEqual(13, duckInterface.PrivateStaticGetValueType);
            Assert.AreEqual(13, duckAbstract.PrivateStaticGetValueType);
            Assert.AreEqual(13, duckVirtual.PrivateStaticGetValueType);
        }

        [TestCaseSource(nameof(Data))]
        public void StaticProperties(object obscureObject)
        {
            var duckInterface = obscureObject.DuckCast<IObscureDuckType>();
            var duckAbstract = obscureObject.DuckCast<ObscureDuckTypeAbstractClass>();
            var duckVirtual = obscureObject.DuckCast<ObscureDuckTypeVirtualClass>();

            Assert.AreEqual(20, duckInterface.PublicStaticGetSetValueType);
            Assert.AreEqual(20, duckAbstract.PublicStaticGetSetValueType);
            Assert.AreEqual(20, duckVirtual.PublicStaticGetSetValueType);

            duckInterface.PublicStaticGetSetValueType = 42;
            Assert.AreEqual(42, duckInterface.PublicStaticGetSetValueType);
            Assert.AreEqual(42, duckAbstract.PublicStaticGetSetValueType);
            Assert.AreEqual(42, duckVirtual.PublicStaticGetSetValueType);

            duckAbstract.PublicStaticGetSetValueType = 50;
            Assert.AreEqual(50, duckInterface.PublicStaticGetSetValueType);
            Assert.AreEqual(50, duckAbstract.PublicStaticGetSetValueType);
            Assert.AreEqual(50, duckVirtual.PublicStaticGetSetValueType);

            duckVirtual.PublicStaticGetSetValueType = 60;
            Assert.AreEqual(60, duckInterface.PublicStaticGetSetValueType);
            Assert.AreEqual(60, duckAbstract.PublicStaticGetSetValueType);
            Assert.AreEqual(60, duckVirtual.PublicStaticGetSetValueType);

            duckInterface.PublicStaticGetSetValueType = 20;

            // *

            Assert.AreEqual(21, duckInterface.InternalStaticGetSetValueType);
            Assert.AreEqual(21, duckAbstract.InternalStaticGetSetValueType);
            Assert.AreEqual(21, duckVirtual.InternalStaticGetSetValueType);

            duckInterface.InternalStaticGetSetValueType = 42;
            Assert.AreEqual(42, duckInterface.InternalStaticGetSetValueType);
            Assert.AreEqual(42, duckAbstract.InternalStaticGetSetValueType);
            Assert.AreEqual(42, duckVirtual.InternalStaticGetSetValueType);

            duckAbstract.InternalStaticGetSetValueType = 50;
            Assert.AreEqual(50, duckInterface.InternalStaticGetSetValueType);
            Assert.AreEqual(50, duckAbstract.InternalStaticGetSetValueType);
            Assert.AreEqual(50, duckVirtual.InternalStaticGetSetValueType);

            duckVirtual.InternalStaticGetSetValueType = 60;
            Assert.AreEqual(60, duckInterface.InternalStaticGetSetValueType);
            Assert.AreEqual(60, duckAbstract.InternalStaticGetSetValueType);
            Assert.AreEqual(60, duckVirtual.InternalStaticGetSetValueType);

            duckInterface.InternalStaticGetSetValueType = 21;

            // *

            Assert.AreEqual(22, duckInterface.ProtectedStaticGetSetValueType);
            Assert.AreEqual(22, duckAbstract.ProtectedStaticGetSetValueType);
            Assert.AreEqual(22, duckVirtual.ProtectedStaticGetSetValueType);

            duckInterface.ProtectedStaticGetSetValueType = 42;
            Assert.AreEqual(42, duckInterface.ProtectedStaticGetSetValueType);
            Assert.AreEqual(42, duckAbstract.ProtectedStaticGetSetValueType);
            Assert.AreEqual(42, duckVirtual.ProtectedStaticGetSetValueType);

            duckAbstract.ProtectedStaticGetSetValueType = 50;
            Assert.AreEqual(50, duckInterface.ProtectedStaticGetSetValueType);
            Assert.AreEqual(50, duckAbstract.ProtectedStaticGetSetValueType);
            Assert.AreEqual(50, duckVirtual.ProtectedStaticGetSetValueType);

            duckVirtual.ProtectedStaticGetSetValueType = 60;
            Assert.AreEqual(60, duckInterface.ProtectedStaticGetSetValueType);
            Assert.AreEqual(60, duckAbstract.ProtectedStaticGetSetValueType);
            Assert.AreEqual(60, duckVirtual.ProtectedStaticGetSetValueType);

            duckInterface.ProtectedStaticGetSetValueType = 22;

            // *

            Assert.AreEqual(23, duckInterface.PrivateStaticGetSetValueType);
            Assert.AreEqual(23, duckAbstract.PrivateStaticGetSetValueType);
            Assert.AreEqual(23, duckVirtual.PrivateStaticGetSetValueType);

            duckInterface.PrivateStaticGetSetValueType = 42;
            Assert.AreEqual(42, duckInterface.PrivateStaticGetSetValueType);
            Assert.AreEqual(42, duckAbstract.PrivateStaticGetSetValueType);
            Assert.AreEqual(42, duckVirtual.PrivateStaticGetSetValueType);

            duckAbstract.PrivateStaticGetSetValueType = 50;
            Assert.AreEqual(50, duckInterface.PrivateStaticGetSetValueType);
            Assert.AreEqual(50, duckAbstract.PrivateStaticGetSetValueType);
            Assert.AreEqual(50, duckVirtual.PrivateStaticGetSetValueType);

            duckVirtual.PrivateStaticGetSetValueType = 60;
            Assert.AreEqual(60, duckInterface.PrivateStaticGetSetValueType);
            Assert.AreEqual(60, duckAbstract.PrivateStaticGetSetValueType);
            Assert.AreEqual(60, duckVirtual.PrivateStaticGetSetValueType);

            duckInterface.PrivateStaticGetSetValueType = 23;
        }

        [TestCaseSource(nameof(Data))]
        public void GetOnlyProperties(object obscureObject)
        {
            var duckInterface = obscureObject.DuckCast<IObscureDuckType>();
            var duckAbstract = obscureObject.DuckCast<ObscureDuckTypeAbstractClass>();
            var duckVirtual = obscureObject.DuckCast<ObscureDuckTypeVirtualClass>();

            // *
            Assert.AreEqual(30, duckInterface.PublicGetValueType);
            Assert.AreEqual(30, duckAbstract.PublicGetValueType);
            Assert.AreEqual(30, duckVirtual.PublicGetValueType);

            // *
            Assert.AreEqual(31, duckInterface.InternalGetValueType);
            Assert.AreEqual(31, duckAbstract.InternalGetValueType);
            Assert.AreEqual(31, duckVirtual.InternalGetValueType);

            // *
            Assert.AreEqual(32, duckInterface.ProtectedGetValueType);
            Assert.AreEqual(32, duckAbstract.ProtectedGetValueType);
            Assert.AreEqual(32, duckVirtual.ProtectedGetValueType);

            // *
            Assert.AreEqual(33, duckInterface.PrivateGetValueType);
            Assert.AreEqual(33, duckAbstract.PrivateGetValueType);
            Assert.AreEqual(33, duckVirtual.PrivateGetValueType);
        }

        [TestCaseSource(nameof(Data))]
        public void Properties(object obscureObject)
        {
            var duckInterface = obscureObject.DuckCast<IObscureDuckType>();
            var duckAbstract = obscureObject.DuckCast<ObscureDuckTypeAbstractClass>();
            var duckVirtual = obscureObject.DuckCast<ObscureDuckTypeVirtualClass>();

            Assert.AreEqual(40, duckInterface.PublicGetSetValueType);
            Assert.AreEqual(40, duckAbstract.PublicGetSetValueType);
            Assert.AreEqual(40, duckVirtual.PublicGetSetValueType);

            duckInterface.PublicGetSetValueType = 42;
            Assert.AreEqual(42, duckInterface.PublicGetSetValueType);
            Assert.AreEqual(42, duckAbstract.PublicGetSetValueType);
            Assert.AreEqual(42, duckVirtual.PublicGetSetValueType);

            duckAbstract.PublicGetSetValueType = 50;
            Assert.AreEqual(50, duckInterface.PublicGetSetValueType);
            Assert.AreEqual(50, duckAbstract.PublicGetSetValueType);
            Assert.AreEqual(50, duckVirtual.PublicGetSetValueType);

            duckVirtual.PublicGetSetValueType = 60;
            Assert.AreEqual(60, duckInterface.PublicGetSetValueType);
            Assert.AreEqual(60, duckAbstract.PublicGetSetValueType);
            Assert.AreEqual(60, duckVirtual.PublicGetSetValueType);

            duckInterface.PublicGetSetValueType = 40;

            // *

            Assert.AreEqual(41, duckInterface.InternalGetSetValueType);
            Assert.AreEqual(41, duckAbstract.InternalGetSetValueType);
            Assert.AreEqual(41, duckVirtual.InternalGetSetValueType);

            duckInterface.InternalGetSetValueType = 42;
            Assert.AreEqual(42, duckInterface.InternalGetSetValueType);
            Assert.AreEqual(42, duckAbstract.InternalGetSetValueType);
            Assert.AreEqual(42, duckVirtual.InternalGetSetValueType);

            duckAbstract.InternalGetSetValueType = 50;
            Assert.AreEqual(50, duckInterface.InternalGetSetValueType);
            Assert.AreEqual(50, duckAbstract.InternalGetSetValueType);
            Assert.AreEqual(50, duckVirtual.InternalGetSetValueType);

            duckVirtual.InternalGetSetValueType = 60;
            Assert.AreEqual(60, duckInterface.InternalGetSetValueType);
            Assert.AreEqual(60, duckAbstract.InternalGetSetValueType);
            Assert.AreEqual(60, duckVirtual.InternalGetSetValueType);

            duckInterface.InternalGetSetValueType = 41;

            // *

            Assert.AreEqual(42, duckInterface.ProtectedGetSetValueType);
            Assert.AreEqual(42, duckAbstract.ProtectedGetSetValueType);
            Assert.AreEqual(42, duckVirtual.ProtectedGetSetValueType);

            duckInterface.ProtectedGetSetValueType = 45;
            Assert.AreEqual(45, duckInterface.ProtectedGetSetValueType);
            Assert.AreEqual(45, duckAbstract.ProtectedGetSetValueType);
            Assert.AreEqual(45, duckVirtual.ProtectedGetSetValueType);

            duckAbstract.ProtectedGetSetValueType = 50;
            Assert.AreEqual(50, duckInterface.ProtectedGetSetValueType);
            Assert.AreEqual(50, duckAbstract.ProtectedGetSetValueType);
            Assert.AreEqual(50, duckVirtual.ProtectedGetSetValueType);

            duckVirtual.ProtectedGetSetValueType = 60;
            Assert.AreEqual(60, duckInterface.ProtectedGetSetValueType);
            Assert.AreEqual(60, duckAbstract.ProtectedGetSetValueType);
            Assert.AreEqual(60, duckVirtual.ProtectedGetSetValueType);

            duckInterface.ProtectedGetSetValueType = 42;

            // *

            Assert.AreEqual(43, duckInterface.PrivateGetSetValueType);
            Assert.AreEqual(43, duckAbstract.PrivateGetSetValueType);
            Assert.AreEqual(43, duckVirtual.PrivateGetSetValueType);

            duckInterface.PrivateGetSetValueType = 42;
            Assert.AreEqual(42, duckInterface.PrivateGetSetValueType);
            Assert.AreEqual(42, duckAbstract.PrivateGetSetValueType);
            Assert.AreEqual(42, duckVirtual.PrivateGetSetValueType);

            duckAbstract.PrivateGetSetValueType = 50;
            Assert.AreEqual(50, duckInterface.PrivateGetSetValueType);
            Assert.AreEqual(50, duckAbstract.PrivateGetSetValueType);
            Assert.AreEqual(50, duckVirtual.PrivateGetSetValueType);

            duckVirtual.PrivateGetSetValueType = 60;
            Assert.AreEqual(60, duckInterface.PrivateGetSetValueType);
            Assert.AreEqual(60, duckAbstract.PrivateGetSetValueType);
            Assert.AreEqual(60, duckVirtual.PrivateGetSetValueType);

            duckInterface.PrivateGetSetValueType = 43;
        }

        [TestCaseSource(nameof(Data))]
        public void Indexer(object obscureObject)
        {
            var duckInterface = obscureObject.DuckCast<IObscureDuckType>();
            var duckAbstract = obscureObject.DuckCast<ObscureDuckTypeAbstractClass>();
            var duckVirtual = obscureObject.DuckCast<ObscureDuckTypeVirtualClass>();

            duckInterface[1] = 100;
            Assert.AreEqual(100, duckInterface[1]);
            Assert.AreEqual(100, duckAbstract[1]);
            Assert.AreEqual(100, duckVirtual[1]);

            duckAbstract[2] = 200;
            Assert.AreEqual(200, duckInterface[2]);
            Assert.AreEqual(200, duckAbstract[2]);
            Assert.AreEqual(200, duckVirtual[2]);

            duckVirtual[3] = 300;
            Assert.AreEqual(300, duckInterface[3]);
            Assert.AreEqual(300, duckAbstract[3]);
            Assert.AreEqual(300, duckVirtual[3]);
        }

        [TestCaseSource(nameof(Data))]
        public void NullableOfKnown(object obscureObject)
        {
            var duckInterface = obscureObject.DuckCast<IObscureDuckType>();
            var duckAbstract = obscureObject.DuckCast<ObscureDuckTypeAbstractClass>();
            var duckVirtual = obscureObject.DuckCast<ObscureDuckTypeVirtualClass>();

            Assert.Null(duckInterface.PublicStaticNullableInt);
            Assert.Null(duckAbstract.PublicStaticNullableInt);
            Assert.Null(duckVirtual.PublicStaticNullableInt);

            duckInterface.PublicStaticNullableInt = 42;
            Assert.AreEqual(42, duckInterface.PublicStaticNullableInt);
            Assert.AreEqual(42, duckAbstract.PublicStaticNullableInt);
            Assert.AreEqual(42, duckVirtual.PublicStaticNullableInt);

            duckAbstract.PublicStaticNullableInt = 50;
            Assert.AreEqual(50, duckInterface.PublicStaticNullableInt);
            Assert.AreEqual(50, duckAbstract.PublicStaticNullableInt);
            Assert.AreEqual(50, duckVirtual.PublicStaticNullableInt);

            duckVirtual.PublicStaticNullableInt = null;
            Assert.Null(duckInterface.PublicStaticNullableInt);
            Assert.Null(duckAbstract.PublicStaticNullableInt);
            Assert.Null(duckVirtual.PublicStaticNullableInt);

            // *

            Assert.Null(duckInterface.PrivateStaticNullableInt);
            Assert.Null(duckAbstract.PrivateStaticNullableInt);
            Assert.Null(duckVirtual.PrivateStaticNullableInt);

            duckInterface.PrivateStaticNullableInt = 42;
            Assert.AreEqual(42, duckInterface.PrivateStaticNullableInt);
            Assert.AreEqual(42, duckAbstract.PrivateStaticNullableInt);
            Assert.AreEqual(42, duckVirtual.PrivateStaticNullableInt);

            duckAbstract.PrivateStaticNullableInt = 50;
            Assert.AreEqual(50, duckInterface.PrivateStaticNullableInt);
            Assert.AreEqual(50, duckAbstract.PrivateStaticNullableInt);
            Assert.AreEqual(50, duckVirtual.PrivateStaticNullableInt);

            duckVirtual.PrivateStaticNullableInt = null;
            Assert.Null(duckInterface.PrivateStaticNullableInt);
            Assert.Null(duckAbstract.PrivateStaticNullableInt);
            Assert.Null(duckVirtual.PrivateStaticNullableInt);

            // *

            Assert.Null(duckInterface.PublicNullableInt);
            Assert.Null(duckAbstract.PublicNullableInt);
            Assert.Null(duckVirtual.PublicNullableInt);

            duckInterface.PublicNullableInt = 42;
            Assert.AreEqual(42, duckInterface.PublicNullableInt);
            Assert.AreEqual(42, duckAbstract.PublicNullableInt);
            Assert.AreEqual(42, duckVirtual.PublicNullableInt);

            duckAbstract.PublicNullableInt = 50;
            Assert.AreEqual(50, duckInterface.PublicNullableInt);
            Assert.AreEqual(50, duckAbstract.PublicNullableInt);
            Assert.AreEqual(50, duckVirtual.PublicNullableInt);

            duckVirtual.PublicNullableInt = null;
            Assert.Null(duckInterface.PublicNullableInt);
            Assert.Null(duckAbstract.PublicNullableInt);
            Assert.Null(duckVirtual.PublicNullableInt);

            // *

            Assert.Null(duckInterface.PrivateNullableInt);
            Assert.Null(duckAbstract.PrivateNullableInt);
            Assert.Null(duckVirtual.PrivateNullableInt);

            duckInterface.PrivateNullableInt = 42;
            Assert.AreEqual(42, duckInterface.PrivateNullableInt);
            Assert.AreEqual(42, duckAbstract.PrivateNullableInt);
            Assert.AreEqual(42, duckVirtual.PrivateNullableInt);

            duckAbstract.PrivateNullableInt = 50;
            Assert.AreEqual(50, duckInterface.PrivateNullableInt);
            Assert.AreEqual(50, duckAbstract.PrivateNullableInt);
            Assert.AreEqual(50, duckVirtual.PrivateNullableInt);

            duckVirtual.PrivateNullableInt = null;
            Assert.Null(duckInterface.PrivateNullableInt);
            Assert.Null(duckAbstract.PrivateNullableInt);
            Assert.Null(duckVirtual.PrivateNullableInt);
        }

        [TestCaseSource(nameof(Data))]
        public void KnownEnum(object obscureObject)
        {
            var duckInterface = obscureObject.DuckCast<IObscureDuckType>();
            var duckAbstract = obscureObject.DuckCast<ObscureDuckTypeAbstractClass>();
            var duckVirtual = obscureObject.DuckCast<ObscureDuckTypeVirtualClass>();

            Assert.AreEqual(TaskStatus.RanToCompletion, duckInterface.Status);
            Assert.AreEqual(TaskStatus.RanToCompletion, duckAbstract.Status);
            Assert.AreEqual(TaskStatus.RanToCompletion, duckVirtual.Status);

            duckInterface.Status = TaskStatus.Running;

            Assert.AreEqual(TaskStatus.Running, duckInterface.Status);
            Assert.AreEqual(TaskStatus.Running, duckAbstract.Status);
            Assert.AreEqual(TaskStatus.Running, duckVirtual.Status);

            duckAbstract.Status = TaskStatus.Faulted;

            Assert.AreEqual(TaskStatus.Faulted, duckInterface.Status);
            Assert.AreEqual(TaskStatus.Faulted, duckAbstract.Status);
            Assert.AreEqual(TaskStatus.Faulted, duckVirtual.Status);

            duckVirtual.Status = TaskStatus.WaitingForActivation;

            Assert.AreEqual(TaskStatus.WaitingForActivation, duckInterface.Status);
            Assert.AreEqual(TaskStatus.WaitingForActivation, duckAbstract.Status);
            Assert.AreEqual(TaskStatus.WaitingForActivation, duckVirtual.Status);
        }

        [TestCaseSource(nameof(Data))]
        public void StructCopy(object obscureObject)
        {
            var duckStructCopy = obscureObject.DuckCast<ObscureDuckTypeStruct>();

            Assert.AreEqual(10, duckStructCopy.PublicStaticGetValueType);
            Assert.AreEqual(11, duckStructCopy.InternalStaticGetValueType);
            Assert.AreEqual(12, duckStructCopy.ProtectedStaticGetValueType);
            Assert.AreEqual(13, duckStructCopy.PrivateStaticGetValueType);

            Assert.AreEqual(20, duckStructCopy.PublicStaticGetSetValueType);
            Assert.AreEqual(21, duckStructCopy.InternalStaticGetSetValueType);
            Assert.AreEqual(22, duckStructCopy.ProtectedStaticGetSetValueType);
            Assert.AreEqual(23, duckStructCopy.PrivateStaticGetSetValueType);

            Assert.AreEqual(30, duckStructCopy.PublicGetValueType);
            Assert.AreEqual(31, duckStructCopy.InternalGetValueType);
            Assert.AreEqual(32, duckStructCopy.ProtectedGetValueType);
            Assert.AreEqual(33, duckStructCopy.PrivateGetValueType);

            Assert.AreEqual(40, duckStructCopy.PublicGetSetValueType);
            Assert.AreEqual(41, duckStructCopy.InternalGetSetValueType);
            Assert.AreEqual(42, duckStructCopy.ProtectedGetSetValueType);
            Assert.AreEqual(43, duckStructCopy.PrivateGetSetValueType);
        }

        [Test]
        public void StructDuckType()
        {
            ObscureObject.PublicStruct source = default;
            source.PublicGetSetValueType = 42;

            var dest = source.DuckCast<IStructDuckType>();
            Assert.AreEqual(source.PublicGetSetValueType, dest.PublicGetSetValueType);
        }
    }
}
