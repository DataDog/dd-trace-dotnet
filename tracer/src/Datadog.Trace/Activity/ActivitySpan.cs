// <copyright file="ActivitySpan.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// TODO everything in here is essentially hacky

using System;
using Datadog.Trace.Activity.DuckTypes;

namespace Datadog.Trace.Activity
{
    internal class ActivitySpan : ISpan
    {
        private IActivity _activity;
        private IW3CActivity _w3cActivity;
        private IActivity5 _activity5;
        private IActivity6 _activity6;
        private ActivitySpanContext _context;

        public ActivitySpan(IActivity activity)
        {
            _activity = activity;

            if (activity is IW3CActivity wc3Activity)
            {
                _w3cActivity = wc3Activity;
            }

            if (activity is IActivity5 activity5)
            {
                _activity5 = activity5;
            }

            if (activity is IActivity6 activity6)
            {
                _activity6 = activity6;
            }

            _context = new ActivitySpanContext(activity);
        }

        public string OperationName { get => GetOperationName(); set => SetOperationName(value); }

        public string ResourceName { get => GetResourceName(); set => SetResourceName(value); }

        public string Type { get => GetTypeOfSpan(); set => SetTypeOfSpan(value); }

        public bool Error { get => GetError(); set => SetError(value); }

        public string ServiceName { get => GetServiceName(); set => SetServiceName(value); }

        public ulong TraceId { get => GetTraceId(); private set => SetTraceId(value); }

        public ulong SpanId { get => GetSpanId(); private set => SetSpanId(value); }

        public ISpanContext Context => _context;

        public void Dispose()
        {
            _activity.Dispose();
        }

        public void Finish()
        {
            _activity.Stop();
        }

        public void Finish(DateTimeOffset finishTimestamp)
        {
            _activity.SetEndTime(finishTimestamp.DateTime);
            _activity.Stop(); // TODO I think overload for this that takes DateTimeOffset
        }

        public string GetTag(string key)
        {
            // TODO better implementation of this just hacking it in now
            foreach (var tag in _activity.Tags)
            {
                if (tag.Key == key)
                {
                    return tag.Value;
                }
            }

            return null;
        }

        public void SetException(Exception exception)
        {
            if (_activity6 is null)
            {
                // TODO what we do here
            }

            _activity6.SetStatus(ActivityStatusCode.Error, exception.Message);
        }

        public ISpan SetTag(string key, string value)
        {
            _activity.AddTag(key, value);
            return this;
        }

        private string GetOperationName()
        {
            var newName = GetTag("new-operation-name");

            if (newName is null)
            {
                return _activity.OperationName;
            }

            return newName;
        }

        private void SetOperationName(string operationName)
        {
            _activity.AddTag("new-operation-name", operationName);
        }

        private string GetResourceName()
        {
            if (_activity5 is not null)
            {
                return _activity5.DisplayName;
            }

            // we only have operation name
            var newName = GetTag("new-resource-name");

            if (newName is not null)
            {
                return newName;
            }

            // we haven't set a resource name, use operation name
            return GetOperationName();
        }

        private void SetResourceName(string resourceName)
        {
            if (_activity5 is not null)
            {
                _activity5.DisplayName = resourceName;
            }

            // we don't have it so gotta use a tag
            _activity.AddTag("new-resource-name", resourceName);
        }

        private bool GetError()
        {
            throw new NotImplementedException();
        }

        private void SetError(bool error)
        {
            throw new NotImplementedException();
        }

        private void SetTypeOfSpan(string spanType)
        {
            throw new NotImplementedException();
        }

        private string GetTypeOfSpan()
        {
            throw new NotImplementedException();
        }

        private void SetServiceName(string serviceName)
        {
            throw new NotImplementedException();
        }

        private string GetServiceName()
        {
            throw new NotImplementedException();
        }

        private void SetTraceId(ulong value)
        {
            throw new NotImplementedException();
        }

        private ulong GetTraceId()
        {
            return _context.TraceId;
        }

        private void SetSpanId(ulong value)
        {
            throw new NotImplementedException();
        }

        private ulong GetSpanId()
        {
            throw new NotImplementedException();
        }
    }
}
