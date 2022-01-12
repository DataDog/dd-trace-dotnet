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
        private string[] handlerTokens;
        private string[] paramTypeArray;

        internal LambdaHandler(string handlerName)
        {
            string[] stringSeparators = new string[] { "::" };
            handlerTokens = handlerName.Split(stringSeparators, StringSplitOptions.None);
            paramTypeArray = BuidParamTypeArray();
        }

        internal string GetAssembly()
        {
            return handlerTokens[0];
        }

        internal string GetFullType()
        {
            return handlerTokens[1];
        }

        internal string GetMethodName()
        {
            return handlerTokens[2];
        }

        internal string[] GetParamTypeArray()
        {
            return paramTypeArray;
        }

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
