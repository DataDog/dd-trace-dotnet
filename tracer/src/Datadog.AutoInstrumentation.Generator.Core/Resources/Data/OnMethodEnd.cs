    internal static CallTargetReturn<$(TReturnTypeParameter)> OnMethodEnd<TTarget$(TReturnType)>(TTarget instance, $(TReturnTypeParameter) returnValue, Exception? exception, in CallTargetState state)$(TArgsConstraint)
    {
        return new CallTargetReturn<$(TReturnTypeParameter)>(returnValue);
    }
