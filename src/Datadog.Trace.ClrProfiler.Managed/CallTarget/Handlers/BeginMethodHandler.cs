using System;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers
{
    internal static class BeginMethodHandler<TIntegration, TTarget>
    {
        private static readonly InvokeDelegate _invokeDelegate;

        static BeginMethodHandler()
        {
            try
            {
                DynamicMethod dynMethod = IntegrationMapper.CreateBeginMethodDelegate(typeof(TIntegration), typeof(TTarget), Util.ArrayHelper.Empty<Type>());
                if (dynMethod != null)
                {
                    _invokeDelegate = (InvokeDelegate)dynMethod.CreateDelegate(typeof(InvokeDelegate));
                }
            }
            catch (Exception ex)
            {
                throw new CallTargetInvokerException(ex);
            }
            finally
            {
                if (_invokeDelegate is null)
                {
                    _invokeDelegate = instance => CallTargetState.GetDefault();
                }
            }
        }

        internal delegate CallTargetState InvokeDelegate(TTarget instance);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static CallTargetState Invoke(TTarget instance)
        {
            return CallTargetState.WithPreviousScope(Tracer.Instance.ActiveScope, _invokeDelegate(instance));
        }
    }
}
