// <copyright file="IpcDualChannel.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Serialization;

namespace Datadog.Trace.Ci.Ipc;

internal abstract class IpcDualChannel : IDisposable
{
    private readonly IChannel _recvChannel;
    private readonly IChannelReceiver _recvChannelReceiver;
    private readonly IChannel _sendChannel;
    private readonly IChannelWriter _sendChannelWriter;
    private readonly JsonSerializerSettings _serializerSettings;

    protected IpcDualChannel(string recvName, string sendName)
    {
        _recvChannel = new CircularChannel(recvName);
        _recvChannelReceiver = _recvChannel.GetReceiver();
        _recvChannelReceiver.MessageReceived += OnMessageReceived;

        _sendChannel = new CircularChannel(sendName);
        _sendChannelWriter = _sendChannel.GetWriter();

        _serializerSettings = new JsonSerializerSettings()
        {
            TypeNameHandling = TypeNameHandling.All,
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore,
            SerializationBinder = new CustomSerializationBinder(),
        };
    }

    public event EventHandler<object>? MessageReceived;

    private void OnMessageReceived(object? sender, byte[] data)
    {
        var jsonMessage = Util.EncodingHelpers.Utf8NoBom.GetString(data);
        var message = JsonConvert.DeserializeObject(jsonMessage, _serializerSettings);
        if (message != null)
        {
            MessageReceived?.Invoke(sender, message);
        }
    }

    public bool TrySendMessage<T>(T message)
    {
        var jsonMessage = JsonConvert.SerializeObject(message, _serializerSettings);
        var bytes = Util.EncodingHelpers.Utf8NoBom.GetBytes(jsonMessage);
        return _sendChannelWriter.TryWrite(bytes);
    }

    public void Dispose()
    {
        _recvChannel.Dispose();
        _sendChannel.Dispose();
    }

    private class CustomSerializationBinder : ISerializationBinder
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
}
