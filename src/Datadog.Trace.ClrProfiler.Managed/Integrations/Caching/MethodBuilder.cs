using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Datadog.Trace.ClrProfiler.Emit;
using Sigil;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    internal class MethodBuilder<TDelegate>
    {
        private readonly Assembly _callingAssembly;
        private readonly int _mdToken;
        private readonly int _originalOpCodeValue;
        private readonly OpCodeValue _opCode;
        private readonly MethodBase _methodBase;
        private Type _concreteType;
        private string _concreteTypeName;
        private object _instance = null;
        private object[] _parameters = new object[0];

        private Type[] _genericTypeArguments = Type.EmptyTypes;

        private bool _requiresBestEffortMatching = false;
        private bool _instanceWasSpecified = false;

        private MethodBuilder(Assembly callingAssembly, int mdToken, int opCode)
        {
            _callingAssembly = callingAssembly;
            _mdToken = mdToken;
            _opCode = (OpCodeValue)opCode;
            _originalOpCodeValue = opCode;

            try
            {
                _methodBase = callingAssembly.ManifestModule.ResolveMethod(mdToken);
            }
            catch
            {
                _requiresBestEffortMatching = true;
            }
        }

        public static MethodBuilder<TDelegate> Start(Assembly callingAssembly, int mdToken, int opCode)
        {
            return new MethodBuilder<TDelegate>(callingAssembly, mdToken, opCode);
        }

        public MethodBuilder<TDelegate> WithConcreteType(Type type)
        {
            _concreteType = type;
            _concreteTypeName = type.FullName;
            return this;
        }

        public MethodBuilder<TDelegate> WithConcreteTypeName(string typeName)
        {
            var concreteType = _callingAssembly.GetType(typeName);
            return this.WithConcreteType(concreteType);
        }

        public MethodBuilder<TDelegate> WithParameters(params object[] parameters)
        {
            _parameters = parameters;
            return this;
        }

        public MethodBuilder<TDelegate> WithGenericTypeArguments(params Type[] genericTypeArguments)
        {
            _genericTypeArguments = genericTypeArguments;
            return this;
        }

        public MethodBuilder<TDelegate> WithInstance(object instance)
        {
            _instance = instance;
            _instanceWasSpecified = true;
            return this;
        }

        public TDelegate Build()
        {
            ValidateRequirements();

            MethodInfo methodInfo = null;

            if (!_requiresBestEffortMatching && _methodBase is MethodInfo info)
            {
                methodInfo = info;
            }
            else
            {
                methodInfo = TryFindMethod();
            }

            Type delegateType = typeof(TDelegate);
            Type[] delegateGenericArgs = delegateType.GenericTypeArguments;

            Type[] delegateParameterTypes;
            Type returnType;

            if (delegateType.Name.StartsWith("Func`"))
            {
                // last generic type argument is the return type
                int parameterCount = delegateGenericArgs.Length - 1;
                delegateParameterTypes = new Type[parameterCount];
                Array.Copy(delegateGenericArgs, delegateParameterTypes, parameterCount);

                returnType = delegateGenericArgs[parameterCount];
            }
            else if (delegateType.Name.StartsWith("Action`"))
            {
                delegateParameterTypes = delegateGenericArgs;
                returnType = typeof(void);
            }
            else
            {
                throw new Exception($"Only Func<> or Action<> are supported in {nameof(MethodBuilder)}.");
            }

            var dynamicMethod = Emit<TDelegate>.NewDynamicMethod(methodInfo.Name);

            if (methodInfo.IsGenericMethodDefinition || methodInfo.IsGenericMethod)
            {
                methodInfo = methodInfo.MakeGenericMethod(_genericTypeArguments);
            }

            Type[] effectiveParameterTypes;

            var reflectedParameterTypes =
                methodInfo.GetParameters().Select(p => p.ParameterType);

            if (methodInfo.IsStatic)
            {
                effectiveParameterTypes = reflectedParameterTypes.ToArray();
            }
            else
            {
                // for instance methods, insert object's type as first element in array
                effectiveParameterTypes = new[] { _concreteType }
                                         .Concat(reflectedParameterTypes)
                                         .ToArray();
            }

            // load each argument and cast or unbox as necessary
            for (ushort argumentIndex = 0; argumentIndex < delegateParameterTypes.Length; argumentIndex++)
            {
                Type delegateParameterType = delegateParameterTypes[argumentIndex];
                Type underlyingParameterType = effectiveParameterTypes[argumentIndex];

                dynamicMethod.LoadArgument(argumentIndex);

                if (underlyingParameterType.IsValueType && delegateParameterType == typeof(object))
                {
                    dynamicMethod.UnboxAny(underlyingParameterType);
                }
                else if (underlyingParameterType != delegateParameterType)
                {
                    dynamicMethod.CastClass(underlyingParameterType);
                }
            }

            if (_opCode == OpCodeValue.Call || methodInfo.IsStatic)
            {
                // non-virtual call (e.g. static method, or method override calling overriden implementation)
                dynamicMethod.Call(methodInfo);
            }
            else if (_opCode == OpCodeValue.Callvirt)
            {
                // Note: C# compiler uses CALLVIRT for non-virtual
                // instance methods to get the cheap null check
                dynamicMethod.CallVirtual(methodInfo);
            }
            else
            {
                throw new NotSupportedException($"OpCode {_originalOpCodeValue} not supported when calling a method.");
            }

            if (methodInfo.ReturnType.IsValueType && returnType == typeof(object))
            {
                dynamicMethod.Box(methodInfo.ReturnType);
            }
            else if (methodInfo.ReturnType != returnType)
            {
                dynamicMethod.CastClass(returnType);
            }

            dynamicMethod.Return();
            return dynamicMethod.CreateDelegate();
        }

        private void ValidateRequirements()
        {
            if (_concreteType == null)
            {
                throw new ArgumentException("There must be a concrete type specified.");
            }

            if (_methodBase == null)
            {
                return;
            }

            if (_methodBase.IsStatic)
            {
                if (_instanceWasSpecified)
                {
                    throw new ArgumentException("There should be no instance specified for static methods.");
                }
            }
            else
            {
                if (!_instanceWasSpecified)
                {
                    throw new ArgumentException("Instance must be specified for instance methods.");
                }
            }
        }

        private MethodInfo TryFindMethod()
        {
            var methods =
                _concreteType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                             .Where(
                                  mi => mi.MetadataToken == _methodBase.MetadataToken
                                     && mi.IsGenericMethodDefinition == _methodBase.IsGenericMethodDefinition
                                     && mi.IsGenericMethod == _methodBase.IsGenericMethod
                                     && mi.IsAbstract == _methodBase.IsAbstract
                                     && mi.ContainsGenericParameters == _methodBase.ContainsGenericParameters
                                     && mi.IsPrivate == _methodBase.IsPrivate
                                     && mi.IsPublic == _methodBase.IsPublic
                                     && mi.IsVirtual == _methodBase.IsVirtual
                                     && mi.Name == _methodBase.Name)
                             .ToArray();

            if (methods.Length > 1)
            {
                throw new ArgumentException($"Unable to safely resolve method {_methodBase.Name} for {_concreteTypeName}");
            }

            MethodInfo methodInfo = methods.SingleOrDefault();

            if (methodInfo == null)
            {
                throw new ArgumentException($"Unable to resolve method {_methodBase.Name} for {_concreteTypeName}");
            }

            return methodInfo;
        }
    }
}
