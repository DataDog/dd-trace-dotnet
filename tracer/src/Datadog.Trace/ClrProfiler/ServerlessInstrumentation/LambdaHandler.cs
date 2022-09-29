// <copyright file="LambdaHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;

namespace Datadog.Trace.ClrProfiler.ServerlessInstrumentation
{
    internal class LambdaHandler
    {
        private static readonly string[] Separator = { "::" };

        internal LambdaHandler(string handlerName)
        {
            var handlerTokens = handlerName.Split(Separator, StringSplitOptions.None);
            Assembly = handlerTokens[0];
            FullType = handlerTokens[1];
            MethodName = handlerTokens[2];
            ParamTypeArray = BuildParamTypeArray();
        }

        internal string[] ParamTypeArray { get; }

        internal string Assembly { get; }

        internal string FullType { get; }

        internal string MethodName { get; }

        private string[] BuildParamTypeArray()
        {
            Type myClassType = Type.GetType($"{FullType},{Assembly}");
            MethodInfo customerMethodInfo = myClassType.GetMethod(MethodName);
            ParameterInfo[] methodParameters = customerMethodInfo.GetParameters();

            string[] paramType = new string[methodParameters.Length + 1];
            paramType[0] = customerMethodInfo.ReturnType.FullName;
            for (var i = 0; i < methodParameters.Length; i++)
            {
                paramType[i + 1] = methodParameters[i].ParameterType.FullName;
            }

            return paramType;
        }
    }
}
