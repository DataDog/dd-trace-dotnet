// <copyright file="AssemblyInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: System.Reflection.AssemblyVersionAttribute("1.0.0.0")]

[assembly: TypeForwardedToAttribute(typeof(Datadog.Trace.AspNet.TracingHttpModule))]
