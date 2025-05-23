// <copyright file="Hresult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tools.Runner.Gac;

internal enum Hresult : int
{
    /// <summary>
    /// Operation successful
    /// </summary>
    S_OK = 0,
    S_FALSE = 1,

    /// <summary>
    /// Not implemented
    /// </summary>
    E_NOTIMPL = unchecked((int)0x80004001),

    /// <summary>
    /// No such interface supported
    /// </summary>
    E_NOINTERFACE = unchecked((int)0x80004002),

    /// <summary>
    /// Pointer that is not valid
    /// </summary>
    E_POINTER = unchecked((int)0x80004003),

    /// <summary>
    /// Operation aborted
    /// </summary>
    E_ABORT = unchecked((int)0x80004004),

    /// <summary>
    /// Unspecified failure
    /// </summary>
    E_FAIL = unchecked((int)0x80004005),

    /// <summary>
    /// Unexpected failure
    /// </summary>
    E_UNEXPECTED = unchecked((int)0x8000FFFF),

    /// <summary>
    /// File not found error
    /// </summary>
    E_FILENOTFOUND = unchecked((int)0x80070002),

    /// <summary>
    /// General access denied error
    /// </summary>
    E_ACCESSDENIED = unchecked((int)0x80070005),

    /// <summary>
    /// Handle that is not valid
    /// </summary>
    E_HANDLE = unchecked((int)0x80070006),

    /// <summary>
    /// Failed to allocate necessary memory
    /// </summary>
    E_OUTOFMEMORY = unchecked((int)0x8007000E),

    /// <summary>
    /// One or more arguments are not valid
    /// </summary>
    E_INVALIDARG = unchecked((int)0x80070057),

    /// <summary>
    /// Strong name signature could not be verified. The assembly may have been tampered with, or it was delay signed but not fully signed with the correct private key.
    /// </summary>
    E_INVALIDSTRONGNAME = unchecked((int)0x80131045),

    /// <summary>
    /// The given assembly name or codebase was invalid.
    /// </summary>
    E_INVALIDASSEMBLY = unchecked((int)0x80131047)
}
