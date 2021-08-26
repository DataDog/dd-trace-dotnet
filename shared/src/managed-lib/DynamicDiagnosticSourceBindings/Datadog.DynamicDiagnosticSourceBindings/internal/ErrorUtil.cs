using System;
using System.Runtime.ExceptionServices;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    internal static class ErrorUtil

    {
        public const string DynamicInvokerLogComponentMoniker = "DynamicInvoker";

        public const string ErrorInvokingStubbedApiMsg = "Error dynamically invoking stubbed API";
        public const string CannotCreateStubMsg = "Cannot create stub for specified instance";

        public static Exception LogAndRethrowStubInvocationError(string message,
                                                                 Exception error,
                                                                 Type dynamicInvokerType,
                                                                 string invokedApiName,
                                                                 bool isStaticApi,
                                                                 Type invokerTargetType,
                                                                 object targetInstance)
        {
            error = CreateErrorIfNoneSpecified(error, dynamicInvokerType, message);

            Type targetInstanceType = targetInstance?.GetType();
            Log.Error(DynamicInvokerLogComponentMoniker,
                      message,
                      error,
                      "DynamicInvokerType",
                      dynamicInvokerType?.Name,
                      "API name",
                      invokedApiName,
                      "IsStaticApi",
                      isStaticApi,
                     $"The invoker's TargetType",
                      invokerTargetType?.AssemblyQualifiedName,
                     $"The invoker's TargetType.Assembly.Location",
                      invokerTargetType?.Assembly?.Location,
                      "targetInstanceType.AssemblyQualifiedName",
                      targetInstanceType?.AssemblyQualifiedName,
                      "targetInstanceType.Assembly.Location",
                      targetInstanceType?.Assembly?.Location);

            ExceptionDispatchInfo.Capture(error).Throw();
            return error;  // line never reached
        }

        public static Exception LogAndRethrowStubInvocationError(string message,
                                                                 Exception error,
                                                                 Type dynamicInvokerType,
                                                                 Type invokerTargetType,
                                                                 object targetInstance)
        {
            error = CreateErrorIfNoneSpecified(error, dynamicInvokerType, message);

            Type targetInstanceType = targetInstance?.GetType();
            Log.Error(DynamicInvokerLogComponentMoniker,
                      message,
                      error,
                      "DynamicInvokerType",
                      dynamicInvokerType?.Name,
                     $"The invoker's TargetType",
                      invokerTargetType?.AssemblyQualifiedName,
                     $"The invoker's TargetType.Assembly.Location",
                      invokerTargetType?.Assembly?.Location,
                      "targetInstanceType.AssemblyQualifiedName",
                      targetInstanceType?.AssemblyQualifiedName,
                      "targetInstanceType.Assembly.Location",
                      targetInstanceType?.Assembly?.Location);

            ExceptionDispatchInfo.Capture(error).Throw();
            return error;  // line never reached
        }

        private static Exception CreateErrorIfNoneSpecified(Exception error, Type dynamicInvokerType, string message)
        {
            if (error == null)
            {
                try
                {
                    throw new DynamicInvocationException(dynamicInvokerType, message);
                }
                catch(DynamicInvocationException diEx)
                {
                    error = diEx;
                }
            }

            return error;
        }
    }
}
