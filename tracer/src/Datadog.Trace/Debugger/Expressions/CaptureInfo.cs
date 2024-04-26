// <copyright file="CaptureInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using Datadog.Trace.Debugger.Helpers;

namespace Datadog.Trace.Debugger.Expressions;

internal readonly ref struct CaptureInfo<TCapture>
{
    internal CaptureInfo(
        int methodMetadataIndex,
        MethodState methodState,
        TCapture value = default,
        MethodBase method = null,
        Type invocationTargetType = null,
        int? localsCount = null,
        int? argumentsCount = null,
        ScopeMemberKind memberKind = ScopeMemberKind.None,
        Type type = null,
        string name = null,
        bool? hasLocalOrArgument = null,
        LineCaptureInfo lineCaptureInfo = default,
        AsyncCaptureInfo asyncCaptureInfo = default)
    {
        MethodMetadataIndex = methodMetadataIndex;
        Value = value;
        MemberKind = memberKind;
        Type = type ?? value?.GetType() ?? typeof(TCapture);
        Name = name;
        MethodState = methodState;
        HasLocalOrArgument = hasLocalOrArgument;
        LineCaptureInfo = lineCaptureInfo;
        AsyncCaptureInfo = asyncCaptureInfo;
        Method = method;
        InvocationTargetType = invocationTargetType;
        LocalsCount = localsCount;
        ArgumentsCount = argumentsCount;
    }

    internal int MethodMetadataIndex { get; }

    internal TCapture Value { get; }

    internal Type Type { get; }

    internal string Name { get; }

    internal MethodBase Method { get; }

    internal Type InvocationTargetType { get; }

    internal int? LocalsCount { get; }

    internal int? ArgumentsCount { get; }

    internal ScopeMemberKind MemberKind { get; }

    internal MethodState MethodState { get; }

    internal bool? HasLocalOrArgument { get; }

    internal AsyncCaptureInfo AsyncCaptureInfo { get; }

    internal LineCaptureInfo LineCaptureInfo { get; }

    internal bool IsAsyncCapture()
    {
        return AsyncCaptureInfo.KickoffInvocationTargetType != null;
    }
}

internal readonly ref struct AsyncCaptureInfo
{
    internal AsyncCaptureInfo(
        object moveNextInvocationTarget,
        object kickoffInvocationTarget,
        Type kickoffInvocationTargetType,
        MethodBase kickoffMethod = null,
        FieldInfo[] hoistedArgs = null,
        AsyncHelper.FieldInfoNameSanitized[] hoistedLocals = null)
    {
        MoveNextInvocationTarget = moveNextInvocationTarget;
        KickoffInvocationTarget = kickoffInvocationTarget;
        MoveNextInvocationTargetType = moveNextInvocationTarget.GetType();
        KickoffInvocationTargetType = kickoffInvocationTargetType;
        KickoffMethod = kickoffMethod;
        HoistedArguments = hoistedArgs;
        HoistedLocals = hoistedLocals;
    }

    internal object MoveNextInvocationTarget { get; }

    internal object KickoffInvocationTarget { get; }

    internal Type MoveNextInvocationTargetType { get; }

    internal Type KickoffInvocationTargetType { get; }

    internal MethodBase KickoffMethod { get; }

    internal FieldInfo[] HoistedArguments { get; }

    internal AsyncHelper.FieldInfoNameSanitized[] HoistedLocals { get; }
}

internal readonly ref struct LineCaptureInfo
{
    internal LineCaptureInfo(int lineNumber, string probeFilePath)
    {
        LineNumber = lineNumber;
        ProbeFilePath = probeFilePath;
    }

    internal int LineNumber { get; }

    internal string ProbeFilePath { get; }
}
