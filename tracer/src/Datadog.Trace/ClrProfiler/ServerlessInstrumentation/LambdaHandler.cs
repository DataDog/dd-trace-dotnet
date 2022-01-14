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
        private string[] _handlerTokens;

        internal LambdaHandler(string handlerName)
        {
            _handlerTokens = handlerName.Split(Separator, StringSplitOptions.None);
            ParamTypeArray = BuidParamTypeArray();
        }

        internal string[] ParamTypeArray { get; }

        internal string GetAssembly() => _handlerTokens[0];

        internal string GetFullType() => _handlerTokens[1];

        internal string GetMethodName() => _handlerTokens[2];

        private string[] BuidParamTypeArray()
        {
            Type myClassType = Type.GetType(GetTypeName());
            MethodInfo customerMethodInfo = myClassType.GetMethod(GetMethodName());
            ParameterInfo[] methodParameters = customerMethodInfo.GetParameters();

            string[] paramType = new string[methodParameters.Length + 1];
            paramType[0] = customerMethodInfo.ReturnType.Name;
            for (var i = 0; i < methodParameters.Length; i++)
            {
                paramType[i + 1] = methodParameters[i].ParameterType.ToString();
            }

            return paramType;
        }

        private string GetTypeName()
        {
            return GetFullType() + "," + GetAssembly();
        }
    }
}
