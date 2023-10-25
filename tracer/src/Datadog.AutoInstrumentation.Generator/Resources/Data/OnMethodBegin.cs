    /// <summary>
    /// OnMethodBegin callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>$(TArgsTypesTypeParamDocumentation)
    /// <param name="instance">Instance value, aka `this` of the instrumented method</param>$(TArgsTypesParamDocumentation)
    /// <returns>Calltarget state value</returns>
    internal static CallTargetState OnMethodBegin<TTarget$(TArgsTypes)>(TTarget instance$(TArgsParameters))$(TArgsConstraint)
    {
        return CallTargetState.GetDefault();
    }