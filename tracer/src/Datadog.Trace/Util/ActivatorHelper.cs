// <copyright file="ActivatorHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Reflection.Emit;
using System.Threading.Tasks;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Util;

internal class ActivatorHelper
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ActivatorHelper>();

    private readonly Type _type;
    private Func<object> _activator;

    public ActivatorHelper(Type type)
    {
        _type = type;
        _activator = DefaultActivator;
        Task.Run(CreateCustomActivator);
    }

    public object CreateInstance() => _activator();

    private object DefaultActivator()
    {
        return Activator.CreateInstance(_type)!;
    }

    private void CreateCustomActivator()
    {
        try
        {
            var ctor = _type.GetConstructor(Type.EmptyTypes)!;

            var createHeadersMethod = new DynamicMethod(
                $"TypeActivator" + _type.Name,
                _type,
                new[] { typeof(object) },
                typeof(DuckType).Module,
                true);

            var il = createHeadersMethod.GetILGenerator();
            il.Emit(OpCodes.Newobj, ctor);
            il.Emit(OpCodes.Ret);

            _activator = (Func<object>)createHeadersMethod.CreateDelegate(typeof(Func<object>), _type);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error Creating the custom activator for: {Type}", _type.FullName);
        }
    }
}
