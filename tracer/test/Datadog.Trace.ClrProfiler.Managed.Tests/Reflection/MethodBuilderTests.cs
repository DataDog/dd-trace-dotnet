// <copyright file="MethodBuilderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using Datadog.Trace.ClrProfiler.Emit;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    /// <summary>
    /// All delegates we generate for instance methods include the instance as the first parameter
    /// </summary>
    public class MethodBuilderTests
    {
        private readonly Guid _moduleVersionId = Assembly.GetExecutingAssembly().ManifestModule.ModuleVersionId;
        private readonly Type _testType = typeof(ObscenelyAnnoyingClass);

        [Fact]
        public void AmbiguousParameters_ClassASystemObjectClassA_CallsExpectedMethod()
        {
            var instance = new ObscenelyAnnoyingClass();
            var p1 = new ClassA();
            var p2 = new object();
            var p3 = new ClassA();
            var expected = MethodReference.Get(() => instance.Method(p1, p2, p3));
            var methodResult = Build<Action<object, object, object, object>>(expected.Name).WithParameters(p1, p2, p3).Build();
            methodResult(instance, p1, p2, p3);
            Assert.Equal(expected: expected.ToString(), instance.LastCall.MethodString);
        }

        [Fact]
        public void AmbiguousParameters_ClassAClassAClassA_CallsExpectedMethod()
        {
            var instance = new ObscenelyAnnoyingClass();
            var p1 = new ClassA();
            var p2 = new ClassA();
            var p3 = new ClassA();
            var expected = MethodReference.Get(() => instance.Method(p1, p2, p3));
            var methodResult = Build<Action<object, object, object, object>>(expected.Name).WithParameters(p1, p2, p3).Build();
            methodResult(instance, p1, p2, p3);
            Assert.Equal(expected: expected.ToString(), instance.LastCall.MethodString);
        }

        [Fact]
        public void AmbiguousParameters_ClassAClassBClassB_CallsExpectedMethod()
        {
            var instance = new ObscenelyAnnoyingClass();
            var p1 = new ClassA();
            var p2 = new ClassB();
            var p3 = new ClassB();
            var expected = MethodReference.Get(() => instance.Method(p1, p2, p3));
            var methodResult = Build<Action<object, object, object, object>>(expected.Name).WithParameters(p1, p2, p3).Build();
            methodResult(instance, p1, p2, p3);
            Assert.Equal(expected: expected.ToString(), instance.LastCall.MethodString);
        }

        [Fact]
        public void AmbiguousParameters_ClassBClassCClassC_CallsExpectedMethod()
        {
            var instance = new ObscenelyAnnoyingClass();
            var p1 = new ClassB();
            var p2 = new ClassC();
            var p3 = new ClassC();
            var expected = MethodReference.Get(() => instance.Method(p1, p2, p3));
            var methodResult = Build<Action<object, object, object, object>>(expected.Name).WithParameters(p1, p2, p3).Build();
            methodResult(instance, p1, p2, p3);
            Assert.Equal(expected: expected.ToString(), instance.LastCall.MethodString);
        }

        [Fact]
        public void AmbiguousParameters_ClassCClassCClassC_CallsExpectedMethod_WithNamespaceNameFilter()
        {
            var instance = new ObscenelyAnnoyingClass();
            var p1 = new ClassC();
            var p2 = new ClassC();
            var p3 = new ClassC();
            var expected = MethodReference.Get(() => instance.Method(p1, p2, p3));

            var methodResult =
                Build<Action<object, object, object, object>>(expected.Name)
                .WithParameters(p1, p2, p3)
                .WithNamespaceAndNameFilters(
                    ClrNames.Void,
                    "Datadog.Trace.ClrProfiler.Managed.Tests.ClassB",
                    "Datadog.Trace.ClrProfiler.Managed.Tests.ClassC",
                    "Datadog.Trace.ClrProfiler.Managed.Tests.ClassC")
                .Build();

            methodResult(instance, p1, p2, p3);
            Assert.Equal(expected: expected.ToString(), instance.LastCall.MethodString);
        }

        [Fact]
        public void AmbiguousParameters_ClassCClassBClassA_CallsExpectedMethod_WithNamespaceNameFilter()
        {
            var instance = new ObscenelyAnnoyingClass();
            var p1 = new ClassC();
            var p2 = new ClassB();
            var p3 = new ClassA();
            var expected = MethodReference.Get(() => instance.Method(p1, p2, p3));

            var methodResult =
                Build<Action<object, object, object, object>>(expected.Name)
                .WithParameters(p1, p2, p3)
                .WithNamespaceAndNameFilters(
                    ClrNames.Void,
                    "Datadog.Trace.ClrProfiler.Managed.Tests.ClassA",
                    "Datadog.Trace.ClrProfiler.Managed.Tests.ClassA",
                    "Datadog.Trace.ClrProfiler.Managed.Tests.ClassA")
                .Build();

            methodResult(instance, p1, p2, p3);
            Assert.Equal(expected: expected.ToString(), instance.LastCall.MethodString);
        }

        [Fact]
        public void NoParameters_ProperlyCalled()
        {
            var instance = new ObscenelyAnnoyingClass();
            var expected = MethodReference.Get(() => instance.Method());
            var methodResult = Build<Action<object>>(expected.Name).Build();
            methodResult(instance);
            Assert.Equal(expected: expected.ToString(), instance.LastCall.MethodString);
        }

        [Fact]
        public void IntParameter_ProperlyCalled()
        {
            var instance = new ObscenelyAnnoyingClass();
            int parameter = 1;
            var expected = MethodReference.Get(() => instance.Method(parameter));
            var methodResult = Build<Action<object, int>>(expected.Name).WithParameters(parameter).Build();
            methodResult(instance, parameter);
            Assert.Equal(expected: expected.ToString(), instance.LastCall.MethodString);
        }

        [Fact]
        public void LongParameter_ProperlyCalled()
        {
            var instance = new ObscenelyAnnoyingClass();
            long parameter = 1;
            var expected = MethodReference.Get(() => instance.Method(parameter));
            var methodResult = Build<Action<object, long>>(expected.Name).WithParameters(parameter).Build();
            methodResult(instance, parameter);
            Assert.Equal(expected: expected.ToString(), instance.LastCall.MethodString);
        }

        [Fact]
        public void ShortParameter_ProperlyCalled()
        {
            var instance = new ObscenelyAnnoyingClass();
            short parameter = 1;
            var expected = MethodReference.Get(() => instance.Method(parameter));
            var methodResult = Build<Action<object, short>>(expected.Name).WithParameters(parameter).Build();
            methodResult(instance, parameter);
            Assert.Equal(expected: expected.ToString(), instance.LastCall.MethodString);
        }

        [Fact]
        public void ObjectParameter_ProperlyCalled()
        {
            var instance = new ObscenelyAnnoyingClass();
            object parameter = new object();
            var expected = MethodReference.Get(() => instance.Method(parameter));
            var methodResult = Build<Action<object, object>>(expected.Name).WithParameters(parameter).Build();
            methodResult(instance, parameter);
            Assert.Equal(expected: expected.ToString(), instance.LastCall.MethodString);
        }

        [Fact]
        public void StringParameter_ProperlyCalled()
        {
            var instance = new ObscenelyAnnoyingClass();
            string parameter = string.Empty;
            var expected = MethodReference.Get(() => instance.Method(parameter));
            var methodResult = Build<Action<object, string>>(expected.Name).WithParameters(parameter).Build();
            methodResult(instance, parameter);
            Assert.Equal(expected: expected.ToString(), instance.LastCall.MethodString);
        }

        [Fact]
        public void StringParameterAsObject_ProperlyCalls_ObjectMethod_WithNamespaceNameFilter()
        {
            var instance = new ObscenelyAnnoyingClass();
            object parameter = string.Empty;
            var expected = MethodReference.Get(() => instance.Method(parameter));
            var methodResult =
                Build<Action<object, object>>(expected.Name)
                .WithParameters(parameter)
                .WithNamespaceAndNameFilters(ClrNames.Void, ClrNames.Object)
                .Build();
            methodResult(instance, parameter);
            Assert.Equal(expected: expected.ToString(), instance.LastCall.MethodString);
        }

        [Fact]
        public void DeclaringTypeGenericParameter_ProperlyCalls_ClosedGenericMethod()
        {
            var instance = new ObscenelyAnnoyingGenericClass<ClassA>();
            var parameter = new ClassA();
            var expected = MethodReference.Get(() => instance.Method(parameter));
            var methodResult = Build<Action<object, object>>(expected.Name, overrideType: instance.GetType()).WithParameters(parameter).Build();
            methodResult(instance, parameter);
            Assert.Equal(expected: expected.MetadataToken, instance.LastCall.MetadataToken);
        }

        [Fact]
        public void DeclaringTypeGenericParameter_WithOpenGenericMethod_ProperlyCalls_OpenGenericMethod()
        {
            var instance = new ObscenelyAnnoyingGenericClass<ClassA>();
            var parameter = new ClassA();
            var expected = MethodReference.Get(() => instance.Method<int>(parameter));
            var methodResult =
                Build<Action<object, object>>(expected.Name, overrideType: instance.GetType())
                   .WithParameters(parameter)
                   .WithMethodGenerics(typeof(int))
                   .Build();
            methodResult(instance, parameter);
            Assert.Equal(expected: expected.MetadataToken, instance.LastCall.MetadataToken);
        }

        [Fact]
        public void DeclaringTypeGenericTypeParam_ThenMethodGenericParam_ProperlyCalls_Method()
        {
            var instance = new ObscenelyAnnoyingGenericClass<ClassA>();
            var parameter1 = new ClassA();
            int parameter2 = 1;
            var expected = MethodReference.Get(() => instance.Method<int>(parameter1, parameter2));
            var methodResult =
                Build<Action<object, object, int>>(expected.Name, overrideType: instance.GetType())
                   .WithParameters(parameter1, parameter2)
                   .WithMethodGenerics(typeof(int))
                   .Build();
            methodResult(instance, parameter1, parameter2);
            Assert.Equal(expected: expected.MetadataToken, instance.LastCall.MetadataToken);
        }

        [Fact]
        public void WrongMetadataToken_NonSpecificDelegateSignature_GetsCorrectMethodAnyways()
        {
            var instance = new ObscenelyAnnoyingClass();
            var wrongMethod = MethodReference.Get(() => instance.Method(1));

            string parameter = string.Empty;
            var expected = MethodReference.Get(() => instance.Method(parameter));

            var methodResult = MethodBuilder<Action<object, object>> // Proper use should be Action<object, string>
                              .Start(_moduleVersionId, wrongMethod.MetadataToken, (int)OpCodeValue.Callvirt, "Method")
                              .WithConcreteType(_testType)
                              .WithParameters(parameter) // The parameter is the saving grace
                              .Build();

            methodResult(instance, parameter);
            Assert.Equal(expected: expected.ToString(), instance.LastCall.MethodString);
        }

        [Fact]
        public void TargetReturnsValueType_DelegateReturnsSameValueType_IsOk()
        {
            var instance = new ObscenelyAnnoyingClass();
            int arg = 42;

            var methodToInvoke = MethodReference.Get(() => instance.ReturnInputInt(arg));
            var methodCall = MethodBuilder<Func<object, int, int>>
                                .Start(_moduleVersionId, methodToInvoke.MetadataToken, (int)OpCodeValue.Callvirt, "ReturnInputInt")
                                .WithConcreteType(typeof(ObscenelyAnnoyingClass))
                                .WithParameters(arg)
                                .Build();

            var actual = methodCall(instance, arg);
            Assert.Equal(arg, actual);
        }

        [Fact]
        public void TargetReturnsValueType_DelegateReturnsObject_IsOk()
        {
            var instance = new ObscenelyAnnoyingClass();
            int arg = 42;

            var methodToInvoke = MethodReference.Get(() => instance.ReturnInputInt(arg));
            var methodCall = MethodBuilder<Func<object, int, object>>
                                .Start(_moduleVersionId, methodToInvoke.MetadataToken, (int)OpCodeValue.Callvirt, "ReturnInputInt")
                                .WithConcreteType(typeof(ObscenelyAnnoyingClass))
                                .WithParameters(arg)
                                .Build();

            var actual = methodCall(instance, arg);
            Assert.Equal(arg, actual);
        }

        [Fact]
        public void TargetReturnsValueType_DelegateReturnsDifferentValueType_ThrowsException()
        {
            var instance = new ObscenelyAnnoyingClass();
            int arg = 42;

            var methodToInvoke = MethodReference.Get(() => instance.ReturnInputInt(arg));

            // Throws
            Assert.ThrowsAny<Exception>(() =>
                MethodBuilder<Func<object, int, double>>
                                .Start(_moduleVersionId, methodToInvoke.MetadataToken, (int)OpCodeValue.Callvirt, "ReturnInputInt")
                                .WithConcreteType(typeof(ObscenelyAnnoyingClass))
                                .WithParameters(arg)
                                .Build());
        }

        [Fact]
        public void TargetReturnsObject_DelegateReturnsObject_IsOk()
        {
            var instance = new ObscenelyAnnoyingClass();
            object arg = new ClassA();

            var methodToInvoke = MethodReference.Get(() => instance.ReturnInputObject(arg));
            var methodCall = MethodBuilder<Func<object, object, object>>
                                .Start(_moduleVersionId, methodToInvoke.MetadataToken, (int)OpCodeValue.Callvirt, "ReturnInputObject")
                                .WithConcreteType(typeof(ObscenelyAnnoyingClass))
                                .WithParameters(arg)
                                .Build();

            var actual = methodCall(instance, arg);
            Assert.Equal(arg, actual);
        }

        [Fact]
        public void TargetReturnsReferenceType_DelegateReturnsObject_IsOk()
        {
            var instance = new ObscenelyAnnoyingClass();
            ClassA arg = new ClassA();

            var methodToInvoke = MethodReference.Get(() => instance.ReturnInputClassA(arg));
            var methodCall = MethodBuilder<Func<object, object, object>>
                                .Start(_moduleVersionId, methodToInvoke.MetadataToken, (int)OpCodeValue.Callvirt, "ReturnInputClassA")
                                .WithConcreteType(typeof(ObscenelyAnnoyingClass))
                                .WithParameters(arg)
                                .Build();

            var actual = methodCall(instance, arg);
            Assert.Equal(arg, actual);
        }

        [Fact]
        public void TargetReturnsReferenceType_DelegateReturnsDifferentReferenceType_IsOk()
        {
            var instance = new ObscenelyAnnoyingClass();
            ClassB arg = new ClassB();

            var methodToInvoke = MethodReference.Get(() => instance.ReturnInputClassA(arg));
            var methodCall = MethodBuilder<Func<object, object, ClassB>>
                                .Start(_moduleVersionId, methodToInvoke.MetadataToken, (int)OpCodeValue.Callvirt, "ReturnInputClassA")
                                .WithConcreteType(typeof(ObscenelyAnnoyingClass))
                                .WithParameters(arg)
                                .Build();

            var actual = methodCall(instance, arg);
            Assert.Equal(arg, actual);
        }

        [Fact]
        public void TargetReturnsReferenceType_DelegateReturnsValueType_ThrowsException()
        {
            var instance = new ObscenelyAnnoyingClass();
            object arg = new ClassA();

            var methodToInvoke = MethodReference.Get(() => instance.ReturnInputObject(arg));

            // Throws
            Assert.ThrowsAny<Exception>(() =>
                MethodBuilder<Func<object, object, int>>
                                .Start(_moduleVersionId, methodToInvoke.MetadataToken, (int)OpCodeValue.Callvirt, "ReturnInputObject")
                                .WithConcreteType(typeof(ObscenelyAnnoyingClass))
                                .WithParameters(arg)
                                .Build());
        }

        private MethodBuilder<T> Build<T>(string methodName, Type overrideType = null)
            where T : Delegate
        {
            return MethodBuilder<T>
                  .Start(_moduleVersionId, 0, (int)OpCodeValue.Callvirt, methodName)
                  .WithConcreteType(overrideType ?? _testType);
        }
    }
}
