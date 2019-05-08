using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class MemberAccessorTests
    {
        public const string Name = "Smith";

        public const int Age = 30;

        public Person TestPerson { get; } = new Person(Name, Age);

        [Fact]
        public void TryCallMethod_Class()
        {
            var success = TestPerson.TryCallMethod("GetString", "test", out string result);

            Assert.True(success);
            Assert.Equal("result: test", result);
        }

        [Fact]
        public void TryCallMethod_Struct()
        {
            var success = TestPerson.TryCallMethod("GetInt32", 5, out int result);

            Assert.True(success);
            Assert.Equal(6, result);
        }

        [Fact]
        public void TryGetPropertyValue_Class()
        {
            var success = TestPerson.TryGetPropertyValue("Name", out string result);

            Assert.True(success);
            Assert.Equal(Name, result);
        }

        [Fact]
        public void TryGetPropertyValue_Struct()
        {
            var success = TestPerson.TryGetPropertyValue("Age", out int result);

            Assert.True(success);
            Assert.Equal(Age, result);
        }

        [Fact]
        public void TryGetFieldValue_Class()
        {
            var success = TestPerson.TryGetFieldValue("_name", out string result);

            Assert.True(success);
            Assert.Equal(Name, result);
        }

        [Fact]
        public void TryGetFieldValue_Struct()
        {
            var success = TestPerson.TryGetFieldValue("_age", out int result);

            Assert.True(success);
            Assert.Equal(Age, result);
        }

        public class Person
        {
            private readonly string _name;
            private readonly int _age;

            public Person(string name, int age)
            {
                _name = name;
                _age = age;
            }

            public string Name => _name;

            public int Age => _age;

            public string GetString(string value) => $"result: {value}";

            public int GetInt32(int value) => value + 1;
        }
    }
}
