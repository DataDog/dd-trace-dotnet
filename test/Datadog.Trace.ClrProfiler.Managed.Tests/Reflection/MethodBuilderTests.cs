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
        private readonly Assembly _thisAssembly = Assembly.GetExecutingAssembly();
        private readonly Type _testType = typeof(ObscenelyAnnoyingClass);

        [Fact]
        public void NoParameters_ProperlyCalled()
        {
            var instance = new ObscenelyAnnoyingClass();
            var expected = MethodReference.Get(() => instance.Method());
            var methodResult = Build<Action<object>>(expected.Name).Build();
            methodResult.Invoke(instance);
            Assert.Equal(expected: expected.MetadataToken, instance.LastCall.MetadataToken);
        }

        [Fact]
        public void IntParameter_ProperlyCalled()
        {
            var instance = new ObscenelyAnnoyingClass();
            int parameter = 1;
            var expected = MethodReference.Get(() => instance.Method(parameter));
            var methodResult = Build<Action<object, int>>(expected.Name).WithParameters(parameter).Build();
            methodResult.Invoke(instance, parameter);
            Assert.Equal(expected: expected.MetadataToken, instance.LastCall.MetadataToken);
        }

        [Fact]
        public void LongParameter_ProperlyCalled()
        {
            var instance = new ObscenelyAnnoyingClass();
            long parameter = 1;
            var expected = MethodReference.Get(() => instance.Method(parameter));
            var methodResult = Build<Action<object, long>>(expected.Name).WithParameters(parameter).Build();
            methodResult.Invoke(instance, parameter);
            Assert.Equal(expected: expected.MetadataToken, instance.LastCall.MetadataToken);
        }

        [Fact]
        public void ShortParameter_ProperlyCalled()
        {
            var instance = new ObscenelyAnnoyingClass();
            short parameter = 1;
            var expected = MethodReference.Get(() => instance.Method(parameter));
            var methodResult = Build<Action<object, short>>(expected.Name).WithParameters(parameter).Build();
            methodResult.Invoke(instance, parameter);
            Assert.Equal(expected: expected.MetadataToken, instance.LastCall.MetadataToken);
        }

        [Fact]
        public void ObjectParameter_ProperlyCalled()
        {
            var instance = new ObscenelyAnnoyingClass();
            object parameter = new object();
            var expected = MethodReference.Get(() => instance.Method(parameter));
            var methodResult = Build<Action<object, object>>(expected.Name).WithParameters(parameter).Build();
            methodResult.Invoke(instance, parameter);
            Assert.Equal(expected: expected.MetadataToken, instance.LastCall.MetadataToken);
        }

        [Fact]
        public void StringParameter_ProperlyCalled()
        {
            var instance = new ObscenelyAnnoyingClass();
            string parameter = string.Empty;
            var expected = MethodReference.Get(() => instance.Method(parameter));
            var methodResult = Build<Action<object, string>>(expected.Name).WithParameters(parameter).Build();
            methodResult.Invoke(instance, parameter);
            Assert.Equal(expected: expected.MetadataToken, instance.LastCall.MetadataToken);
        }

        [Fact]
        public void StringParameterAsObject_ProperlyCalls_ObjectMethod()
        {
            var instance = new ObscenelyAnnoyingClass();
            object parameter = string.Empty;
            var expected = MethodReference.Get(() => instance.Method(parameter));
            var methodResult = Build<Action<object, object>>(expected.Name).WithParameters(parameter).Build();
            methodResult.Invoke(instance, parameter);
            Assert.Equal(expected: expected.MetadataToken, instance.LastCall.MetadataToken);
        }

        [Fact]
        public void GenericParameter_ProperlyCalls_GenericMethod()
        {
            var instance = new ObscenelyAnnoyingGenericClass<ClassB>();
            var parameter = new ClassB();
            var expected = MethodReference.Get(() => instance.Method(parameter));
            var methodResult = Build<Action<object, object>>(expected.Name, overrideType: instance.GetType()).WithParameters(parameter).Build();
            methodResult.Invoke(instance, parameter);
            Assert.Equal(expected: expected.MetadataToken, instance.LastCall.MetadataToken);
        }

        private MethodBuilder<T> Build<T>(string methodName, Type overrideType = null)
        {
            return MethodBuilder<T>
                  .Start(_thisAssembly, 0, (int)OpCodeValue.Callvirt, methodName)
                  .WithConcreteType(overrideType ?? _testType);
        }
    }
}
