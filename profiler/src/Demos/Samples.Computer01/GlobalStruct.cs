// <copyright file="GlobalStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

#pragma warning disable SA1401 // Fields should be private

// This value type is deliberately declared outside of any namespace: when it is given as the type
// argument of a generic method, the |fg: part of the frame must not be prefixed by a '.' standing
// for the empty namespace.
internal struct GlobalStruct
{
    public int Member;
}

#pragma warning restore SA1401 // Fields should be private
