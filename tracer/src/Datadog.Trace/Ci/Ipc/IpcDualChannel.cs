// <copyright file="IpcDualChannel.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.IO;
using System.Reflection;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Ipc.Messages;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Serialization;

namespace Datadog.Trace.Ci.Ipc;

internal abstract class IpcDualChannel : IDisposable
{
    private readonly CircularChannel _recvChannel;
    private readonly IChannelReader _recvChannelReader;
    private readonly CircularChannel _sendChannel;
    private readonly IChannelWriter _sendChannelWriter;
    private readonly JsonSerializer _jsonSerializer;
    private Action<object>? _callback;

    protected IpcDualChannel(string recvName, string sendName)
    {
        _recvChannel = new CircularChannel(recvName);
        _recvChannelReader = _recvChannel.GetReader();

        _sendChannel = new CircularChannel(sendName);
        _sendChannelWriter = _sendChannel.GetWriter();

        _jsonSerializer = JsonSerializer.Create(new JsonSerializerSettings()
        {
            TypeNameHandling = TypeNameHandling.All,
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new IpcContractResolver(),
            SerializationBinder = new CustomSerializationBinder(),
        });
    }

    public void SetMessageReceivedCallback(Action<object> callback)
    {
        _callback = callback;
        if (_callback is not null)
        {
            _recvChannelReader.SetCallback(OnMessageReceived);
        }
    }

    private void OnMessageReceived(ArraySegment<byte> data)
    {
        using var memoryStream = new MemoryStream(data.Array!, data.Offset, data.Count);
        using var reader = new StreamReader(memoryStream, Util.EncodingHelpers.Utf8NoBom);
        using var jsonReader = new JsonTextReader(reader) { ArrayPool = JsonArrayPool.Shared };
        var message = _jsonSerializer.Deserialize(jsonReader);
        if (message != null)
        {
            _callback?.Invoke(message);
        }
    }

    public bool TrySendMessage(object message)
    {
        var rentedArray = ArrayPool<byte>.Shared.Rent(_sendChannel.BufferBodySize);
        try
        {
            using var memoryStream = new MemoryStream();
            using (var writer = new StreamWriter(memoryStream, Util.EncodingHelpers.Utf8NoBom))
            {
                using var jsonWriter = new JsonTextWriter(writer) { ArrayPool = JsonArrayPool.Shared };
                _jsonSerializer.Serialize(jsonWriter, message);
            }

            memoryStream.TryGetBuffer(out var memoryBuffer);
            return _sendChannelWriter.TryWrite(in memoryBuffer);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedArray);
        }
    }

    public void Dispose()
    {
        _recvChannel.Dispose();
        _sendChannel.Dispose();
        _callback = null;
    }

    private sealed class CustomSerializationBinder : ISerializationBinder
    {
        public Type BindToType(string? assemblyName, string typeName)
        {
            // Let's protect ourselves from deserializing types that we don't want to
            if (assemblyName?.StartsWith("Datadog.Trace") == true)
            {
                return DefaultSerializationBinder.Instance.BindToType(assemblyName, typeName);
            }

            return typeof(void);
        }

        public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
        {
            DefaultSerializationBinder.Instance.BindToName(serializedType, out assemblyName, out typeName);
        }
    }

    private sealed class IpcContractResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);
            if (ShouldSuppressNestedTypeNames(member))
            {
                property.TypeNameHandling = TypeNameHandling.None;
                property.ItemTypeNameHandling = TypeNameHandling.None;
            }

            return property;
        }

        private static bool ShouldSuppressNestedTypeNames(MemberInfo member)
        {
            if (member.DeclaringType == typeof(SessionCodeCoverageMessage))
            {
                return member.Name == nameof(SessionCodeCoverageMessage.BackfillValidation) ||
                       member.Name == nameof(SessionCodeCoverageMessage.SupersededResultIds);
            }

            if (member.DeclaringType == typeof(CodeCoverageBackfillValidation))
            {
                return member.Name == nameof(CodeCoverageBackfillValidation.ExpectedCoveredLinesByBackendPath) ||
                       member.Name == nameof(CodeCoverageBackfillValidation.RequiredBackendPathsWithCoverage) ||
                       member.Name == nameof(CodeCoverageBackfillValidation.RequiredBackendLinesByBackendPath) ||
                       member.Name == nameof(CodeCoverageBackfillValidation.RepresentedBackendLinesByBackendPath) ||
                       member.Name == nameof(CodeCoverageBackfillValidation.LocalCandidateByBackendPath);
            }

            return false;
        }
    }
}
