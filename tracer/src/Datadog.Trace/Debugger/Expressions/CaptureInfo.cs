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
    public CaptureInfo(
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

    public TCapture Value { get; }

    public Type Type { get; }

    public string Name { get; }

    public MethodBase Method { get; }

    public Type InvocationTargetType { get; }

    public int? LocalsCount { get; }

    public int? ArgumentsCount { get; }

    public ScopeMemberKind MemberKind { get; }

    public MethodState MethodState { get; }

    public bool? HasLocalOrArgument { get; }

    public AsyncCaptureInfo AsyncCaptureInfo { get; }

    public LineCaptureInfo LineCaptureInfo { get; }

    internal bool IsAsyncCapture()
    {
        return AsyncCaptureInfo.KickoffInvocationTargetType != null;
    }
}

internal readonly ref struct AsyncCaptureInfo
{
    public AsyncCaptureInfo(
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

    public object MoveNextInvocationTarget { get; }

    public object KickoffInvocationTarget { get; }

    public Type MoveNextInvocationTargetType { get; }

    public Type KickoffInvocationTargetType { get; }

    public MethodBase KickoffMethod { get; }

    public FieldInfo[] HoistedArguments { get; }

    public AsyncHelper.FieldInfoNameSanitized[] HoistedLocals { get; }
}

internal readonly ref struct LineCaptureInfo
{
    public LineCaptureInfo(int lineNumber, string probeFilePath)
    {
        LineNumber = lineNumber;
        ProbeFilePath = probeFilePath;
    }

    public int LineNumber { get; }

    public string ProbeFilePath { get; }
}
