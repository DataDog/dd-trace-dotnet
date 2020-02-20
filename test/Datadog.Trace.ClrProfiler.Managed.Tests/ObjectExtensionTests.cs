using Datadog.Trace.ClrProfiler.Emit;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class ObjectExtensionTests
    {
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

        private class SomeClass : SomeBaseClass
        {
            private readonly int someIntField = 305;

            public override int SomeIntProperty { get; } = 205;

            public int GetSomeIntField()
            {
                return someIntField;
            }
        }

        private abstract class SomeBaseClass
        {
            public abstract int SomeIntProperty { get; }
        }
    }
}
