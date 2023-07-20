// <copyright file="UriBuilderAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Iast.Dataflow;
using Datadog.Trace.Iast.Propagation;

namespace Hdiv.AST.Aspects.System
{
    /// <summary> UriBuilderAspect class aspects </summary>
    [AspectClass("System,netstandard,System.Runtime.Extensions,System.Runtime")]
    public partial class UriBuilderAspect
    {
        /// <summary>
        /// Taints the UriBuilder if the input parameters are tainted
        /// </summary>
        /// <param name="uriText">the uri</param>
        /// <returns>the result of the original method</returns>
        [AspectCtorReplace("System.UriBuilder::.ctor(System.String)")]
        public static UriBuilder Init(string uriText)
        {
            var result = new UriBuilder(uriText);
            PropagationModuleImpl.PropagateResultWhenInputTainted(result, uriText);
            return result;
        }

        /// <summary>
        /// Taints the UriBuilder if the input parameters are tainted
        /// </summary>
        /// <param name="uri">the uri</param>
        /// <returns>the result of the original method</returns>
        [AspectCtorReplace("System.UriBuilder::.ctor(System.Uri)")]
        public static UriBuilder Init(Uri uri)
        {
            var result = new UriBuilder(uri);
            PropagationModuleImpl.PropagateResultWhenInputTainted(result, uri);
            return result;
        }

        /// <summary>
        /// Taints the UriBuilder if the sensitive input parameters are tainted
        /// </summary>
        /// <param name="uriText">the uri</param>
        /// <param name="host">the host</param>
        /// <returns>the result of the original method</returns>
        [AspectCtorReplace("System.UriBuilder::.ctor(System.String,System.String)")]
        public static UriBuilder Init(string uriText, string host)
        {
            var result = new UriBuilder(uriText, host);
            PropagationModuleImpl.PropagateResultWhenInputTainted(result, uriText, host);
            return result;
        }

        /// <summary>
        /// Taints the UriBuilder if the sensitive input parameters are tainted
        /// </summary>
        /// <param name="uriText">the uri</param>
        /// <param name="host">the host</param>
        /// <param name="port">the port</param>
        /// <returns>the result of the original method</returns>
        [AspectCtorReplace("System.UriBuilder::.ctor(System.String,System.String,System.Int32)")]
        public static UriBuilder Init(string uriText, string host, int port)
        {
            var result = new UriBuilder(uriText, host, port);
            PropagationModuleImpl.PropagateResultWhenInputTainted(result, uriText, host);
            return result;
        }

        /// <summary>
        /// Taints the UriBuilder if the sensitive input parameters are tainted
        /// </summary>
        /// <param name="uriText">the uri</param>
        /// <param name="host">the host</param>
        /// <param name="port">the port</param>
        /// <param name="path">the path</param>
        /// <returns>the result of the original method</returns>
        [AspectCtorReplace("System.UriBuilder::.ctor(System.String,System.String,System.Int32,System.String)")]
        public static UriBuilder Init(string uriText, string host, int port, string path)
        {
            var result = new UriBuilder(uriText, host, port, path);
            PropagationModuleImpl.PropagateResultWhenInputTainted(result, uriText, host, path);
            return result;
        }

        /// <summary>
        /// Taints the UriBuilder if the sensitive input parameters are tainted
        /// </summary>
        /// <param name="uriText">the uri</param>
        /// <param name="host">the host</param>
        /// <param name="port">the port</param>
        /// <param name="path">the path</param>
        /// <param name="extra">the extra parameter</param>
        /// <returns>the result of the original method</returns>
        [AspectCtorReplace("System.UriBuilder::.ctor(System.String,System.String,System.Int32,System.String,System.String)")]
        public static UriBuilder Init(string uriText, string host, int port, string path, string extra)
        {
            var result = new UriBuilder(uriText, host, port, path, extra);
            PropagationModuleImpl.PropagateResultWhenInputTainted(result, uriText, host, path, extra);
            return result;
        }

        /// <summary>
        /// Taints the UriBuilder if the instance is tainted
        /// </summary>
        /// <param name="instance">the UriBuilder instance</param>
        /// <returns>the result of the original method</returns>
        [AspectMethodReplace("System.Object::ToString()", "System.UriBuilder")]
        public static string ToString(UriBuilder instance)
        {
            var result = instance.ToString();
            PropagationModuleImpl.PropagateResultWhenInputTainted(result, instance);
            return result;
        }

        /// <summary>
        /// Taints the Uri if the instance is tainted
        /// </summary>
        /// <param name="instance">the UriBuilder instance</param>
        /// <returns>the result of the original method</returns>
        [AspectMethodReplace("System.UriBuilder::get_Uri()")]
        public static Uri GetUri(UriBuilder instance)
        {
            var result = instance.Uri;
            PropagationModuleImpl.PropagateResultWhenInputTainted(result, instance);
            return result;
        }
    }
}
