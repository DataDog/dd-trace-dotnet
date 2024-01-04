using System.Reflection.Emit;
using System.Reflection;
using System;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Datadog.Trace.Vendors.Newtonsoft.Json.Utilities;
using System.Text;
using Microsoft.Diagnostics.Tracing.Parsers.MicrosoftWindowsWPF;

namespace Benchmarks.Trace.Reflection
{
    [BenchmarkAgent7]
    public class ReflectionTests
    {
        static Func<T1, T2, TRes> GetFunc<T1, T2, TRes>(MethodInfo method)
        {
            Func<T1, T2, TRes> res = null;
            try
            {
                DynamicMethod dynMethod = new DynamicMethod(method.Name + "_dynMethod", typeof(TRes), new Type[] { typeof(T1), typeof(T2) }, method.DeclaringType, true);
                ILGenerator il = dynMethod.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_1);
                il.EmitCall(method.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, method, null);
                il.Emit(OpCodes.Ret);
                res = (Func<T1, T2, TRes>)dynMethod.CreateDelegate(typeof(Func<T1, T2, TRes>));
            }
            catch (Exception err)
            {
                Console.WriteLine(err.ToString());
            }
            if (res == null)
            {
                res = (arg1, arg2) =>
                {
                    if (method.IsStatic) { return (TRes)method.Invoke(null, new object[] { arg1, arg2 }); }
                    return (TRes)method.Invoke(arg1, new object[] { arg2 });
                };
            }
            return res;
        }

        static Func<T1, T2, TRes> GetFunc<T1, T2, TRes>(ConstructorInfo ctor)
        {
            Func<T1, T2, TRes> res = null;
            try
            {
                DynamicMethod dynMethod = new DynamicMethod(ctor.Name + "_dynMethod", typeof(TRes), new Type[] { typeof(T1), typeof(T2) }, ctor.DeclaringType, true);
                ILGenerator il = dynMethod.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Newobj, ctor);
                il.Emit(OpCodes.Ret);
                res = (Func<T1, T2, TRes>)dynMethod.CreateDelegate(typeof(Func<T1, T2, TRes>));
            }
            catch (Exception err)
            {
                Console.WriteLine(err.ToString());
            }
            if (res == null)
            {
                res = (arg1, arg2) =>
                {
                    return (TRes)ctor.Invoke(new object[] { arg1, arg2 });
                };
            }
            return res;
        }

        abstract public class MethodWrapper
        {
            public string Name { get; }
            public string ParamsSignature { get; }
            public string MethodSignature { get => Name + ParamsSignature; }
            public string AssemblyName { get; }

            bool initialized = false;
            string typeName = null;
            MethodInfo method = null;
            ConstructorInfo ctor = null;

            protected bool IsCtor => Name == ".ctor";
            protected MethodWrapper(string methodSignature, string assemblyName = null)
            {
                int typeIndex = methodSignature.IndexOf("::");
                if (typeIndex >= 0)
                {
                    this.typeName = methodSignature.Substring(0, typeIndex);
                    methodSignature = methodSignature.Substring(typeIndex + 2);
                }
                int paramsIndex = methodSignature.IndexOf('(');
                Name = methodSignature.Substring(0, paramsIndex);
                ParamsSignature = methodSignature.Substring(paramsIndex);
                AssemblyName = assemblyName;
            }

            protected MethodInfo ResolveMethod(object obj = null)
            {
                if (!initialized)
                {
                    if (typeName == null && obj == null) { throw new ArgumentNullException("obj"); }
                    Type t = typeName != null ? GetType(typeName) : obj.GetType();
                    if (t == null && obj != null) { t = obj.GetType(); }
                    SetMethod(t);
                }
                if (method == null)
                {
                    // Logger.Trace("Execute -> Method {0}::{1} not found on", typeName, MethodSignature);
                    throw new MissingMethodException(typeName, MethodSignature);
                }
                return method;
            }

            protected ConstructorInfo ResolveCtor()
            {
                if (!initialized)
                {
                    if (typeName == null) { throw new ArgumentNullException("obj"); }
                    Type t = GetType(typeName);
                    SetCtor(t);
                }
                if (ctor == null)
                {
                    // Logger.Trace("Execute -> Method {0}::{1} not found on", typeName, MethodSignature);
                    throw new MissingMethodException(typeName, MethodSignature);
                }
                return ctor;
            }

            private void SetMethod(Type t)
            {
                if (t != null)
                {
                    if (typeName == null) { typeName = t.ToString(); }
                    method = t.GetMethods().FirstOrDefault(m => IsMethod(m));
                    if (method == null)
                    {
                        method = t.GetMethods(BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).FirstOrDefault(m => IsMethod(m));
                    }
                }
                initialized = true;
            }
            private void SetCtor(Type t)
            {
                if (t != null)
                {
                    if (typeName == null) { typeName = t.ToString(); }
                    ctor = t.GetConstructors().FirstOrDefault(m => IsMethod(m));
                    if (ctor == null)
                    {
                        ctor = t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(m => IsMethod(m));
                    }
                }
                initialized = true;
            }

            private Type GetType(string typeName)
            {
                if (typeName == null) { return null; }

                if (AssemblyName == null)
                {
                    Type t = Type.GetType(typeName);
                    if (t != null) { return t; }

                    string assemblyName = typeName;
                    int pos = assemblyName.LastIndexOf(".");
                    while (pos >= 0)
                    {
                        assemblyName = assemblyName.Substring(0, pos);
                        var res = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.FullName.StartsWith(assemblyName)).Select(a => a.GetType(typeName)).FirstOrDefault(type => type != null);
                        if (res != null) { return res; }
                        pos = assemblyName.LastIndexOf(".");
                    }
                }
                else
                {
                    return AppDomain.CurrentDomain.GetAssemblies().Where(a => a.FullName.StartsWith(AssemblyName)).Select(a => a.GetType(typeName)).FirstOrDefault(type => type != null);
                }
                return null;
            }
            private bool IsMethod(MethodBase m)
            {
                if (m.Name != Name) { return false; }
                var parameters = m.GetParameters().Select(p => p.ParameterType.ToString()).ToArray();
                string signature = string.Format("({0})", string.Join(",", parameters));
                return signature == ParamsSignature;
            }
        }
        public class FuncWrapper<T1, T2, TRes> : MethodWrapper
        {
            Func<T1, T2, TRes> func = null;
            public FuncWrapper(string methodSignature) : base(methodSignature) { }

            public TRes Invoke(T1 arg1, T2 arg2)
            {
                if (func == null) 
                {
                    if (IsCtor) { func = GetFunc<T1, T2, TRes>(ResolveCtor()); }
                    else { func = GetFunc<T1, T2, TRes>(ResolveMethod(arg1)); }
                }

                return func.Invoke(arg1, arg2);
            }
        }


        class TestType
        {
            string name;
            int val;

            public TestType(string name, int val) 
            {
                this.name = name;
                this.val = val;
            }

            public static string Method1(string s1, int i2)
            {
                return s1 + i2.ToString();
            }

            public override string ToString()
            {
                return name + " - " + val.ToString();
            }
        }

        [Benchmark]
        public void PureReflection1()
        {
            var method = typeof(TestType).GetMethod("Method1", BindingFlags.Public | BindingFlags.Static);
            method.Invoke(null, new object[] { "Iteration: ", 10 });
        }

        MethodInfo _method = typeof(TestType).GetMethod("Method1", BindingFlags.Public | BindingFlags.Static);
        [Benchmark]
        public void PureReflection2()
        {
            _method.Invoke(null, new object[] { "Iteration: ", 10 });
        }

        FuncWrapper<string, int, string> _wrapper = new FuncWrapper<string, int, string>("Benchmarks.Trace.Reflection.ReflectionTests+TestType::Method1(System.String,System.Int32)");
        [Benchmark]
        public void FuncWrapperReflection()
        {
            _wrapper.Invoke("Iteration: ", 10);
        }

        [Benchmark]
        public void DirectCall()
        {
            TestType.Method1("Iteration: ", 10);
        }

        [Benchmark]
        public void DirectCtor()
        {
            var obj = new TestType("Iteration: ", 10);
        }

        [Benchmark]
        public void ActivatorCreateInstance()
        {
            var obj = Activator.CreateInstance(typeof(StringBuilder), new object[] { 10, 20 });
        }

        ConstructorInfo _ctor = typeof(StringBuilder).GetConstructor(new Type[] { typeof(string), typeof(int) });
        [Benchmark]
        public void ReflectionCtor()
        {
            _ctor.Invoke(new object[] { 10, 20 });
        }


        FuncWrapper<int, int, StringBuilder> _ctorWrapper = new FuncWrapper<int, int, StringBuilder>("System.Text.StringBuilder::.ctor(System.Int32,System.Int32)");
        [Benchmark]
        public void CtorWrapperReflection()
        {
            var obj = _ctorWrapper.Invoke(10, 20);
        }

    }
}
