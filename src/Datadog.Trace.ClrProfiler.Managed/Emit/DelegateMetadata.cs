using System;

namespace Datadog.Trace.ClrProfiler.Emit
{
    internal class DelegateMetadata
    {
        public Type Type { get; set; }

        public Type ReturnType { get; set; }

        public Type[] Generics { get; set; }

        public Type[] Parameters { get; set; }

        public static DelegateMetadata Create<TDelegate>()
            where TDelegate : System.Delegate
        {
            Type delegateType = typeof(TDelegate);
            Type[] genericTypeArguments = delegateType.GenericTypeArguments;

            Type[] parameterTypes;
            Type returnType;

            if (delegateType.Name.StartsWith("Func`"))
            {
                // last generic type argument is the return type
                int parameterCount = genericTypeArguments.Length - 1;
                parameterTypes = new Type[parameterCount];
                Array.Copy(genericTypeArguments, parameterTypes, parameterCount);

                returnType = genericTypeArguments[parameterCount];
            }
            else if (delegateType.Name.StartsWith("Action`"))
            {
                parameterTypes = genericTypeArguments;
                returnType = typeof(void);
            }
            else
            {
                throw new Exception($"Only Func<> or Action<> are supported in {nameof(DelegateMetadata)}.");
            }

            return new DelegateMetadata()
            {
                Generics = genericTypeArguments,
                Parameters = parameterTypes,
                ReturnType = returnType,
                Type = delegateType
            };
        }
    }
}
