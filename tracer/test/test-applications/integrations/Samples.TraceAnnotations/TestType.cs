using System.Threading.Tasks;

namespace Samples.TraceAnnotations
{
    internal class TestType
    {
        static TestType() { }
        public TestType() { }
        public string Name { get; set; }
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

        public void VoidMethod(string arg1, int arg2, object arg3) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3) => 42;
        public string ReturnReferenceMethod(string arg, int arg21, object arg3) => "Hello World";
        public string ReturnNullMethod(string arg, int arg21, object arg3) => null;
        public T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3) => default;
        public Task ReturnTaskMethod(string arg1, int arg2, object arg3) => Task.Delay(100);
        public Task<bool> ReturnTaskTMethod(string arg1, int arg2, object arg3) => Task.FromResult<bool>(true);
        public ValueTask ReturnValueTaskMethod(string arg1, int arg2, object arg3) => new ValueTask(Task.Delay(100));
        public ValueTask<bool> ReturnValueTaskTMethod(string arg1, int arg2, object arg3) => new ValueTask<bool>(true);
    }
    class TestTypeGeneric<T>
    {
        static TestTypeGeneric() { }
        public TestTypeGeneric() { }
        public string Name { get; set; }
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

        public void VoidMethod(string arg1, int arg2, object arg3) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3) => "Hello World";
        public string ReturnNullMethod(string arg, int arg21, object arg3) => null;
        public T ReturnGenericMethod<TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3) => default;
        public Task ReturnTaskMethod(string arg1, int arg2, object arg3) => Task.Delay(100);
        public Task<bool> ReturnTaskTMethod(string arg1, int arg2, object arg3) => Task.FromResult<bool>(true);
        public ValueTask ReturnValueTaskMethod(string arg1, int arg2, object arg3) => new ValueTask(Task.Delay(100));
        public ValueTask<bool> ReturnValueTaskTMethod(string arg1, int arg2, object arg3) => new ValueTask<bool>(true);
    }
    struct TestTypeStruct
    {
        static TestTypeStruct() { }
        public TestTypeStruct() { Name = null; }
        public string Name { get; set; }
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

        public void VoidMethod(string arg1, int arg2, object arg3) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3) => "Hello World";
        public string ReturnNullMethod(string arg, int arg21, object arg3) => null;
        public T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3) => default;
        public Task ReturnTaskMethod(string arg1, int arg2, object arg3) => Task.Delay(100);
        public Task<bool> ReturnTaskTMethod(string arg1, int arg2, object arg3) => Task.FromResult<bool>(true);
        public ValueTask ReturnValueTaskMethod(string arg1, int arg2, object arg3) => new ValueTask(Task.Delay(100));
        public ValueTask<bool> ReturnValueTaskTMethod(string arg1, int arg2, object arg3) => new ValueTask<bool>(true);
    }
    static class TestTypeStatic
    {
        static TestTypeStatic() { }
        public static string Name { get; set; }

        public static void VoidMethod(string arg1, int arg2, object arg3) { }
        public static int ReturnValueMethod(string arg1, int arg2, object arg3) => 42;
        public static string ReturnReferenceMethod(string arg1, int arg2, object arg3) => "Hello World";
        public static string ReturnNullMethod(string arg, int arg21, object arg3) => null;
        public static T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3) => default;
        public static Task ReturnTaskMethod(string arg1, int arg2, object arg3) => Task.Delay(100);
        public static Task<bool> ReturnTaskTMethod(string arg1, int arg2, object arg3) => Task.FromResult<bool>(true);
        public static ValueTask ReturnValueTaskMethod(string arg1, int arg2, object arg3) => new ValueTask(Task.Delay(100));
        public static ValueTask<bool> ReturnValueTaskTMethod(string arg1, int arg2, object arg3) => new ValueTask<bool>(true);
    }
}
