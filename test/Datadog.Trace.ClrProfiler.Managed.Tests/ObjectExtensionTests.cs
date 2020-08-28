using Datadog.Trace.ClrProfiler.Emit;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class ObjectExtensionTests
    {
        private enum SomeEnum
        {
            Zero = 0,
            One = 1,
            Two = 2
        }

        [Fact]
        public void GetProperty_WithDifferentType_ShouldNotAffectResult()
        {
            SomeBaseClass someAbstractInstance = new SomeClass();
            var expected = someAbstractInstance.SomeIntProperty;

            var someCast = (object)someAbstractInstance;

            var objectResult = someCast.GetProperty<object>("SomeIntProperty");
            var actualResult = someCast.GetProperty<int>("SomeIntProperty");

            Assert.Equal(expected, (int)objectResult.GetValueOrDefault());
            Assert.Equal(expected, actualResult.GetValueOrDefault());
        }

        [Fact]
        public void GetProperty_WithNoDirectInheritance_ShouldNotAffectResult()
        {
            var someInstance = new SomeClass();
            var expected = someInstance.SomeEnumProperty;

            var someCast = (object)someInstance;

            var intResult = someCast.GetProperty<int>("SomeEnumProperty");
            var actualResult = someCast.GetProperty<SomeEnum>("SomeEnumProperty");

            Assert.Equal((int)expected, intResult.GetValueOrDefault());
            Assert.Equal(expected, actualResult.GetValueOrDefault());
        }

        [Fact]
        public void GetField_WithDifferentType_ShouldNotAffectResult()
        {
            var someInstance = new SomeClass();
            var expected = someInstance.GetSomeIntField();

            var someCast = (object)someInstance;

            var objectResult = someCast.GetField<object>("someIntField");
            var actualResult = someCast.GetField<int>("someIntField");

            Assert.Equal(expected, (int)objectResult.GetValueOrDefault());
            Assert.Equal(expected, actualResult.GetValueOrDefault());
        }

        [Fact]
        public void CallMethod_OneTypeArg_WithDifferentReturnType_ShouldNotAffectResult()
        {
            var someInstance = new SomeClass();
            var expected = 42;

            var objectResult = someInstance.CallMethod<object>("GetTheAnswerToTheUniverse");
            var actualResult = someInstance.CallMethod<int>("GetTheAnswerToTheUniverse");

            Assert.Equal(expected, (int)objectResult.GetValueOrDefault());
            Assert.Equal(expected, actualResult.GetValueOrDefault());
        }

        [Fact]
        public void CallMethod_TwoTypeArgs_WithDifferentReturnType_ShouldNotAffectResult()
        {
            var someInstance = new SomeClass();
            var expected = 1;

            var objectResult = someInstance.CallMethod<int, object>("AddOne", 0);
            var actualResult = someInstance.CallMethod<int, int>("AddOne", 0);

            Assert.Equal(expected, (int)objectResult.GetValueOrDefault());
            Assert.Equal(expected, actualResult.GetValueOrDefault());
        }

        [Fact]
        public void CallVoidMethod_TwoTypeArgs_CallsCorrectOverload()
        {
            var someInstance = new SomeClass();
            var expectedObjectResult = "12";
            var expectedActualResult = "3";

            someInstance.CallVoidMethod<object, object>("Add", 1, 2);
            var objectResult = someInstance.LastAddResult;

            someInstance.CallVoidMethod<int, int>("Add", 1, 2);
            var actualResult = someInstance.LastAddResult;

            Assert.Equal(expectedObjectResult, objectResult);
            Assert.Equal(expectedActualResult, actualResult);
        }

        private class SomeClass : SomeBaseClass
        {
            private readonly int someIntField = 305;

            public override int SomeIntProperty { get; } = 205;

            public SomeEnum SomeEnumProperty { get; } = SomeEnum.Two;

            public string LastAddResult { get; internal set; } = string.Empty;

            public int GetSomeIntField()
            {
                return someIntField;
            }

            public int GetTheAnswerToTheUniverse()
            {
                return 42;
            }

            public int AddOne(int value)
            {
                return value + 1;
            }

            public void Add(object arg1, object arg2)
            {
                LastAddResult = arg1.ToString() + arg2.ToString();
            }

            public void Add(int arg1, int arg2)
            {
                LastAddResult = (arg1 + arg2).ToString();
            }
        }

        private abstract class SomeBaseClass
        {
            public abstract int SomeIntProperty { get; }
        }
    }
}
