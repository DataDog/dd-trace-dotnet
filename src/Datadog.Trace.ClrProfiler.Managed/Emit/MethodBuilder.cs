using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Sigil;

namespace Datadog.Trace.ClrProfiler.Emit
{
    internal class MethodBuilder<TDelegate>
    {
        /// <summary>
        /// Global dictionary for caching reflected delegates
        /// </summary>
        private static readonly ConcurrentDictionary<Key, TDelegate> Cache = new ConcurrentDictionary<Key, TDelegate>(new KeyComparer());

        private readonly Assembly _callingAssembly;
        private readonly int _mdToken;
        private readonly int _originalOpCodeValue;
        private readonly OpCodeValue _opCode;

        private MethodBase _methodBase;
        private Type _concreteType;
        private string _concreteTypeName;
        private object[] _argumentObjects = new object[0];

        private Type[] _declaringTypeGenericArguments = null;
        private Type[] _methodGenericArguments = null;

        private MethodBuilder(Assembly callingAssembly, int mdToken, int opCode)
        {
            _callingAssembly = callingAssembly;
            _mdToken = mdToken;
            _opCode = (OpCodeValue)opCode;
            _originalOpCodeValue = opCode;
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
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            _argumentObjects = parameters;
            return this;
        }

        public MethodBuilder<TDelegate> WithDeclaringTypeGenericTypeArguments(Type[] declaringTypeGenericTypeArguments)
        {
            _declaringTypeGenericArguments = declaringTypeGenericTypeArguments;
            return this;
        }

        public MethodBuilder<TDelegate> WithMethodGenericArguments(params Type[] methodGenericArguments)
        {
            _methodGenericArguments = methodGenericArguments;
            return this;
        }

        public TDelegate Build()
        {
            ValidateRequirements();

            var cacheKey = new Key(
                callingModule: _callingAssembly.ManifestModule,
                mdToken: _mdToken,
                callOpCode: _opCode,
                methodGenericArguments: _declaringTypeGenericArguments,
                genericParameterTypes: _methodGenericArguments);

            return Cache.GetOrAdd(cacheKey, key => EmitDelegate());
        }

        private TDelegate EmitDelegate()
        {
            var requiresBestEffortMatching = false;

            try
            {
                // Don't resolve until we build, because we need to wait for generics to be specified
                _methodBase =
                    _callingAssembly.ManifestModule.ResolveMethod(
                        metadataToken: _mdToken,
                        genericTypeArguments: _declaringTypeGenericArguments,
                        genericMethodArguments: _methodGenericArguments);
            }
            catch
            {
                requiresBestEffortMatching = true;
            }

            MethodInfo methodInfo = null;

            if (!requiresBestEffortMatching && _methodBase is MethodInfo info)
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
                methodInfo = methodInfo.MakeGenericMethod(_declaringTypeGenericArguments);
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
        }

        private MethodInfo TryFindMethod()
        {
            var methods =
                _concreteType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

            if (_methodBase != null)
            {
                // For the case I haven't encountered, where MethodBase is not MethodInfo
                methods =
                    methods.Where(
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
            }
            else
            {
                // An attempt to match on the metadata token for the concrete type

                // Non-Generic Method - { IsGenericMethod: false, ContainsGenericParameters: false, IsGenericMethodDefinition: false }
                // Generic Method Definition - { IsGenericMethod: true, ContainsGenericParameters: true, IsGenericMethodDefinition: true }
                // Open Constructed Method - { IsGenericMethod: true, ContainsGenericParameters: true, IsGenericMethodDefinition: false }
                // Closed Constructed Method - { IsGenericMethod: true, ContainsGenericParameters: false, IsGenericMethodDefinition: false }

                // We don't officially instrument any Closed Constructed Methods
                // This would need updating if we did
                var relevantMethods =
                    methods
                       .Where(mi => mi.MetadataToken == _mdToken) // This doesn't work at all with the method spec, maybe if it was the method def
                       .Where(
                            mi =>
                            {
                                var genericArgs = mi.GetGenericArguments();

                                if (genericArgs.Length != (_methodGenericArguments?.Length ?? 0))
                                {
                                    return false;
                                }

                                var parameters = mi.GetParameters();

                                if (parameters.Length != _argumentObjects.Length)
                                {
                                    return false;
                                }

                                for (var i = 0; i < parameters.Length; i++)
                                {
                                    var candidateParameter = parameters[i];

                                    var parameterType = candidateParameter.ParameterType;

                                    var actualArgument = _argumentObjects[i];

                                    if (actualArgument == null)
                                    {
                                        // Skip the rest of this check
                                        continue;
                                    }

                                    var actualArgumentType = actualArgument.GetType();

                                    if (!parameterType.IsAssignableFrom(actualArgumentType))
                                    {
                                        return false;
                                    }
                                }

                                return true;
                            });

                methods = relevantMethods.ToArray();
            }

            var methodText = _methodBase?.Name ?? $"mdToken: {_mdToken}";

            if (methods.Length > 1)
            {
                throw new ArgumentException($"Unable to safely resolve method {methodText} for {_concreteTypeName}");
            }

            var methodInfo = methods.SingleOrDefault();

            if (methodInfo == null)
            {
                throw new ArgumentException($"Unable to resolve method {methodText} for {_concreteTypeName}");
            }

            return methodInfo;
        }

        private struct Key
        {
            public readonly int CallingModuleMetadataToken;
            public readonly int MethodMetadataToken;
            public readonly OpCodeValue CallOpCode;
            public readonly string GenericSpec;

            public Key(
                Module callingModule,
                int mdToken,
                OpCodeValue callOpCode,
                Type[] methodGenericArguments,
                Type[] genericParameterTypes)
            {
                CallingModuleMetadataToken = callingModule.MetadataToken;
                MethodMetadataToken = mdToken;
                CallOpCode = callOpCode;

                GenericSpec = "_gArgs_";

                if (methodGenericArguments != null)
                {
                    for (var i = 0; i < methodGenericArguments.Length; i++)
                    {
                        GenericSpec = string.Concat(GenericSpec, $"_{methodGenericArguments[i].FullName}_");
                    }
                }

                GenericSpec = string.Concat(GenericSpec, "_gParams_");

                if (genericParameterTypes != null)
                {
                    for (var i = 0; i < genericParameterTypes.Length; i++)
                    {
                        GenericSpec = string.Concat(GenericSpec, $"_{genericParameterTypes[i].FullName}_");
                    }
                }
            }
        }

        private class KeyComparer : IEqualityComparer<Key>
        {
            public bool Equals(Key x, Key y)
            {
                if (!int.Equals(x.CallingModuleMetadataToken, y.CallingModuleMetadataToken))
                {
                    return false;
                }

                if (!int.Equals(x.MethodMetadataToken, y.MethodMetadataToken))
                {
                    return false;
                }

                if (!short.Equals(x.CallOpCode, y.CallOpCode))
                {
                    return false;
                }

                if (!string.Equals(x.GenericSpec, y.GenericSpec))
                {
                    return false;
                }

                return true;
            }

            public int GetHashCode(Key obj)
            {
                unchecked
                {
                    int hash = 17;
                    hash = (hash * 23) + obj.CallingModuleMetadataToken.GetHashCode();
                    hash = (hash * 23) + obj.MethodMetadataToken.GetHashCode();
                    hash = (hash * 23) + obj.CallOpCode.GetHashCode();
                    hash = (hash * 23) + obj.GenericSpec.GetHashCode();
                    return hash;
                }
            }
        }
    }
}
