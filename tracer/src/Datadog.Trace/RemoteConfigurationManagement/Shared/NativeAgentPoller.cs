// <copyright file="NativeAgentPoller.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.RemoteConfigurationManagement.Transport;

namespace Datadog.Trace.RemoteConfigurationManagement.Shared;

internal sealed class NativeAgentPoller(string libraryPath, RcmSubscriptionManager subscriptions) : IDisposable
{
    internal const string LibraryPathSetting = "Datadog.Trace.SharedRemoteConfig.NativeLibrary";

    private const string ApmProduct = DynamicConfigurationManager.ProductName;
    private const string ResponseTooLarge = "Agent Remote Configuration response exceeded the size limit";
    private const int MaxPooledResponseBytes = 256 * 1024;
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<NativeAgentPoller>();
    private static readonly ArrayPool<byte> TransportBuffers = ArrayPool<byte>.Create(MaxPooledResponseBytes, maxArraysPerBucket: 1);

    private readonly string _libraryPath = libraryPath;
    private readonly RcmSubscriptionManager _subscriptions = subscriptions;
    private readonly string _clientId = Guid.NewGuid().ToString();
    private Reactor? _reactor;
    private Reactor.WatchOperations? _watchCalls;
    private Subscription? _watchOwner;
    private int _watch;
    private int _client;
    private bool _activationFailed;
    private bool _resetTarget;
    private bool _reconcile = subscriptions.HasDeliveredPaths();
    private IReadOnlyList<ISubscription>? _observedSubscriptions;
    private HashSet<string>? _rawReplayProducts;
    private long _targetGeneration;
    private Identity? _identity;
    private ICollection<string>? _products;
    private byte[]? _capabilityBits;

    private enum FetchFailure
    {
        None,
        Transport,
        TooLarge,
    }

    public void Dispose() => _reactor?.Dispose();

    internal static bool HasSameTarget(RcmClientTracer previous, RcmClientTracer current)
    {
        var before = CreateTarget(previous);
        var after = CreateTarget(current);
        return before.Service == after.Service &&
               before.Env == after.Env &&
               before.AppVersion == after.AppVersion &&
               (ReferenceEquals(before.ProcessTags, after.ProcessTags) || before.ProcessTags.SequenceEqual(after.ProcessTags));
    }

    internal async Task Poll(IRemoteConfigurationApi api, string endpoint, RcmClientTracer tracer, Task stop, Func<bool> isCurrent, Func<Func<bool>, bool> tryCommitResponse)
    {
        if (_activationFailed || stop.IsCompleted || !isCurrent())
        {
            return;
        }

        var reactor = _reactor ?? Activate();
        var pollOutstanding = false;
        ArraySegment<byte> request = default;
        try
        {
            var (subscriptions, owner) = PreparePoll(reactor, tracer);
            if (!_reconcile && _rawReplayProducts is { Count: > 0 } products &&
                !await ApplySnapshot(reactor, subscriptions, stop, isCurrent, tryCommitResponse, products).ConfigureAwait(false))
            {
                if (stop.IsCompleted || !isCurrent())
                {
                    return;
                }

                // Let the Agent advance even when a callback changes registrations during replay.
                (subscriptions, owner) = PreparePoll(reactor, tracer);
            }

            reactor.Check(reactor.Poll(_client));
            pollOutstanding = true;
            var fetchFailure = FetchFailure.None;
            var firstEvent = reactor.Check(reactor.Event(_client));
            if (firstEvent != 0)
            {
                var http = ReadEvent(reactor, firstEvent, "http");
                request = reactor.TakePooled(http.GetProperty("body").GetInt32(), TransportBuffers);
                if (stop.IsCompleted || !isCurrent())
                {
                    reactor.Check(reactor.Cancel(_client));
                    DiscardPoll(reactor);
                    return;
                }

                var operation = http.GetProperty("id").GetInt32();
                var timer = ReadEvent(reactor, reactor.Check(reactor.Event(_client)), "sleep");
                var timerId = timer.GetProperty("id").GetInt32();
                using var cancellation = new CancellationTokenSource();
                var deadline = Task.Delay(timer.GetProperty("delayMs").GetInt32(), cancellation.Token);
                var pending = Fetch(api, endpoint, request, cancellation.Token);
                request = default;
                var completed = pending.IsCompleted ? pending : await Task.WhenAny(pending, deadline, stop).ConfigureAwait(false);
                cancellation.Cancel();
                if (stop.IsCompleted || !isCurrent() || completed == deadline)
                {
                    try
                    {
                        if (completed == deadline && !stop.IsCompleted && isCurrent())
                        {
                            reactor.Check(reactor.TimerElapsed(_client, timerId));
                        }
                        else
                        {
                            reactor.Check(reactor.Cancel(_client));
                        }

                        DiscardPoll(reactor);
                        pollOutstanding = false;
                    }
                    finally
                    {
                        (await pending.ConfigureAwait(false)).Dispose();
                    }

                    return;
                }

                using var reply = await pending.ConfigureAwait(false);
                fetchFailure = reply.Failure;
                if (fetchFailure != FetchFailure.None)
                {
                    var reason = fetchFailure == FetchFailure.TooLarge ? ResponseTooLarge : "Agent transport failed";
                    reactor.WithText(reason, message => reactor.HttpFail(_client, operation, message));
                }
                else
                {
                    reply.Complete(reactor, _client, operation);
                }

                DrainEvents(reactor);
            }

            var resultHandle = reactor.Check(reactor.Result(_client));
            if (resultHandle == 0)
            {
                throw new InvalidDataException("Remote Config poll did not produce a result");
            }

            pollOutstanding = false;
            var result = reactor.TakeJson(resultHandle);
            var batch = result.GetProperty("batch").GetInt32();
            var active = !stop.IsCompleted && isCurrent();
            if (batch != 0)
            {
                try
                {
                    if (!active)
                    {
                        _resetTarget = true;
                    }
                    else if (!_reconcile && !CanSkipRawBatch(result))
                    {
                        await ApplyBatch(reactor, batch, subscriptions, stop, isCurrent, tryCommitResponse).ConfigureAwait(false);
                    }
                }
                finally
                {
                    reactor.Check(reactor.Release(batch));
                }
            }
            else if (active && result.TryGetProperty("error", out _))
            {
                if (fetchFailure == FetchFailure.Transport)
                {
                    Log.Debug("Failed to read the Agent's Remote Configuration response");
                    return;
                }

                if (fetchFailure == FetchFailure.TooLarge)
                {
                    Log.Debug(ResponseTooLarge);
                    return;
                }

                throw new RemoteConfigurationException("The shared session rejected the Agent poll");
            }

            if (active && (_reconcile || owner is not null) && result.GetProperty("ready").GetBoolean())
            {
                if (_reconcile)
                {
                    await ApplySnapshot(reactor, subscriptions, stop, isCurrent, tryCommitResponse).ConfigureAwait(false);
                }

                if (!_resetTarget && owner is { InstallSnapshot: { } install } && !stop.IsCompleted && isCurrent())
                {
                    ApplyWatched(reactor, owner, install, stop, isCurrent, tryCommitResponse);
                }
            }
        }
        catch
        {
            if (_activationFailed)
            {
                _reactor = null;
                reactor.Dispose();
            }
            else if (pollOutstanding)
            {
                reactor.Check(reactor.Cancel(_client));
                DiscardPoll(reactor);
            }

            throw;
        }
        finally
        {
            ReturnBuffer(request.Array);
        }
    }

    private static JsonElement ReadEvent(Reactor reactor, int handle, string kind)
    {
        if (handle != 0)
        {
            var next = reactor.TakeJson(handle);
            if (next.GetProperty("kind").GetString() == kind)
            {
                return next;
            }

            if (next.TryGetProperty("body", out var body))
            {
                reactor.Free(body.GetInt32());
            }
        }

        throw new InvalidDataException("Unexpected Remote Config host operation");
    }

    private static void WriteDiagnostic(ArrayBufferWriter<byte> output, string diagnostic, int limit)
    {
        const string OversizedDiagnostic = "Diagnostic too large.";
        var length = diagnostic.Length <= limit ? Encoding.UTF8.GetByteCount(diagnostic) : limit + 1;
        if (length > limit)
        {
            diagnostic = limit >= OversizedDiagnostic.Length ? OversizedDiagnostic : string.Empty;
            length = diagnostic.Length;
        }

        var span = output.GetSpan(4 + length);
        BinaryPrimitives.WriteInt32LittleEndian(span, length);
        Encoding.UTF8.GetBytes(diagnostic.AsSpan(), span[4..]);
        output.Advance(4 + length);
    }

    private static ChangeRecord<TFile>[] Describe<TFile>(Reactor reactor, int view)
        => reactor.TakeJson<BatchDescriptor<TFile>>(reactor.Check(reactor.Describe(view))).Records
           ?? throw new InvalidDataException("Remote Config view is missing its records");

    private static RemoteConfiguration ReadFile(Reactor reactor, int view, int index, RemoteConfigurationPath path, FileDescriptor file)
    {
        var content = reactor.Take(reactor.Check(reactor.File(view, index, 0)));
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in file.Hashes)
        {
            hashes[pair[0]] = pair[1];
        }

        var version = long.Parse(file.Version, CultureInfo.InvariantCulture);
        return new RemoteConfiguration(path, content, content.Length, hashes, version);
    }

    private static void Add<T>(Dictionary<string, List<T>> entries, string product, T entry)
    {
        if (!entries.TryGetValue(product, out var list))
        {
            list = new List<T>();
            entries.Add(product, list);
        }

        list.Add(entry);
    }

    private static async Task<Reply> Fetch(IRemoteConfigurationApi api, string endpoint, ArraySegment<byte> request, CancellationToken cancellationToken)
    {
        byte[]? rented = null;
        List<ArraySegment<byte>>? rentedSegments = null;
        try
        {
            using var response = await api.PostStreamingAsync(endpoint, request, cancellationToken).ConfigureAwait(false);
            var status = response.StatusCode;
            if (status is < 200 or >= 300)
            {
                return new Reply(status, default);
            }

            var length = response.ContentLength;
            if (length > Reactor.MaxBufferBytes)
            {
                return Reply.TooLarge;
            }

            var stream = await response.GetStreamAsync().ConfigureAwait(false);
            // Transfer-Encoding takes precedence over Content-Length.
            if (length >= 0 && response.GetHeader("Transfer-Encoding") is null)
            {
                if (length == 0)
                {
                    return new Reply(status, default);
                }

                var size = (int)length;
                var storage = AllocateResponseBuffer(size, out rented);
                var destination = storage.AsMemory(0, size);
                var used = 0;
                while (used < destination.Length)
                {
                    var read = await stream.ReadAsync(destination[used..], cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    used += read;
                }

                return new Reply(status, destination[..used], Rented: rented);
            }

            var buffer = ArrayPool<byte>.Shared.Rent(8192);
            try
            {
                var storage = Array.Empty<byte>();
                var used = 0;
                int count;
                while ((count = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) != 0)
                {
                    var total = used + count;
                    if (total > MaxPooledResponseBytes)
                    {
                        rentedSegments = [new(storage, 0, used)];
                        rented = null;
                        var reply = await ReadSegmentedReply(stream, status, rentedSegments, buffer, count, cancellationToken).ConfigureAwait(false);
                        if (reply.Failure != FetchFailure.None)
                        {
                            ReturnResponseSegments(rentedSegments);
                            rentedSegments = null;
                        }

                        return reply;
                    }

                    if (total > storage.Length)
                    {
                        var capacity = Math.Min(MaxPooledResponseBytes, Math.Max(total, storage.Length * 2));
                        var expanded = AllocateResponseBuffer(capacity, out var nextRented);
                        storage.AsSpan(0, used).CopyTo(expanded);
                        ReturnBuffer(rented);
                        rented = nextRented;
                        storage = expanded;
                    }

                    buffer.AsSpan(0, count).CopyTo(storage.AsSpan(used));
                    used = total;
                }

                return new Reply(status, storage.AsMemory(0, used), Rented: rented);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        catch (Exception exception)
        {
            ReturnBuffer(rented);
            if (rentedSegments is not null)
            {
                ReturnResponseSegments(rentedSegments);
            }

            if (exception is IOException or HttpRequestException or OperationCanceledException)
            {
                return Reply.Unavailable;
            }

            throw;
        }
        finally
        {
            ReturnBuffer(request.Array);
        }
    }

    private static async Task<Reply> ReadSegmentedReply(
        Stream stream,
        int status,
        List<ArraySegment<byte>> segments,
        byte[] scratch,
        int firstRead,
        CancellationToken cancellationToken)
    {
        byte[]? segment = null;
        try
        {
            segment = TransportBuffers.Rent(MaxPooledResponseBytes);
            scratch.AsSpan(0, firstRead).CopyTo(segment);
            var used = firstRead;
            var total = segments[0].Count + firstRead;

            while (total < Reactor.MaxBufferBytes)
            {
                if (used == segment.Length)
                {
                    segments.Add(new ArraySegment<byte>(segment));
                    segment = null;
                    segment = TransportBuffers.Rent(MaxPooledResponseBytes);
                    used = 0;
                }

                var remaining = Math.Min(segment.Length - used, Reactor.MaxBufferBytes - total);
                var count = await stream.ReadAsync(segment.AsMemory(used, remaining), cancellationToken).ConfigureAwait(false);
                if (count == 0)
                {
                    break;
                }

                used += count;
                total += count;
            }

            if (total == Reactor.MaxBufferBytes &&
                await stream.ReadAsync(scratch.AsMemory(0, 1), cancellationToken).ConfigureAwait(false) != 0)
            {
                return Reply.TooLarge;
            }

            if (used != 0)
            {
                segments.Add(new ArraySegment<byte>(segment, 0, used));
                segment = null;
            }

            return new Reply(status, default, Segments: segments, SegmentedLength: total);
        }
        finally
        {
            ReturnBuffer(segment);
        }
    }

    private static byte[] AllocateResponseBuffer(int size, out byte[]? rented)
    {
        if (size <= MaxPooledResponseBytes)
        {
            rented = TransportBuffers.Rent(size);
            return rented;
        }

        rented = null;
        return new byte[size];
    }

    private static void ReturnBuffer(byte[]? buffer)
    {
        if (buffer is not null)
        {
            TransportBuffers.Return(buffer);
        }
    }

    private static void ReturnResponseSegments(List<ArraySegment<byte>> segments)
    {
        foreach (var segment in segments)
        {
            ReturnBuffer(segment.Array);
        }
    }

    private static bool TryCommitIfCurrent(Func<Func<bool>, bool> tryCommitResponse, Func<bool> canPublish, Action publish)
        => tryCommitResponse(() =>
        {
            if (!canPublish())
            {
                return false;
            }

            publish();
            return true;
        });

    private static Target CreateTarget(RcmClientTracer tracer)
        => new(tracer.Service ?? string.Empty, tracer.Env ?? string.Empty, tracer.AppVersion ?? string.Empty, tracer.ProcessTags ?? Array.Empty<string>());

    private Reactor Activate()
    {
        _activationFailed = true;
        var reactor = Reactor.OpenNative(_libraryPath);
        _reactor = reactor;
        _activationFailed = false;
        return reactor;
    }

    private void UpdateOptions(Reactor reactor, RcmClientTracer tracer, ICollection<string> products, byte[] bits)
    {
        var tags = tracer.Tags;
        var extraServices = tracer.ExtraServices ?? Array.Empty<string>();
        var runtimeId = tracer.RuntimeId ?? string.Empty;
        var identityChanged = _identity is null || _identity.RuntimeId != runtimeId ||
                              !tags.SequenceEqual(_identity.Tags) || !extraServices.AsSpan().SequenceEqual(_identity.ExtraServices);
        var identity = identityChanged ? new Identity(_clientId, runtimeId, tags.ToArray(), extraServices) : _identity!;
        var interestChanged = !ReferenceEquals(products, _products) ||
                              (!ReferenceEquals(bits, _capabilityBits) && !bits.AsSpan().SequenceEqual(_capabilityBits));

        if (_client == 0)
        {
            _client = reactor.WithJson(
                new
                {
                    target = CreateTarget(tracer),
                    identity,
                    interest = new { products, capabilityIndices = Reactor.GetCapabilityIndices(bits) },
                    language = "dotnet",
                    tracerVersion = tracer.TracerVersion ?? string.Empty,
                    url = "http://127.0.0.1",
                    timeoutMs = RemoteConfigurationApiFactory.AgentTimeoutMs,
                },
                reactor.Open);
        }
        else
        {
            if (tracer.TargetGeneration != _targetGeneration || _resetTarget)
            {
                reactor.WithJson(new { target = CreateTarget(tracer), identity }, options => reactor.Retarget(_client, options));
                _reconcile = true;
                _resetTarget = false;
            }
            else if (identityChanged)
            {
                reactor.WithJson(identity, options => reactor.SetIdentity(_client, options));
            }

            if (interestChanged)
            {
                reactor.WithJson(new { products, capabilityIndices = Reactor.GetCapabilityIndices(bits) }, options => reactor.SetInterest(_client, options));
            }
        }

        _targetGeneration = tracer.TargetGeneration;
        _identity = identity;
        _products = products;
        _capabilityBits = bits;
    }

    private (IReadOnlyList<ISubscription> Subscriptions, Subscription? WatchOwner) PreparePoll(Reactor reactor, RcmClientTracer tracer)
    {
        var interest = _subscriptions.CapturePollInterest();
        var owner = _subscriptions.GetSnapshotSubscription(ApmProduct, interest.Subscriptions);
        QueueChangedProducts(interest.Subscriptions);
        UpdateOptions(reactor, tracer, interest.Products, interest.Capabilities);
        UpdateWatch(reactor, owner);
        if (owner is not null)
        {
            _rawReplayProducts?.Remove(ApmProduct);
        }

        return (interest.Subscriptions, owner);
    }

    private bool CanSkipRawBatch(JsonElement result)
    {
        return _watchOwner is not null
            && ((_products!.Count == 1 && _products.Contains(ApmProduct))
                || (result.TryGetProperty("batchFullyWatched", out var watched) && watched.GetBoolean()));
    }

    private void UpdateWatch(Reactor reactor, Subscription? owner)
    {
        if (ReferenceEquals(_watchOwner, owner))
        {
            return;
        }

        _activationFailed = true;
        if (_watch != 0)
        {
            reactor.Check(_watchCalls!.Close(_client, _watch));
            _watch = 0;
            _watchOwner = null;
        }

        if (owner is not null)
        {
            var calls = _watchCalls ??= reactor.BindWatch();
            _watch = reactor.WithJson(new { delivery = "latestState", products = new[] { ApmProduct } }, options => calls.Open(_client, options));
            _watchOwner = owner;
        }

        _activationFailed = false;
    }

    private void ApplyWatched(Reactor reactor, Subscription owner, SnapshotInstaller install, Task stop, Func<bool> isCurrent, Func<Func<bool>, bool> tryCommitResponse)
    {
        var calls = _watchCalls!;
        var view = reactor.Check(calls.Next(_client, _watch));
        if (view == 0)
        {
            return;
        }

        var watch = _watch;
        var finished = false;
        try
        {
            if (!CanInstall())
            {
                Abandon();
                return;
            }

            var snapshot = reactor.Check(calls.Snapshot(view));
            var configuration = new List<RemoteConfiguration>();
            try
            {
                foreach (var record in Describe<FileDescriptor>(reactor, snapshot))
                {
                    var file = record.File;
                    var path = RemoteConfigurationPath.FromPath(file.Path);
                    configuration.Add(ReadFile(reactor, snapshot, configuration.Count, path, file));
                }
            }
            finally
            {
                reactor.Check(reactor.Release(snapshot));
            }

            var records = Describe<PathDescriptor>(reactor, view);
            var rejections = new Dictionary<string, string>(StringComparer.Ordinal);
            string? failure = null;
            if (!CanInstall())
            {
                Abandon();
                return;
            }

            var commitDeclined = false;
            try
            {
                commitDeclined = !install(configuration, rejections, _targetGeneration, CommitSettings);
            }
            catch (Exception exception)
            {
                Log.Error(exception, "Error while installing shared APM tracing configuration");
                failure = exception.Message;
            }

            if (commitDeclined || !CanInstall())
            {
                Abandon();
                return;
            }

            var count = records.Length;
            var limit = (Reactor.MaxBufferBytes - 5 - (9 * count)) / (count + 1);
            var plan = new ArrayBufferWriter<byte>(1 + (5 * count));
            plan.GetSpan(1)[0] = failure is null ? (byte)0 : (byte)2;
            plan.Advance(1);
            if (failure is not null)
            {
                WriteDiagnostic(plan, failure, limit);
            }

            var index = 0;
            foreach (var record in records)
            {
                if (record.Kind == "upsert")
                {
                    var path = record.File.Path;
                    var rejected = rejections.TryGetValue(path, out var error);
                    var entry = plan.GetSpan(5);
                    BinaryPrimitives.WriteInt32LittleEndian(entry, index);
                    entry[4] = rejected ? (byte)2 : (byte)0;
                    plan.Advance(5);
                    if (rejected)
                    {
                        WriteDiagnostic(plan, error!, limit);
                    }
                }

                index++;
            }

            reactor.WithBytes(plan.WrittenSpan, input => calls.Finish(_client, watch, view, input));
            finished = true;
        }
        finally
        {
            if (!finished)
            {
                _activationFailed = true;
            }
        }

        bool CanInstall() => !stop.IsCompleted && isCurrent() && ReferenceEquals(owner, _subscriptions.GetSnapshotSubscription(ApmProduct));

        bool CommitSettings(Action publish) => TryCommitIfCurrent(tryCommitResponse, CanInstall, publish);

        void Abandon()
        {
            reactor.Check(calls.Close(_client, watch));
            reactor.WithBytes([1], input => calls.Finish(_client, watch, view, input));
            _watch = 0;
            _watchOwner = null;
            _resetTarget = true;
            finished = true;
        }
    }

    private void DrainEvents(Reactor reactor)
    {
        int next;
        while ((next = reactor.Check(reactor.Event(_client))) != 0)
        {
            var operation = reactor.TakeJson(next);
            if (operation.TryGetProperty("body", out var body))
            {
                reactor.Free(body.GetInt32());
            }
        }
    }

    private void DiscardPoll(Reactor reactor)
    {
        DrainEvents(reactor);
        var pending = reactor.Check(reactor.Result(_client));
        if (pending != 0)
        {
            var result = reactor.TakeJson(pending);
            var batch = result.GetProperty("batch").GetInt32();
            if (batch != 0)
            {
                reactor.Check(reactor.Release(batch));
            }
        }
    }

    private void QueueChangedProducts(IReadOnlyList<ISubscription> subscriptions)
    {
        if (_observedSubscriptions is { } previous && !ReferenceEquals(previous, subscriptions))
        {
            var changedProducts = RcmSubscriptionManager.GetProductsWithChangedOwners(previous, subscriptions);
            if (_rawReplayProducts is null)
            {
                _rawReplayProducts = changedProducts;
            }
            else
            {
                _rawReplayProducts.UnionWith(changedProducts);
            }
        }

        _observedSubscriptions = subscriptions;
    }

    private async Task<bool> ApplySnapshot(
        Reactor reactor,
        IReadOnlyList<ISubscription> subscriptions,
        Task stop,
        Func<bool> isCurrent,
        Func<Func<bool>, bool> tryCommitResponse,
        HashSet<string>? products = null)
    {
        var snapshot = reactor.Check(reactor.Snapshot(_client));
        if (snapshot != 0)
        {
            try
            {
                if (!await ApplyBatch(reactor, snapshot, subscriptions, stop, isCurrent, tryCommitResponse, products).ConfigureAwait(false))
                {
                    return false;
                }
            }
            finally
            {
                reactor.Check(reactor.Release(snapshot));
            }
        }

        products?.Clear();
        return true;
    }

    private async Task<bool> ApplyBatch(
        Reactor reactor,
        int batch,
        IReadOnlyList<ISubscription> subscriptions,
        Task stop,
        Func<bool> isCurrent,
        Func<Func<bool>, bool> tryCommitResponse,
        HashSet<string>? replayProducts = null)
    {
        if (stop.IsCompleted || !isCurrent())
        {
            _resetTarget = true;
            return false;
        }

        var reconcileAll = _reconcile;
        var completeAssignment = reconcileAll || replayProducts is not null;
        var upserts = new Dictionary<string, List<RemoteConfiguration>>();
        var removals = new Dictionary<string, List<RemoteConfigurationPath>>();
        var sourceIndices = new Dictionary<string, int>(StringComparer.Ordinal);
        var records = Describe<FileDescriptor>(reactor, batch);
        var index = 0;
        foreach (var record in records)
        {
            var file = record.File;
            var path = RemoteConfigurationPath.FromPath(file.Path);
            if ((_watchOwner is not null && path.Product == ApmProduct) ||
                (replayProducts is not null && !replayProducts.Contains(path.Product)))
            {
                index++;
                continue;
            }

            switch (record.Kind)
            {
                case "remove":
                    Add(removals, path.Product, path);
                    break;
                case "upsert":
                case "snapshot" when completeAssignment:
                    Add(upserts, path.Product, ReadFile(reactor, batch, index, path, file));
                    sourceIndices.Add(path.Path, index);
                    break;
                default:
                    throw new InvalidDataException("Unexpected Remote Config assignment operation");
            }

            index++;
        }

        var assignedPaths = completeAssignment ? new HashSet<string>(sourceIndices.Keys, StringComparer.Ordinal) : null;
        var interrupted = false;
        var results = await _subscriptions.ApplyChanges(
                          upserts, removals, (_, _) => CanDispatch(), CommitChanges, _watchOwner, subscriptions, assignedPaths, replayProducts).ConfigureAwait(false);
        if (!CanDispatch())
        {
            return false;
        }

        _reconcile = false;
        if (reconcileAll)
        {
            _rawReplayProducts?.Clear();
        }

        if (results is null)
        {
            return true;
        }

        foreach (var result in results)
        {
            if (!CanDispatch())
            {
                return false;
            }

            if (!sourceIndices.TryGetValue(result.Filename, out var sourceIndex) ||
                result.ApplyState is not (ApplyStates.ACKNOWLEDGED or ApplyStates.ERROR))
            {
                continue;
            }

            var receipt = reactor.Check(reactor.File(batch, sourceIndex, 0));
            try
            {
                if (result.ApplyState == ApplyStates.ERROR)
                {
                    reactor.WithText(result.Error ?? string.Empty, message => reactor.Apply(_client, receipt, 3, message));
                }
                else
                {
                    reactor.Check(reactor.Apply(_client, receipt, 2, 0));
                }
            }
            finally
            {
                reactor.Free(receipt);
            }
        }

        return true;

        bool CommitChanges(Dictionary<string, List<RemoteConfiguration>> unusedUpserts, Dictionary<string, List<RemoteConfigurationPath>>? unusedRemovals, Action publish) =>
            TryCommitIfCurrent(tryCommitResponse, CanDispatch, publish);

        bool CanDispatch()
        {
            if (interrupted)
            {
                return false;
            }

            if (stop.IsCompleted || !isCurrent())
            {
                _resetTarget = true;
                interrupted = true;
            }
            else if (!_subscriptions.AreSubscriptionsCurrent(subscriptions, upserts, removals, completeAssignment))
            {
                // A raw membership change invalidates these deliveries, not the session's other receipts.
                if (!reconcileAll)
                {
                    _rawReplayProducts ??= new HashSet<string>(StringComparer.Ordinal);
                    _rawReplayProducts.UnionWith(upserts.Keys);
                    _rawReplayProducts.UnionWith(removals.Keys);
                }

                interrupted = true;
            }

            return !interrupted;
        }
    }

    private readonly record struct Target(string Service, string Env, string AppVersion, IReadOnlyCollection<string> ProcessTags);

    private readonly record struct BatchDescriptor<TFile>(ChangeRecord<TFile>[] Records);

    private readonly record struct ChangeRecord<TFile>(string Kind, TFile File);

    private readonly record struct FileDescriptor(string Path, string Version, string[][] Hashes);

    private readonly record struct PathDescriptor(string Path);

    private readonly record struct Reply(
        int Status,
        ReadOnlyMemory<byte> Body,
        FetchFailure Failure = FetchFailure.None,
        byte[]? Rented = null,
        List<ArraySegment<byte>>? Segments = null,
        int SegmentedLength = 0) : IDisposable
    {
        public static readonly Reply Unavailable = new(0, default, FetchFailure.Transport);
        public static readonly Reply TooLarge = new(0, default, FetchFailure.TooLarge);

        public void Complete(Reactor reactor, int client, int operation)
        {
            if (Segments is { } segments)
            {
                reactor.CompleteHttp(client, operation, segments, SegmentedLength, Status);
            }
            else
            {
                reactor.CompleteHttp(client, operation, Body.Span, Status);
            }
        }

        public void Dispose()
        {
            ReturnBuffer(Rented);
            if (Segments is { } segments)
            {
                ReturnResponseSegments(segments);
            }
        }
    }

    private sealed record Identity(string ClientId, string RuntimeId, string[] Tags, string[] ExtraServices);
}
