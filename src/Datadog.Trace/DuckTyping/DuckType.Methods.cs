// <copyright file="DuckType.Methods.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// Duck Type
    /// </summary>
    public static partial class DuckType
    {
        private static List<MethodInfo> GetMethods(Type baseType)
        {
            List<MethodInfo> selectedMethods = new List<MethodInfo>(GetBaseMethods(baseType));
            // If the base type is an interface we must make sure we implement all methods, including from other interfaces
            if (baseType.IsInterface)
            {
                Type[] implementedInterfaces = baseType.GetInterfaces();
                foreach (Type imInterface in implementedInterfaces)
                {
                    if (imInterface == typeof(IDuckType))
                    {
                        continue;
                    }

                    foreach (MethodInfo interfaceMethod in imInterface.GetMethods())
                    {
                        if (interfaceMethod.IsSpecialName)
                        {
                            continue;
                        }

                        string interfaceMethodName = interfaceMethod.ToString();
                        bool methodAlreadySelected = false;
                        foreach (MethodInfo currentMethod in selectedMethods)
                        {
                            if (currentMethod.ToString() == interfaceMethodName)
                            {
                                methodAlreadySelected = true;
                                break;
                            }
                        }

                        if (!methodAlreadySelected)
                        {
                            MethodInfo prevMethod = baseType.GetMethod(interfaceMethod.Name, DuckAttribute.DefaultFlags, null, interfaceMethod.GetParameters().Select(p => p.ParameterType).ToArray(), null);
                            if (prevMethod == null || prevMethod.GetCustomAttribute<DuckIgnoreAttribute>() is null)
                            {
                                selectedMethods.Add(interfaceMethod);
                            }
                        }
                    }
                }
            }

            return selectedMethods;

            static IEnumerable<MethodInfo> GetBaseMethods(Type baseType)
            {
                foreach (MethodInfo method in baseType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    // Avoid proxying object methods like ToString(), GetHashCode()
                    // or the Finalize() that creates problems by keeping alive the object to another collection.
                    // You can still proxy those methods if they are defined in an interface, or if you add the DuckInclude attribute.
                    if (method.DeclaringType == typeof(object))
                    {
                        bool include = method.GetCustomAttribute<DuckIncludeAttribute>(true) is not null;

                        if (!include)
                        {
                            continue;
                        }
                    }

                    if (method.IsSpecialName || method.IsFinal || method.IsPrivate)
                    {
                        continue;
                    }

                    if (baseType.IsInterface || method.IsAbstract || method.IsVirtual)
                    {
                        yield return method;
                    }
                }
            }
        }

        private static void CreateMethods(TypeBuilder proxyTypeBuilder, Type proxyType, Type targetType, FieldInfo instanceField)
        {
            var proxyMethodsDefinitions = GetMethods(proxyType);

            var targetMethodsDefinitions = GetMethods(targetType);

            foreach (var method in targetMethodsDefinitions)
            {
                if (method.GetCustomAttribute<DuckIncludeAttribute>(true) is not null)
                {
                    proxyMethodsDefinitions.Add(method);
                }
            }

            foreach (MethodInfo proxyMethodDefinition in proxyMethodsDefinitions)
            {
                // Ignore the method marked with `DuckIgnore` attribute
                if (proxyMethodDefinition.GetCustomAttribute<DuckIgnoreAttribute>(true) is not null)
                {
                    continue;
                }

                // Extract the method parameters types
                ParameterInfo[] proxyMethodDefinitionParameters = proxyMethodDefinition.GetParameters();
                Type[] proxyMethodDefinitionParametersTypes = proxyMethodDefinitionParameters.Select(p => p.ParameterType).ToArray();

                // We select the target method to call
                MethodInfo targetMethod = SelectTargetMethod(targetType, proxyMethodDefinition, proxyMethodDefinitionParameters, proxyMethodDefinitionParametersTypes);

                // If the target method couldn't be found and the proxy method doesn't have an implementation already (ex: abstract and virtual classes) we throw.
                if (targetMethod is null && proxyMethodDefinition.IsVirtual)
                {
                    DuckTypeTargetMethodNotFoundException.Throw(proxyMethodDefinition);
                }

                // Check if target method is a reverse method
                bool isReverse = targetMethod.GetCustomAttribute<DuckReverseMethodAttribute>(true) is not null;

                // Gets the proxy method definition generic arguments
                Type[] proxyMethodDefinitionGenericArguments = proxyMethodDefinition.GetGenericArguments();
                string[] proxyMethodDefinitionGenericArgumentsNames = proxyMethodDefinitionGenericArguments.Select(a => a.Name).ToArray();

                // Checks if the target method is a generic method while the proxy method is non generic (checks if the Duck attribute contains the generic parameters)
                Type[] targetMethodGenericArguments = targetMethod.GetGenericArguments();
                if (proxyMethodDefinitionGenericArguments.Length == 0 && targetMethodGenericArguments.Length > 0)
                {
                    DuckAttribute proxyDuckAttribute = proxyMethodDefinition.GetCustomAttribute<DuckAttribute>();
                    if (proxyDuckAttribute is null)
                    {
                        DuckTypeTargetMethodNotFoundException.Throw(proxyMethodDefinition);
                    }

                    if (proxyDuckAttribute.GenericParameterTypeNames?.Length != targetMethodGenericArguments.Length)
                    {
                        DuckTypeTargetMethodNotFoundException.Throw(proxyMethodDefinition);
                    }

                    targetMethod = targetMethod.MakeGenericMethod(proxyDuckAttribute.GenericParameterTypeNames.Select(name => Type.GetType(name)).ToArray());
                }

                // Gets target method parameters
                ParameterInfo[] targetMethodParameters = targetMethod.GetParameters();
                Type[] targetMethodParametersTypes = targetMethodParameters.Select(p => p.ParameterType).ToArray();

                // Make sure we have the right methods attributes.
                MethodAttributes proxyMethodAttributes = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.HideBySig;

                // Create the proxy method implementation
                ParameterBuilder[] proxyMethodParametersBuilders = new ParameterBuilder[proxyMethodDefinitionParameters.Length];
                MethodBuilder proxyMethod = proxyTypeBuilder.DefineMethod(proxyMethodDefinition.Name, proxyMethodAttributes, proxyMethodDefinition.ReturnType, proxyMethodDefinitionParametersTypes);
                if (proxyMethodDefinitionGenericArgumentsNames.Length > 0)
                {
                    _ = proxyMethod.DefineGenericParameters(proxyMethodDefinitionGenericArgumentsNames);
                }

                // Define the proxy method implementation parameters for optional parameters with default values
                for (int j = 0; j < proxyMethodDefinitionParameters.Length; j++)
                {
                    ParameterInfo pmDefParameter = proxyMethodDefinitionParameters[j];
                    ParameterBuilder pmImpParameter = proxyMethod.DefineParameter(j, pmDefParameter.Attributes, pmDefParameter.Name);
                    if (pmDefParameter.HasDefaultValue)
                    {
                        pmImpParameter.SetConstant(pmDefParameter.RawDefaultValue);
                    }

                    proxyMethodParametersBuilders[j] = pmImpParameter;
                }

                LazyILGenerator il = new LazyILGenerator(proxyMethod.GetILGenerator());
                Type returnType = targetMethod.ReturnType;
                List<OutputAndRefParameterData> outputAndRefParameters = null;

                // Load the instance if needed
                if (!targetMethod.IsStatic)
                {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(instanceField.FieldType.IsValueType ? OpCodes.Ldflda : OpCodes.Ldfld, instanceField);
                }

                // Load all the arguments / parameters
                int maxParamLength = Math.Max(proxyMethodDefinitionParameters.Length, targetMethodParameters.Length);
                for (int idx = 0; idx < maxParamLength; idx++)
                {
                    ParameterInfo proxyParamInfo = idx < proxyMethodDefinitionParameters.Length ? proxyMethodDefinitionParameters[idx] : null;
                    ParameterInfo targetParamInfo = targetMethodParameters[idx];

                    if (proxyParamInfo is null)
                    {
                        // The proxy method is missing parameters, we check if the target parameter is optional
                        if (!targetParamInfo.IsOptional)
                        {
                            // The target method parameter is not optional.
                            DuckTypeProxyMethodParameterIsMissingException.Throw(proxyMethodDefinition, targetParamInfo);
                        }
                    }
                    else
                    {
                        if (proxyParamInfo.IsOut != targetParamInfo.IsOut || proxyParamInfo.IsIn != targetParamInfo.IsIn)
                        {
                            // the proxy and target parameters doesn't have the same signature
                            DuckTypeProxyAndTargetMethodParameterSignatureMismatchException.Throw(proxyMethodDefinition, targetMethod);
                        }

                        Type proxyParamType = proxyParamInfo.ParameterType;
                        Type targetParamType = targetParamInfo.ParameterType;

                        if (proxyParamType.IsByRef != targetParamType.IsByRef)
                        {
                            // the proxy and target parameters doesn't have the same signature
                            DuckTypeProxyAndTargetMethodParameterSignatureMismatchException.Throw(proxyMethodDefinition, targetMethod);
                        }

                        // We check if we have to handle an output parameter, by ref parameter or a normal parameter
                        if (proxyParamInfo.IsOut)
                        {
                            // If is an output parameter with diferent types we need to handle differently
                            // by creating a local var first to store the target parameter out value
                            // and then try to set the output parameter of the proxy method by converting the value (a base class or a duck typing)
                            if (proxyParamType != targetParamType)
                            {
                                LocalBuilder localTargetArg = il.DeclareLocal(targetParamType.GetElementType());

                                // We need to store the output parameter data to set the proxy parameter value after we call the target method
                                if (outputAndRefParameters is null)
                                {
                                    outputAndRefParameters = new List<OutputAndRefParameterData>();
                                }

                                outputAndRefParameters.Add(new OutputAndRefParameterData(localTargetArg.LocalIndex, targetParamType, idx, proxyParamType));

                                // Load the local var ref (to be used in the target method param as output)
                                il.Emit(OpCodes.Ldloca_S, localTargetArg.LocalIndex);
                            }
                            else
                            {
                                il.WriteLoadArgument(idx, false);
                            }
                        }
                        else if (proxyParamType.IsByRef)
                        {
                            // If is a ref parameter with diferent types we need to handle differently
                            // by creating a local var first to store the initial proxy parameter ref value casted to the target parameter type ( this cast may fail at runtime )
                            // later pass this local var ref to the target method, and then, modify the proxy parameter ref with the new reference from the target method
                            // by converting the value (a base class or a duck typing)
                            if (proxyParamType != targetParamType)
                            {
                                Type proxyParamTypeElementType = proxyParamType.GetElementType();
                                Type targetParamTypeElementType = targetParamType.GetElementType();

                                if (!UseDirectAccessTo(proxyTypeBuilder, targetParamTypeElementType))
                                {
                                    targetParamType = typeof(object).MakeByRefType();
                                    targetParamTypeElementType = typeof(object);
                                }

                                LocalBuilder localTargetArg = il.DeclareLocal(targetParamTypeElementType);

                                // We need to store the ref parameter data to set the proxy parameter value after we call the target method
                                if (outputAndRefParameters is null)
                                {
                                    outputAndRefParameters = new List<OutputAndRefParameterData>();
                                }

                                outputAndRefParameters.Add(new OutputAndRefParameterData(localTargetArg.LocalIndex, targetParamType, idx, proxyParamType));

                                // Load the argument (ref)
                                il.WriteLoadArgument(idx, false);

                                // Load the value inside the ref
                                il.Emit(OpCodes.Ldind_Ref);

                                // Check if the type can be converted of if we need to enable duck chaining
                                if (NeedsDuckChaining(targetParamTypeElementType, proxyParamTypeElementType))
                                {
                                    // First we check if the value is null before trying to get the instance value
                                    Label lblCallGetInstance = il.DefineLabel();
                                    Label lblAfterGetInstance = il.DefineLabel();

                                    il.Emit(OpCodes.Dup);
                                    il.Emit(OpCodes.Brtrue_S, lblCallGetInstance);

                                    il.Emit(OpCodes.Pop);
                                    il.Emit(OpCodes.Ldnull);
                                    il.Emit(OpCodes.Br_S, lblAfterGetInstance);

                                    // Call IDuckType.Instance property to get the actual value
                                    il.MarkLabel(lblCallGetInstance);
                                    il.Emit(OpCodes.Castclass, typeof(IDuckType));
                                    il.EmitCall(OpCodes.Callvirt, DuckTypeInstancePropertyInfo.GetMethod, null);
                                    il.MarkLabel(lblAfterGetInstance);
                                }

                                // Cast the value to the target type
                                il.WriteSafeTypeConversion(proxyParamTypeElementType, targetParamTypeElementType);

                                // Store the casted value to the local var
                                il.WriteStoreLocal(localTargetArg.LocalIndex);

                                // Load the local var ref (to be used in the target method param)
                                il.Emit(OpCodes.Ldloca_S, localTargetArg.LocalIndex);
                            }
                            else
                            {
                                il.WriteLoadArgument(idx, false);
                            }
                        }
                        else if (!isReverse)
                        {
                            // Check if the type can be converted of if we need to enable duck chaining
                            if (NeedsDuckChaining(targetParamType, proxyParamType))
                            {
                                // Load the argument and cast it as Duck type
                                il.WriteLoadArgument(idx, false);
                                il.Emit(OpCodes.Castclass, typeof(IDuckType));

                                // Call IDuckType.Instance property to get the actual value
                                il.EmitCall(OpCodes.Callvirt, DuckTypeInstancePropertyInfo.GetMethod, null);
                            }
                            else
                            {
                                il.WriteLoadArgument(idx, false);
                            }

                            // If the target parameter type is public or if it's by ref we have to actually use the original target type.
                            targetParamType = UseDirectAccessTo(proxyTypeBuilder, targetParamType) ? targetParamType : typeof(object);
                            il.WriteSafeTypeConversion(proxyParamType, targetParamType);

                            targetMethodParametersTypes[idx] = targetParamType;
                        }
                        else
                        {
                            if (NeedsDuckChaining(proxyParamType, targetParamType))
                            {
                                // Load the argument (our proxy type) and cast it as Duck type (the original type)
                                il.WriteLoadArgument(idx, false);
                                if (UseDirectAccessTo(proxyTypeBuilder, proxyParamType) && proxyParamType.IsValueType)
                                {
                                    il.Emit(OpCodes.Box, proxyParamType);
                                }

                                // We call DuckType.CreateCache<>.Create(object instance)
                                MethodInfo getProxyMethodInfo = typeof(CreateCache<>)
                                    .MakeGenericType(targetParamType).GetMethod("Create");

                                il.Emit(OpCodes.Call, getProxyMethodInfo);
                            }
                            else
                            {
                                il.WriteLoadArgument(idx, false);
                            }

                            // If the target parameter type is public or if it's by ref we have to actually use the original target type.
                            targetParamType = UseDirectAccessTo(proxyTypeBuilder, targetParamType) ? targetParamType : typeof(object);
                            il.WriteSafeTypeConversion(proxyParamType, targetParamType);

                            targetMethodParametersTypes[idx] = targetParamType;
                        }
                    }
                }

                // Call the target method
                if (UseDirectAccessTo(proxyTypeBuilder, targetType))
                {
                    // If the instance is public we can emit directly without any dynamic method

                    // Create generic method call
                    if (proxyMethodDefinitionGenericArguments.Length > 0)
                    {
                        targetMethod = targetMethod.MakeGenericMethod(proxyMethodDefinitionGenericArguments);
                    }

                    // Method call
                    // A generic method cannot be called using calli (throws System.InvalidOperationException)
                    if (targetMethod.IsPublic || targetMethod.IsGenericMethod)
                    {
                        // We can emit a normal call if we have a public instance with a public target method.
                        il.EmitCall(targetMethod.IsStatic || targetMethod.DeclaringType.IsValueType ? OpCodes.Call : OpCodes.Callvirt, targetMethod, null);
                    }
                    else
                    {
                        // In case we have a public instance and a non public target method we can use [Calli] with the function pointer
                        il.WriteMethodCalli(targetMethod);
                    }
                }
                else
                {
                    // A generic method call can't be made from a DynamicMethod
                    if (proxyMethodDefinitionGenericArguments.Length > 0)
                    {
                        DuckTypeProxyMethodsWithGenericParametersNotSupportedInNonPublicInstancesException.Throw(proxyMethod);
                    }

                    // If the instance is not public we need to create a Dynamic method to overpass the visibility checks
                    // we can't access non public types so we have to cast to object type (in the instance object and the return type).

                    string dynMethodName = $"_callMethod_{targetMethod.DeclaringType.Name}_{targetMethod.Name}";
                    returnType = UseDirectAccessTo(proxyTypeBuilder, targetMethod.ReturnType) && !targetMethod.ReturnType.IsGenericParameter ? targetMethod.ReturnType : typeof(object);

                    // We create the dynamic method
                    Type[] originalTargetParameters = targetMethod.GetParameters().Select(p => p.ParameterType).ToArray();
                    Type[] targetParameters = targetMethod.IsStatic ? originalTargetParameters : (new[] { typeof(object) }).Concat(originalTargetParameters).ToArray();
                    Type[] dynParameters = targetMethod.IsStatic ? targetMethodParametersTypes : (new[] { typeof(object) }).Concat(targetMethodParametersTypes).ToArray();
                    DynamicMethod dynMethod = new DynamicMethod(dynMethodName, returnType, dynParameters, proxyTypeBuilder.Module, true);

                    // Emit the dynamic method body
                    LazyILGenerator dynIL = new LazyILGenerator(dynMethod.GetILGenerator());

                    if (!targetMethod.IsStatic)
                    {
                        dynIL.LoadInstanceArgument(typeof(object), targetMethod.DeclaringType);
                    }

                    for (int idx = targetMethod.IsStatic ? 0 : 1; idx < dynParameters.Length; idx++)
                    {
                        dynIL.WriteLoadArgument(idx, true);
                        dynIL.WriteSafeTypeConversion(dynParameters[idx], targetParameters[idx]);
                    }

                    // Check if we can emit a normal Call/CallVirt to the target method
                    if (!targetMethod.ContainsGenericParameters)
                    {
                        dynIL.EmitCall(targetMethod.IsStatic || targetMethod.DeclaringType.IsValueType ? OpCodes.Call : OpCodes.Callvirt, targetMethod, null);
                    }
                    else
                    {
                        // We can't emit a call to a method with generics from a DynamicMethod
                        // Instead we emit a Calli with the function pointer.
                        dynIL.WriteMethodCalli(targetMethod);
                    }

                    dynIL.WriteSafeTypeConversion(targetMethod.ReturnType, returnType);
                    dynIL.Emit(OpCodes.Ret);
                    dynIL.Flush();

                    // Emit the call to the dynamic method
                    il.WriteDynamicMethodCall(dynMethod, proxyTypeBuilder);
                }

                // We check if we have output or ref parameters to set in the proxy method
                if (outputAndRefParameters != null)
                {
                    foreach (OutputAndRefParameterData outOrRefParameter in outputAndRefParameters)
                    {
                        Type proxyArgumentType = outOrRefParameter.ProxyArgumentType.GetElementType();
                        Type localType = outOrRefParameter.LocalType.GetElementType();

                        // We load the argument to be set
                        il.WriteLoadArgument(outOrRefParameter.ProxyArgumentIndex, false);

                        // We load the value from the local
                        il.WriteLoadLocal(outOrRefParameter.LocalIndex);

                        // If we detect duck chaining we create a new proxy instance with the output of the original target method
                        if (NeedsDuckChaining(localType, proxyArgumentType))
                        {
                            if (localType.IsValueType)
                            {
                                il.Emit(OpCodes.Box, localType);
                            }

                            // We call DuckType.CreateCache<>.Create()
                            MethodInfo getProxyMethodInfo = typeof(CreateCache<>)
                                .MakeGenericType(proxyArgumentType).GetMethod("Create");

                            il.Emit(OpCodes.Call, getProxyMethodInfo);
                        }
                        else
                        {
                            il.WriteSafeTypeConversion(localType, proxyArgumentType);
                        }

                        // We store the value
                        il.Emit(OpCodes.Stind_Ref);
                    }
                }

                // Check if the target method returns something
                if (targetMethod.ReturnType != typeof(void))
                {
                    // Handle the return value
                    // Check if the type can be converted or if we need to enable duck chaining
                    if (NeedsDuckChaining(targetMethod.ReturnType, proxyMethodDefinition.ReturnType))
                    {
                        if (UseDirectAccessTo(proxyTypeBuilder, targetMethod.ReturnType) && targetMethod.ReturnType.IsValueType)
                        {
                            il.Emit(OpCodes.Box, targetMethod.ReturnType);
                        }

                        // We call DuckType.CreateCache<>.Create()
                        MethodInfo getProxyMethodInfo = typeof(CreateCache<>)
                            .MakeGenericType(proxyMethodDefinition.ReturnType).GetMethod("Create");

                        il.Emit(OpCodes.Call, getProxyMethodInfo);
                    }
                    else if (returnType != proxyMethodDefinition.ReturnType)
                    {
                        // If the type is not the expected type we try a conversion.
                        il.WriteSafeTypeConversion(returnType, proxyMethodDefinition.ReturnType);
                    }
                }

                il.Emit(OpCodes.Ret);
                il.Flush();
                _methodBuilderGetToken.Invoke(proxyMethod, null);
            }
        }

        private static MethodInfo SelectTargetMethod(Type targetType, MethodInfo proxyMethod, ParameterInfo[] proxyMethodParameters, Type[] proxyMethodParametersTypes)
        {
            DuckAttribute proxyMethodDuckAttribute = proxyMethod.GetCustomAttribute<DuckAttribute>(true) ?? new DuckAttribute();
            proxyMethodDuckAttribute.Name ??= proxyMethod.Name;

            MethodInfo targetMethod = null;

            // Check if the duck attribute has the parameter type names to use for selecting the target method, in case of not found an exception is thrown.
            if (proxyMethodDuckAttribute.ParameterTypeNames != null)
            {
                Type[] parameterTypes = proxyMethodDuckAttribute.ParameterTypeNames.Select(pName => Type.GetType(pName, true)).ToArray();
                targetMethod = targetType.GetMethod(proxyMethodDuckAttribute.Name, proxyMethodDuckAttribute.BindingFlags, null, parameterTypes, null);
                if (targetMethod is null)
                {
                    DuckTypeTargetMethodNotFoundException.Throw(proxyMethod);
                }

                return targetMethod;
            }

            // If the duck attribute doesn't specify the parameters to use, we do the best effor to find a target method without any ambiguity.

            // First we try with the current proxy parameter types
            targetMethod = targetType.GetMethod(proxyMethodDuckAttribute.Name, proxyMethodDuckAttribute.BindingFlags, null, proxyMethodParametersTypes, null);
            if (targetMethod != null)
            {
                return targetMethod;
            }

            // If the method wasn't found could be because a DuckType interface is being use in the parameters or in the return value.
            // Also this can happen if the proxy parameters type uses a base object (ex: System.Object) instead the type.
            // In this case we try to find a method that we can match, in case of ambiguity (> 1 method found) we throw an exception.

            MethodInfo[] allTargetMethods = targetType.GetMethods(DuckAttribute.DefaultFlags);
            foreach (MethodInfo candidateMethod in allTargetMethods)
            {
                string name = proxyMethodDuckAttribute.Name;
                bool useRelaxedNameComparison = false;

                // If there is an explicit interface type name we add it to the name
                if (!string.IsNullOrEmpty(proxyMethodDuckAttribute.ExplicitInterfaceTypeName))
                {
                    string interfaceTypeName = proxyMethodDuckAttribute.ExplicitInterfaceTypeName;

                    if (interfaceTypeName == "*")
                    {
                        // If a wildcard is use, then we relax the name comparison so it can be an implicit or explicity implementation
                        useRelaxedNameComparison = true;
                    }
                    else
                    {
                        // Nested types are separated with a "." on explicit implementation.
                        interfaceTypeName = interfaceTypeName.Replace("+", ".");

                        name = interfaceTypeName + "." + name;
                    }
                }

                // We omit target methods with different names.
                if (candidateMethod.Name != name)
                {
                    if (!useRelaxedNameComparison || !candidateMethod.Name.EndsWith("." + name))
                    {
                        continue;
                    }
                }

                // Check if the candidate method is a reverse mapped method
                DuckReverseMethodAttribute reverseMethodAttribute = candidateMethod.GetCustomAttribute<DuckReverseMethodAttribute>(true);
                if (reverseMethodAttribute?.Arguments is not null)
                {
                    string[] arguments = reverseMethodAttribute.Arguments;
                    if (arguments.Length != proxyMethodParametersTypes.Length)
                    {
                        continue;
                    }

                    bool match = true;
                    for (var i = 0; i < arguments.Length; i++)
                    {
                        if (arguments[i] != proxyMethodParametersTypes[i].FullName && arguments[i] != proxyMethodParametersTypes[i].Name)
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match)
                    {
                        return candidateMethod;
                    }
                }

                ParameterInfo[] candidateParameters = candidateMethod.GetParameters();

                // The proxy must have the same or less parameters than the candidate ( less is due to possible optional parameters in the candidate ).
                if (proxyMethodParameters.Length > candidateParameters.Length)
                {
                    continue;
                }

                // We compare the target method candidate parameter by parameter.
                bool skip = false;
                for (int i = 0; i < proxyMethodParametersTypes.Length; i++)
                {
                    ParameterInfo proxyParam = proxyMethodParameters[i];
                    ParameterInfo candidateParam = candidateParameters[i];

                    Type proxyParamType = proxyParam.ParameterType;
                    Type candidateParamType = candidateParam.ParameterType;

                    // both needs to have the same parameter direction
                    if (proxyParam.IsOut != candidateParam.IsOut)
                    {
                        skip = true;
                        break;
                    }

                    // Both need to have the same element type or byref type signature.
                    if (proxyParamType.IsByRef != candidateParamType.IsByRef)
                    {
                        skip = true;
                        break;
                    }

                    // If the parameters are by ref we unwrap them to have the actual type
                    proxyParamType = proxyParamType.IsByRef ? proxyParamType.GetElementType() : proxyParamType;
                    candidateParamType = candidateParamType.IsByRef ? candidateParamType.GetElementType() : candidateParamType;

                    // We can't compare generic parameters
                    if (candidateParamType.IsGenericParameter)
                    {
                        continue;
                    }

                    // If the proxy parameter type is a value type (no ducktyping neither a base class) both types must match
                    if (proxyParamType.IsValueType && !proxyParamType.IsEnum && proxyParamType != candidateParamType)
                    {
                        skip = true;
                        break;
                    }

                    // If the proxy parameter is a class and not is an abstract class (only interface and abstract class can be used as ducktype base type)
                    if (proxyParamType.IsClass && !proxyParamType.IsAbstract && proxyParamType != typeof(object))
                    {
                        if (!candidateParamType.IsAssignableFrom(proxyParamType))
                        {
                            // Check if the parameter type contains generic types before skipping
                            if (!candidateParamType.IsGenericType || !proxyParamType.IsGenericType)
                            {
                                skip = true;
                                break;
                            }

                            // if the string representation of the generic parameter types is not the same we need to analyze the
                            // GenericTypeArguments array before skipping it
                            if (candidateParamType.ToString() != proxyParamType.ToString())
                            {
                                if (candidateParamType.GenericTypeArguments.Length != proxyParamType.GenericTypeArguments.Length)
                                {
                                    skip = true;
                                    break;
                                }

                                for (int paramIndex = 0; paramIndex < candidateParamType.GenericTypeArguments.Length; paramIndex++)
                                {
                                    Type candidateParamTypeGenericType = candidateParamType.GenericTypeArguments[paramIndex];
                                    Type proxyParamTypeGenericType = proxyParamType.GenericTypeArguments[paramIndex];

                                    // Both need to have the same element type or byref type signature.
                                    if (proxyParamTypeGenericType.IsByRef != candidateParamTypeGenericType.IsByRef)
                                    {
                                        skip = true;
                                        break;
                                    }

                                    // If the parameters are by ref we unwrap them to have the actual type
                                    proxyParamTypeGenericType = proxyParamTypeGenericType.IsByRef ? proxyParamTypeGenericType.GetElementType() : proxyParamTypeGenericType;
                                    candidateParamTypeGenericType = candidateParamTypeGenericType.IsByRef ? candidateParamTypeGenericType.GetElementType() : candidateParamTypeGenericType;

                                    // We can't compare generic parameters
                                    if (candidateParamTypeGenericType.IsGenericParameter)
                                    {
                                        continue;
                                    }

                                    // If the proxy parameter type is a value type (no ducktyping neither a base class) both types must match
                                    if (proxyParamTypeGenericType.IsValueType && !proxyParamTypeGenericType.IsEnum && proxyParamTypeGenericType != candidateParamTypeGenericType)
                                    {
                                        skip = true;
                                        break;
                                    }

                                    // If the proxy parameter is a class and not is an abstract class (only interface and abstract class can be used as ducktype base type)
                                    if (proxyParamTypeGenericType.IsClass && !proxyParamTypeGenericType.IsAbstract && proxyParamTypeGenericType != typeof(object))
                                    {
                                        if (!candidateParamTypeGenericType.IsAssignableFrom(proxyParamTypeGenericType))
                                        {
                                            skip = true;
                                            break;
                                        }
                                    }
                                }

                                if (skip)
                                {
                                    break;
                                }
                            }
                        }
                    }
                }

                if (skip)
                {
                    continue;
                }

                // The target method may have optional parameters with default values so we have to skip those
                for (int i = proxyMethodParametersTypes.Length; i < candidateParameters.Length; i++)
                {
                    if (!candidateParameters[i].IsOptional)
                    {
                        skip = true;
                        break;
                    }
                }

                if (skip)
                {
                    continue;
                }

                if (targetMethod is null)
                {
                    targetMethod = candidateMethod;
                }
                else
                {
                    DuckTypeTargetMethodAmbiguousMatchException.Throw(proxyMethod, targetMethod, candidateMethod);
                }
            }

            return targetMethod;
        }

        private static void WriteSafeTypeConversion(this LazyILGenerator il, Type actualType, Type expectedType)
        {
            // If both types are generics, we expect that the generic parameter are the same type (passthrough)
            if (actualType.IsGenericParameter && expectedType.IsGenericParameter)
            {
                return;
            }

            il.WriteTypeConversion(actualType, expectedType);
        }

        private readonly struct OutputAndRefParameterData
        {
            public readonly Type LocalType;
            public readonly Type ProxyArgumentType;
            public readonly int LocalIndex;
            public readonly int ProxyArgumentIndex;

            public OutputAndRefParameterData(int localIndex, Type localType, int proxyArgumentIndex, Type proxyArgumentType)
            {
                LocalIndex = localIndex;
                LocalType = localType;
                ProxyArgumentIndex = proxyArgumentIndex;
                ProxyArgumentType = proxyArgumentType;
            }
        }
    }
}
