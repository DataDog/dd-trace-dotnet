    /// <summary>
    /// OnAsyncMethodEnd callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>$(TReturnTypeParamDocumentation)$(TReturnParamDocumentation)
    /// <param name="exception">Exception instance in case the original code threw an exception.</param>
    /// <param name="state">Calltarget state value</param>
    /// <returns>A response value, in an async scenario will be T of Task of T</returns>
    internal static $(TReturnTypeParameter) OnAsyncMethodEnd<TTarget$(TReturnType)>($(TReturnTypeParameter) returnValue, Exception exception, in CallTargetState state)$(TArgsConstraint)
    {
        return returnValue;
    }