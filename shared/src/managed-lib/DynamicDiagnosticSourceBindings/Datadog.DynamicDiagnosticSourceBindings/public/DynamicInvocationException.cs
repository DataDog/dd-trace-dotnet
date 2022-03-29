using System;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    public class DynamicInvocationException : Exception
    {
        public DynamicInvocationException(Type dynamicInvokerType, string message)
            : base(ComposeMessage(dynamicInvokerType, message))
        {
            DynamicInvokerType = dynamicInvokerType;
        }

        public DynamicInvocationException(Type dynamicInvokerType, string message, Exception innerException)
            : base(ComposeMessage(dynamicInvokerType, message), innerException)
        {
            DynamicInvokerType = dynamicInvokerType;
        }

        public Type DynamicInvokerType
        {
            get; private set;
        }

        private static string ComposeMessage(Type dynamicInvokerType, string message)
        {
            if (dynamicInvokerType == null)
            {
                return message ?? String.Empty;
            }

            if (message == null)
            {
                return $"DynamicInvokerType: \"{dynamicInvokerType.Name}\".";
            }
            else
            {
                return $"{message} (DynamicInvokerType: \"{dynamicInvokerType.Name}\")";
            }
        }
    }
}
