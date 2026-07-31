// <copyright file="IWafLibraryInvoker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;

namespace Datadog.Trace.AppSec.Waf.NativeBindings;

internal interface IWafLibraryInvoker
{
    void ContextDestroy(IntPtr handle);

    void SubcontextDestroy(IntPtr handle);

    /// <summary>
    /// Releases an object built with the given allocator. Passing a different allocator than the one
    /// the object was created with corrupts the heap.
    /// </summary>
    void ObjectDestroy(ref DdwafObjectStruct input, IntPtr alloc);

    /// <summary>
    /// Releases an object built with the default allocator, which covers everything libddwaf
    /// allocates on our behalf (eval results, diagnostics).
    /// </summary>
    void ObjectDestroy(ref DdwafObjectStruct input);
}
