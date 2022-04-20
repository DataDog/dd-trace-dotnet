using System.Threading.Tasks;
using Datadog.Trace.Annotations;

namespace Samples.TraceAnnotations
{
    internal class TestType
    {
        static TestType() { }
        public TestType() { }
        public string Name { get; [Trace(OperationName = "overridden.attribute")] set; }
        public override string ToString() => Name;
        public override int GetHashCode()
        {
            return (Name ?? "").GetHashCode();
        }
        public override bool Equals(object obj)
        {
            // If this and obj do not refer to the same type, then they are not equal.
            if (obj.GetType() != this.GetType()) return false;

            // Return true if  x and y fields match.
            var other = (TestType)obj;
            return this.Name == other.Name;
        }
        ~TestType()
        {
            // Finalizer code
        }
        public void Finalize(int someInt)
        {
            // Non-finalizer code
        }

        [Trace(ResourceName = "TestType_VoidMethod")]
        public void VoidMethod(string arg1, int arg2, object arg3) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3) => 42;
        public string ReturnReferenceMethod(string arg, int arg21, object arg3) => "Hello World";
        public string ReturnNullMethod(string arg, int arg21, object arg3) => null;
        public T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3) => default;
        public Task ReturnTaskMethod(string arg1, int arg2, object arg3) => Task.Delay(100);
        public Task<bool> ReturnTaskTMethod(string arg1, int arg2, object arg3) => Task.FromResult<bool>(true);
        public ValueTask ReturnValueTaskMethod(string arg1, int arg2, object arg3) => new ValueTask(Task.Delay(100));
        public ValueTask<bool> ReturnValueTaskTMethod(string arg1, int arg2, object arg3) => new ValueTask<bool>(true);

        [Trace(OperationName = "overridden.attribute", ResourceName = "TestType_ReturnGenericMethodAttribute")]
        public T ReturnGenericMethodAttribute<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3) => default;
    }
    class TestTypeGeneric<T>
    {
        static TestTypeGeneric() { }
        public TestTypeGeneric() { }
        public string Name { get; [Trace(OperationName = "overridden.attribute")] set; }
        public override string ToString() => Name;
        public override int GetHashCode()
        {
            return (Name ?? "").GetHashCode();
        }
        public override bool Equals(object obj)
        {
            // If this and obj do not refer to the same type, then they are not equal.
            if (obj.GetType() != this.GetType()) return false;

            // Return true if  x and y fields match.
            var other = (TestTypeGeneric<T>)obj;
            return this.Name == other.Name;
        }
        ~TestTypeGeneric()
        {
            // Finalizer code
        }
        public void Finalize(int someInt)
        {
            // Non-finalizer code
        }

        [Trace(ResourceName = "TestTypeGeneric_VoidMethod")]
        public void VoidMethod(string arg1, int arg2, object arg3) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3) => "Hello World";
        public string ReturnNullMethod(string arg, int arg21, object arg3) => null;
        public T ReturnGenericMethod<TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3) => default;
        public Task ReturnTaskMethod(string arg1, int arg2, object arg3) => Task.Delay(100);
        public Task<bool> ReturnTaskTMethod(string arg1, int arg2, object arg3) => Task.FromResult<bool>(true);
        public ValueTask ReturnValueTaskMethod(string arg1, int arg2, object arg3) => new ValueTask(Task.Delay(100));
        public ValueTask<bool> ReturnValueTaskTMethod(string arg1, int arg2, object arg3) => new ValueTask<bool>(true);

        [Trace(OperationName = "overridden.attribute", ResourceName = "TestTypeGeneric_ReturnGenericMethodAttribute")]
        public T ReturnGenericMethodAttribute<TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3) => default;
    }
    struct TestTypeStruct
    {
        private string _name;
        static TestTypeStruct() { }
        public TestTypeStruct() { _name = null; }
        public string Name { get => _name; [Trace(OperationName = "overridden.attribute")] set => _name = value; }
        public override string ToString() => Name;
        public override int GetHashCode()
        {
            return (Name ?? "").GetHashCode();
        }
        public override bool Equals(object obj)
        {
            // If this and obj do not refer to the same type, then they are not equal.
            if (obj.GetType() != this.GetType()) return false;

            // Return true if  x and y fields match.
            var other = (TestTypeStruct)obj;
            return this.Name == other.Name;
        }

        [Trace(ResourceName = "TestTypeStruct_VoidMethod")]
        public void VoidMethod(string arg1, int arg2, object arg3) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3) => "Hello World";
        public string ReturnNullMethod(string arg, int arg21, object arg3) => null;
        public T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3) => default;
        public Task ReturnTaskMethod(string arg1, int arg2, object arg3) => Task.Delay(100);
        public Task<bool> ReturnTaskTMethod(string arg1, int arg2, object arg3) => Task.FromResult<bool>(true);
        public ValueTask ReturnValueTaskMethod(string arg1, int arg2, object arg3) => new ValueTask(Task.Delay(100));
        public ValueTask<bool> ReturnValueTaskTMethod(string arg1, int arg2, object arg3) => new ValueTask<bool>(true);

        [Trace(OperationName = "overridden.attribute", ResourceName = "TestTypeStruct_ReturnGenericMethodAttribute")]
        public T ReturnGenericMethodAttribute<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3) => default;
    }
    static class TestTypeStatic
    {
        static TestTypeStatic() { }
        public static string Name { get; [Trace(OperationName = "overridden.attribute")] set; }

        [Trace(ResourceName = "TestTypeStatic_VoidMethod")]
        public static void VoidMethod(string arg1, int arg2, object arg3) { }
        public static int ReturnValueMethod(string arg1, int arg2, object arg3) => 42;
        public static string ReturnReferenceMethod(string arg1, int arg2, object arg3) => "Hello World";
        public static string ReturnNullMethod(string arg, int arg21, object arg3) => null;
        public static T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3) => default;
        public static Task ReturnTaskMethod(string arg1, int arg2, object arg3) => Task.Delay(100);
        public static Task<bool> ReturnTaskTMethod(string arg1, int arg2, object arg3) => Task.FromResult<bool>(true);
        public static ValueTask ReturnValueTaskMethod(string arg1, int arg2, object arg3) => new ValueTask(Task.Delay(100));
        public static ValueTask<bool> ReturnValueTaskTMethod(string arg1, int arg2, object arg3) => new ValueTask<bool>(true);

        [Trace(OperationName = "overridden.attribute", ResourceName = "TestTypeStatic_ReturnGenericMethodAttribute")]
        public static T ReturnGenericMethodAttribute<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3) => default;
    }

    static class AttributeOnlyStatic
    {
        [Trace(ResourceName = "UninstrumentedType_ReturnTaskTMethod")]
        public static Task<bool> ReturnTaskTMethod(string arg1, int arg2, object arg3) => Task.FromResult<bool>(true);
    }
}
