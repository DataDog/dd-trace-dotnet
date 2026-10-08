// <copyright file="IntegrationBinder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;

namespace Datadog.Trace.Tools.Runner.Aot.CallTarget;

/// <summary>
/// Binds integration methods to handler shapes exactly as <c>IntegrationMapper</c> does at runtime (B2): same method
/// lookup by name, same validations, same generic arguments and same parameter loads. Checks that depend on the
/// instantiation of a generic target become <see cref="RuntimeTypeCheck"/>s.
/// </summary>
internal static class IntegrationBinder
{
    internal const string BeginMethodName = "OnMethodBegin";
    internal const string EndMethodName = "OnMethodEnd";
    internal const string EndAsyncMethodName = "OnAsyncMethodEnd";

    private const string CallTargetStateName = "Datadog.Trace.ClrProfiler.CallTarget.CallTargetState";
    private const string CallTargetReturnName = "Datadog.Trace.ClrProfiler.CallTarget.CallTargetReturn";
    private const string DuckTypeInterfaceName = "Datadog.Trace.DuckTyping.IDuckType";
    private const string PreserveContextAttributeName = "Datadog.Trace.ClrProfiler.CallTarget.PreserveContextAttribute";

    /// <summary>
    /// <c>IntegrationMapper.CreateBeginMethodDelegate</c>. Adapter parameters: <c>(TTarget instance, ref TArg1 arg1, ...)</c>.
    /// </summary>
    public static AdapterBinding BindBegin(TypeDef integration, TypeSig target, IReadOnlyList<TypeSig> arguments, IDuckProxyProvider? proxies = null)
    {
        if (FindMethod(integration, BeginMethodName) is not { } lookup)
        {
            return AdapterBinding.NoMethod();
        }

        if (lookup.Failure is { } ambiguous)
        {
            return ambiguous;
        }

        var method = lookup.Method!;
        var typeName = integration.ReflectionFullName;
        if (method.ReturnType.FullName != CallTargetStateName)
        {
            return AdapterBinding.Failure($"The return type of the method: {BeginMethodName} in type: {typeName} is not CallTargetState");
        }

        var genericParameters = method.GenericParameters;
        if (genericParameters.Count < 1)
        {
            return AdapterBinding.Failure($"The method: {BeginMethodName} in type: {typeName} doesn't have the generic type for the instance type.");
        }

        var parameters = method.MethodSig.Params;
        if (parameters.Count < arguments.Count)
        {
            return AdapterBinding.Failure($"The method: {BeginMethodName} with {parameters.Count} parameters in type: {typeName} has less parameters than required.");
        }

        if (parameters.Count > arguments.Count + 1)
        {
            return AdapterBinding.Failure($"The method: {BeginMethodName} with {parameters.Count} parameters in type: {typeName} has more parameters than required.");
        }

        if (parameters.Count != arguments.Count && !IsMethodVariable(parameters[0], 0))
        {
            return AdapterBinding.Failure($"The first generic argument for method: {BeginMethodName} in type: {typeName} must be the same as the first parameter for the instance value.");
        }

        var binding = AdapterBinding.Bound(method);
        var mustLoadInstance = parameters.Count != arguments.Count;
        if (BindInstance(binding, genericParameters[0], target, mustLoadInstance, null, BeginMethodName, proxies) is { } unboundInstance)
        {
            return unboundInstance;
        }

        for (var i = mustLoadInstance ? 1 : 0; i < parameters.Count; i++)
        {
            var argumentIndex = mustLoadInstance ? i - 1 : i;
            var source = arguments[argumentIndex];
            var adapterParameter = argumentIndex + 1;
            var parameter = parameters[i];
            if (parameter is GenericMVar variable)
            {
                if (FirstConstraint(genericParameters[(int)variable.Number], excludeDuckType: true) is { } constraint)
                {
                    var proxy = RequestProxy(proxies, constraint, source);
                    if (!IsUsable(proxy))
                    {
                        return Unbound(proxy, $"{BeginMethodName}: duck typing constraint on argument {argumentIndex + 1} ({constraint.FullName})");
                    }

                    binding.ProxyArguments.Add(binding.GenericArguments.Count);
                    binding.GenericArguments.Add(ProxyTypeSig(proxy, constraint));
                    binding.Loads.Add(AdapterLoad.Proxied(adapterParameter, source, proxy, source));
                    continue;
                }

                binding.GenericArguments.Add(source);
                binding.Loads.Add(AdapterLoad.ArgumentValue(adapterParameter, source));
            }
            else if (parameter is ByRefSig { Next: GenericMVar byRefVariable })
            {
                if (FirstConstraint(genericParameters[(int)byRefVariable.Number], excludeDuckType: true) is { } constraint)
                {
                    return AdapterBinding.Failure($"DuckType constraints cannot be used in ByRef arguments. ({constraint.ReflectionFullName})");
                }

                binding.GenericArguments.Add(source);
                binding.Loads.Add(AdapterLoad.Argument(adapterParameter));
            }
            else if (parameter.ContainsGenericParameter)
            {
                return AdapterBinding.Deferred($"{BeginMethodName}: unsupported generic parameter type {parameter.FullName}");
            }
            else
            {
                var targetType = parameter is ByRefSig byRef ? byRef.Next : parameter;
                AddCheck(binding, sameType: false, targetType, source, $"The target parameter {parameter.ReflectionFullName} can't be assigned from {source.ReflectionFullName}&");
                binding.Loads.Add(parameter is ByRefSig ? AdapterLoad.Argument(adapterParameter) : AdapterLoad.ArgumentValue(adapterParameter, source, BoxFor(targetType, source)));
            }
        }

        return Complete(binding, method);
    }

    /// <summary>
    /// <c>IntegrationMapper.CreateSlowBeginMethodDelegate</c>. Adapter parameters: <c>(TTarget instance, object[] arguments)</c>.
    /// </summary>
    /// <param name="integration">The integration type.</param>
    /// <param name="target">The target type.</param>
    /// <param name="proxies">The DuckType AOT proxies.</param>
    /// <param name="argumentTypes">The parameter types of the instrumented method, whose values duck typing converts at runtime.</param>
    public static AdapterBinding BindSlowBegin(TypeDef integration, TypeSig target, IDuckProxyProvider? proxies = null, IReadOnlyList<TypeSig>? argumentTypes = null)
    {
        if (FindMethod(integration, BeginMethodName) is not { } lookup)
        {
            return AdapterBinding.NoMethod();
        }

        if (lookup.Failure is { } ambiguous)
        {
            return ambiguous;
        }

        var method = lookup.Method!;
        var typeName = integration.ReflectionFullName;
        if (method.ReturnType.FullName != CallTargetStateName)
        {
            return AdapterBinding.Failure($"The return type of the method: {BeginMethodName} in type: {typeName} is not CallTargetState");
        }

        var genericParameters = method.GenericParameters;
        if (genericParameters.Count < 1)
        {
            return AdapterBinding.Failure($"The method: {BeginMethodName} in type: {typeName} doesn't have the generic type for the instance type.");
        }

        var parameters = method.MethodSig.Params;
        if (parameters.Count == 0)
        {
            return AdapterBinding.Failure("Index was outside the bounds of the array.");
        }

        var binding = AdapterBinding.Bound(method);
        var mustLoadInstance = IsMethodVariable(parameters[0], 0);
        if (BindInstance(binding, genericParameters[0], target, mustLoadInstance, null, BeginMethodName, proxies) is { } unboundInstance)
        {
            return unboundInstance;
        }

        for (var i = mustLoadInstance ? 1 : 0; i < parameters.Count; i++)
        {
            var parameter = parameters[i];
            var element = i - (mustLoadInstance ? 1 : 0);
            if (parameter is GenericMVar variable)
            {
                if (FirstConstraint(genericParameters[(int)variable.Number], excludeDuckType: true) is { } constraint)
                {
                    // Duck typing at runtime: IntegrationMapper.ConvertType, which the DuckType AOT registry serves.
                    if (proxies is not null && argumentTypes is not null && element < argumentTypes.Count)
                    {
                        proxies.RequestRuntimeProxy(constraint, argumentTypes[element]);
                    }

                    binding.GenericArguments.Add(constraint);
                    binding.Loads.Add(AdapterLoad.ArrayElementConvert(element, constraint));
                }
                else
                {
                    binding.GenericArguments.Add(method.Module.CorLibTypes.Object);
                    binding.Loads.Add(AdapterLoad.ArrayElement(element));
                }
            }
            else if (parameter.ContainsGenericParameter)
            {
                return AdapterBinding.Deferred($"{BeginMethodName}: unsupported generic parameter type {parameter.FullName}");
            }
            else if (parameter.IsValueType)
            {
                binding.Loads.Add(AdapterLoad.ArrayElementUnbox(element, parameter));
            }
            else
            {
                binding.Loads.Add(AdapterLoad.ArrayElement(element));
            }
        }

        return Complete(binding, method);
    }

    /// <summary>
    /// <c>IntegrationMapper.CreateEndMethodDelegate(integration, target)</c>. Adapter parameters:
    /// <c>(TTarget instance, Exception exception, in CallTargetState state)</c>.
    /// </summary>
    public static AdapterBinding BindEndVoid(TypeDef integration, TypeSig target, IDuckProxyProvider? proxies = null)
    {
        if (FindEndMethod(integration, "CallTargetReturn") is not { } method)
        {
            return AdapterBinding.NoMethod();
        }

        var typeName = integration.ReflectionFullName;
        if (method.ReturnType.FullName != CallTargetReturnName)
        {
            return AdapterBinding.Failure($"The return type of the method: {EndMethodName} in type: {typeName} is not CallTargetReturn");
        }

        var genericParameters = method.GenericParameters;
        if (genericParameters.Count != 1)
        {
            return AdapterBinding.Failure($"The method: {EndMethodName} in type: {typeName} must have a single generic type for the instance type.");
        }

        var parameters = method.MethodSig.Params;
        if (ValidateEndParameters(parameters, 2, EndMethodName, typeName) is { } invalid)
        {
            return invalid;
        }

        var binding = AdapterBinding.Bound(method);
        if (BindInstance(binding, genericParameters[0], target, parameters.Count == 3, parameters[0], EndMethodName, proxies) is { } unboundInstance)
        {
            return unboundInstance;
        }

        binding.Loads.Add(AdapterLoad.Argument(1));
        binding.Loads.Add(LoadState(parameters, 2));
        return Complete(binding, method);
    }

    /// <summary>
    /// <c>IntegrationMapper.CreateEndMethodDelegate(integration, target, returnType)</c>. Adapter parameters:
    /// <c>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)</c>.
    /// </summary>
    public static AdapterBinding BindEndReturn(TypeDef integration, TypeSig target, TypeSig returnType, IDuckProxyProvider? proxies = null)
    {
        if (FindEndMethod(integration, "CallTargetReturn`1") is not { } method)
        {
            return AdapterBinding.NoMethod();
        }

        var typeName = integration.ReflectionFullName;
        if (method.ReturnType is not GenericInstSig { GenericType.FullName: CallTargetReturnName + "`1" })
        {
            return AdapterBinding.Failure($"The return type of the method: {EndMethodName} in type: {typeName} is not CallTargetReturn");
        }

        return BindReturnValueMethod(integration, method, target, returnType, EndMethodName, isAsync: false, proxies);
    }

    /// <summary>
    /// <c>IntegrationMapper.CreateAsyncEndMethodDelegate(integration, target, returnType)</c>. Adapter parameters:
    /// <c>(TTarget instance, TResult returnValue, Exception exception, in CallTargetState state)</c>; it returns
    /// <c>TResult</c> or <c>Task&lt;TResult&gt;</c>.
    /// </summary>
    public static AdapterBinding BindAsyncEnd(TypeDef integration, TypeSig target, TypeSig returnType, IDuckProxyProvider? proxies = null)
    {
        if (FindMethod(integration, EndAsyncMethodName) is not { } lookup)
        {
            return AdapterBinding.NoMethod();
        }

        if (lookup.Failure is { } ambiguous)
        {
            return ambiguous;
        }

        return BindReturnValueMethod(integration, lookup.Method!, target, returnType, EndAsyncMethodName, isAsync: true, proxies);
    }

    private static AdapterBinding BindReturnValueMethod(TypeDef integration, MethodDef method, TypeSig target, TypeSig returnType, string name, bool isAsync, IDuckProxyProvider? proxies)
    {
        var typeName = integration.ReflectionFullName;
        var isTaskReturn = false;
        RuntimeTypeCheck? returnTypeCheck = null;
        if (isAsync && method.ReturnType is not GenericMVar && !Equivalent(method.ReturnType, returnType))
        {
            if (IsTask(method.ReturnType))
            {
                isTaskReturn = true;
            }
            else if (method.ReturnType.ContainsGenericParameter)
            {
                return AdapterBinding.Deferred($"{name}: unsupported generic return type {method.ReturnType.FullName}");
            }
            else
            {
                // Only known at runtime for a generic target.
                returnTypeCheck = new RuntimeTypeCheck(sameType: true, method.ReturnType, returnType, $"The return type of the method: {name} in type: {typeName} is not {returnType.ReflectionFullName}");
            }
        }

        var genericParameters = method.GenericParameters;
        if (genericParameters.Count < 1 || genericParameters.Count > 2)
        {
            return AdapterBinding.Failure($"The method: {name} in type: {typeName} must have the generic type for the instance type.");
        }

        var parameters = method.MethodSig.Params;
        if (ValidateEndParameters(parameters, 3, name, typeName) is { } invalid)
        {
            return invalid;
        }

        var preserveContext = isAsync && method.CustomAttributes.Any(a => a.AttributeType?.FullName == PreserveContextAttributeName);
        var binding = AdapterBinding.Bound(method, preserveContext, isTaskReturn);
        if (returnTypeCheck is not null)
        {
            binding.Checks.Add(returnTypeCheck);
        }

        if (BindInstance(binding, genericParameters[0], target, parameters.Count == 4, parameters[0], name, proxies) is { } unboundInstance)
        {
            return unboundInstance;
        }

        var returnLoad = AdapterLoad.Argument(1);
        var returnParameterIndex = parameters.Count == 4 ? 1 : 0;
        var returnParameter = parameters[returnParameterIndex];
        if (returnParameter is GenericMVar or GenericVar)
        {
            if (genericParameters.Count < 2)
            {
                return AdapterBinding.Failure("Index was outside the bounds of the array.");
            }

            if (FirstConstraint(genericParameters[1], excludeDuckType: false) is { } returnConstraint)
            {
                var proxy = RequestProxy(proxies, returnConstraint, returnType);
                if (!IsUsable(proxy))
                {
                    return Unbound(proxy, $"{name}: duck typing constraint on the return value ({returnConstraint.FullName})");
                }

                binding.ProxyArguments.Add(binding.GenericArguments.Count);
                binding.GenericArguments.Add(ProxyTypeSig(proxy, returnConstraint));
                binding.SetReturnProxy(proxy, returnType);
                returnLoad = AdapterLoad.Proxied(1, null, proxy, returnType);
            }
            else
            {
                binding.GenericArguments.Add(returnType);
            }
        }
        else if (returnParameter.ContainsGenericParameter)
        {
            return AdapterBinding.Deferred($"{name}: unsupported generic return value type {returnParameter.FullName}");
        }
        else
        {
            AddCheck(binding, sameType: true, returnParameter, returnType, $"The ReturnValue type parameter of the method: {name} in type: {typeName} is invalid. [{returnParameter.ReflectionFullName} != {returnType.ReflectionFullName}]");
        }

        binding.Loads.Add(returnLoad);
        binding.Loads.Add(AdapterLoad.Argument(2));
        binding.Loads.Add(LoadState(parameters, 3));
        return Complete(binding, method);
    }

    /// <summary>
    /// The state goes by reference (<c>in</c>) to the adapter, and by value (<c>ldobj</c>) or by reference to the integration.
    /// </summary>
    private static AdapterLoad LoadState(IList<TypeSig> parameters, int adapterParameter)
        => parameters[parameters.Count - 1] is ByRefSig ? AdapterLoad.Argument(adapterParameter) : AdapterLoad.ArgumentValue(adapterParameter, parameters[parameters.Count - 1]);

    /// <summary>
    /// The checks shared by the end methods: <paramref name="minimum"/> to <paramref name="minimum"/> + 1 parameters,
    /// an <see cref="System.Exception"/> and a <c>CallTargetState</c> (by value or by reference) as the last two.
    /// </summary>
    private static AdapterBinding? ValidateEndParameters(IList<TypeSig> parameters, int minimum, string name, string typeName)
    {
        if (parameters.Count < minimum)
        {
            return AdapterBinding.Failure($"The method: {name} with {parameters.Count} parameters in type: {typeName} has less parameters than required.");
        }

        if (parameters.Count > minimum + 1)
        {
            return AdapterBinding.Failure($"The method: {name} with {parameters.Count} parameters in type: {typeName} has more parameters than required.");
        }

        if (parameters[parameters.Count - 2].FullName != "System.Exception")
        {
            return AdapterBinding.Failure($"The Exception type parameter of the method: {name} in type: {typeName} is missing.");
        }

        var state = parameters[parameters.Count - 1];
        if ((state is ByRefSig byRef ? byRef.Next : state).FullName != CallTargetStateName)
        {
            return AdapterBinding.Failure($"The CallTargetState type parameter of the method: {name} in type: {typeName} is missing.");
        }

        return null;
    }

    /// <summary>
    /// <c>MethodInfo.MakeGenericMethod</c>: the arity, and the constraints of every generic parameter bound to a type
    /// (duck typing constraints were deferred before).
    /// </summary>
    private static AdapterBinding Complete(AdapterBinding binding, MethodDef method)
    {
        var genericParameters = method.GenericParameters;
        if (binding.GenericArguments.Count != genericParameters.Count)
        {
            return AdapterBinding.Failure("The number of generic arguments provided doesn't equal the arity of the generic method definition.");
        }

        for (var i = 0; i < genericParameters.Count; i++)
        {
            var genericParameter = genericParameters[i];
            if (binding.ProxyArguments.Contains(i))
            {
                // A proxy implements its duck type and IDuckType.
                continue;
            }

            if ((genericParameter.Flags & GenericParamAttributes.SpecialConstraintMask) != 0)
            {
                return AdapterBinding.Deferred($"{method.Name}: unsupported special constraint on {genericParameter.Name}");
            }

            foreach (var constraint in genericParameter.GenericParamConstraints)
            {
                var constraintType = constraint.Constraint.ToTypeSig();
                if (constraintType.ContainsGenericParameter)
                {
                    return AdapterBinding.Deferred($"{method.Name}: unsupported generic constraint {constraintType.FullName} on {genericParameter.Name}");
                }

                var argument = binding.GenericArguments[i];
                AddCheck(binding, sameType: false, constraintType, argument, $"GenericArguments[{i}], '{argument.ReflectionFullName}', on '{method.FullName}' violates the constraint of type '{genericParameter.Name}'.");
            }
        }

        return binding;
    }

    /// <summary>
    /// The instance: the target type, or the proxy of its duck typing constraint (on <c>IDuckType</c> too), which the
    /// integration method is instantiated with even when it has no instance parameter.
    /// </summary>
    private static AdapterBinding? BindInstance(AdapterBinding binding, GenericParam instanceParameter, TypeSig target, bool load, TypeSig? parameterType, string name, IDuckProxyProvider? proxies)
    {
        if (FirstConstraint(instanceParameter, excludeDuckType: false) is { } constraint)
        {
            var proxy = RequestProxy(proxies, constraint, target);
            if (!IsUsable(proxy))
            {
                return Unbound(proxy, $"{name}: duck typing constraint on the instance ({constraint.FullName})");
            }

            binding.ProxyArguments.Add(binding.GenericArguments.Count);
            binding.GenericArguments.Add(ProxyTypeSig(proxy, constraint));
            if (load)
            {
                binding.Loads.Add(AdapterLoad.Proxied(0, null, proxy, target));
            }

            return null;
        }

        binding.GenericArguments.Add(target);
        if (load)
        {
            binding.Loads.Add(AdapterLoad.Argument(0, parameterType is null ? null : BoxFor(parameterType, target)));
        }

        return null;
    }

    private static bool IsUsable(DuckProxy proxy) => proxy.Status is DuckProxyStatus.Available or DuckProxyStatus.Pending;

    /// <summary>
    /// The proxy type, or the duck type itself while the proxies are collected (nothing is emitted then).
    /// </summary>
    private static TypeSig ProxyTypeSig(DuckProxy proxy, TypeSig proxyDefinition) => proxy.Type?.ToTypeSig() ?? proxyDefinition;

    private static DuckProxy RequestProxy(IDuckProxyProvider? proxies, TypeSig proxyDefinition, TypeSig target)
    {
        if (proxies is null)
        {
            return DuckProxy.Unavailable("no DuckType AOT registry");
        }

        // The proxy depends on the instantiation of a generic target (instantiations aren't discovered yet, C3).
        return target.ContainsGenericParameter ? DuckProxy.Unavailable("open generic type") : proxies.Resolve(proxyDefinition, target);
    }

    /// <summary>
    /// <c>DuckType.GetOrCreateProxyType</c> failing makes <c>IntegrationMapper</c> throw; a proxy that can't be named yet
    /// defers the shape.
    /// </summary>
    private static AdapterBinding Unbound(DuckProxy proxy, string context)
        => proxy.Status == DuckProxyStatus.Failure ? AdapterBinding.Failure(proxy.Message ?? context) : AdapterBinding.Deferred($"{context}: {proxy.Message}");

    private static void AddCheck(AdapterBinding binding, bool sameType, TypeSig left, TypeSig right, string message)
    {
        if (!Equivalent(left, right))
        {
            binding.Checks.Add(new RuntimeTypeCheck(sameType, left, right, message));
        }
    }

    /// <summary>
    /// <c>Type.GetMethod(name, Public | NonPublic | Static)</c>: the static methods declared by the type, ambiguous when
    /// there are several.
    /// </summary>
    private static MethodLookup? FindMethod(TypeDef integration, string name)
    {
        var methods = integration.Methods.Where(m => m.IsStatic && m.Name == name).ToList();
        return methods.Count switch
        {
            0 => null,
            1 => new MethodLookup(methods[0], null),
            _ => new MethodLookup(null, AdapterBinding.Failure($"Ambiguous match found for '{integration.ReflectionFullName} {name}'.")),
        };
    }

    /// <summary>
    /// <c>IntegrationMapper.GetOnMethodEndMethodInfo</c>: several <c>OnMethodEnd</c> methods are disambiguated by the
    /// name of their return type.
    /// </summary>
    private static MethodDef? FindEndMethod(TypeDef integration, string returnTypeName)
    {
        var methods = integration.Methods.Where(m => m.IsStatic && m.Name == EndMethodName).ToList();
        if (methods.Count <= 1)
        {
            return methods.FirstOrDefault();
        }

        return methods.FirstOrDefault(m => TypeName(m.ReturnType) == returnTypeName);
    }

    private static string? TypeName(TypeSig type)
        => type is GenericInstSig instance ? instance.GenericType.TypeDefOrRef.Name.String : type.ToTypeDefOrRef()?.Name.String;

    /// <summary>
    /// <c>GetGenericParameterConstraints().FirstOrDefault()</c>, without <c>IDuckType</c> for the arguments.
    /// </summary>
    private static TypeSig? FirstConstraint(GenericParam genericParameter, bool excludeDuckType)
    {
        foreach (var constraint in genericParameter.GenericParamConstraints)
        {
            var type = constraint.Constraint.ToTypeSig();
            if (!excludeDuckType || type.FullName != DuckTypeInterfaceName)
            {
                return type;
            }
        }

        return null;
    }

    private static bool IsMethodVariable(TypeSig type, uint number) => type is GenericMVar variable && variable.Number == number;

    private static bool IsTask(TypeSig type)
        => type.FullName == "System.Threading.Tasks.Task" || type is GenericInstSig { GenericType.FullName: "System.Threading.Tasks.Task`1" };

    private static bool Equivalent(TypeSig left, TypeSig right) => new SigComparer().Equals(left, right);

    /// <summary>
    /// The type to box when a value of <paramref name="source"/> goes to a parameter of a reference type.
    /// <c>IntegrationMapper</c> doesn't box (invalid IL for a value type); boxing a reference type is a no-op.
    /// </summary>
    private static TypeSig? BoxFor(TypeSig parameter, TypeSig source)
    {
        if (parameter is GenericMVar or GenericVar or ByRefSig || parameter.IsValueType || source is ByRefSig or PtrSig)
        {
            return null;
        }

        return source is GenericMVar or GenericVar || source.IsValueType ? source : null;
    }

    private sealed class MethodLookup
    {
        public MethodLookup(MethodDef? method, AdapterBinding? failure)
        {
            Method = method;
            Failure = failure;
        }

        public MethodDef? Method { get; }

        public AdapterBinding? Failure { get; }
    }
}
#endif
