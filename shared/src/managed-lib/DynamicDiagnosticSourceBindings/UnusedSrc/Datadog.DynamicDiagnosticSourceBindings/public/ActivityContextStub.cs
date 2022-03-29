using System;
using Datadog.Util;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    public struct ActivityContextStub
    {
        public static class Vendors
        {
            public static class Datadog
            {
                public static string CreateW3CId(ulong datadogTraceId, ulong datadogSpanId, bool isSampled)
                {
                    string w3cId;
                    unsafe
                    {
                        char* template = stackalloc char[55] { '0', '0', '-',
                                                               'd', 'a', '7', 'a', 'd', '0', '9', '0', '0', '0', '0', '0', '0', '0', '0', '0',
                                                               '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '-', 
                                                               '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '-',
                                                               '0', '0'};
                        w3cId = ActivityContextStub.CreateW3CIdCore(datadogTraceId, datadogSpanId, isSampled, template);
                    }

                    return w3cId;
                }

                public static ActivityContextStub CreateNew(ulong datadogTraceId, ulong datadogSpanId, bool isSampled, string traceState = null, bool isRemote = false)
                {
                    string w3cId = ActivityContextStub.Vendors.Datadog.CreateW3CId(datadogTraceId, datadogSpanId, isSampled);
                    if (w3cId == null)
                    {
                        return default(ActivityContextStub);
                    }

                    var aCtx = new ActivityContextStub(w3cId, traceState, isRemote);
                    return aCtx;
                }
            }
        }

        public static ActivityContextStub CreateNew(string traceId, string spanId, bool isSampled, string traceState = null, bool isRemote = false)
        {
            string w3cId = CreateW3CId(traceId, spanId, isSampled);
            if (w3cId == null)
            {
                return default(ActivityContextStub);
            }

            var aCtx = new ActivityContextStub(w3cId, traceState, isRemote);
            return aCtx;
        }

        public static ActivityContextStub CreateNew(ulong traceId, ulong spanId, bool isSampled, string traceState = null, bool isRemote = false)
        {
            string w3cId = CreateW3CId(traceId, spanId, isSampled);
            if (w3cId == null)
            {
                return default(ActivityContextStub);
            }

            var aCtx = new ActivityContextStub(w3cId, traceState, isRemote);
            return aCtx;
        }

        public static string CreateW3CId(string traceId, string spanId, bool isSampled)
        {
            if (isSampled == false && traceId == null && spanId == null)
            {
                return null;
            }

            if (traceId == null)
            {
                throw new ArgumentException($"Either ALL of {nameof(traceId)}, {nameof(spanId)}, {nameof(isSampled)} must"
                                          + $" be not set (i.e. = 0), or {nameof(traceId)} may not be zero.");
            }

            if (spanId == null)
            {
                throw new ArgumentException($"Either ALL of {nameof(traceId)}, {nameof(spanId)}, {nameof(isSampled)} must"
                                          + $" be not set (i.e. = 0), or {nameof(spanId)} may not be zero.");
            }

            if (traceId.Length < 1 || 32 < traceId.Length)
            {
                throw new ArgumentException($"The specified {nameof(traceId)} was expected to have between 1 and 32 characters,"
                                          + $" but it actually contains {traceId.Length} characters.");
            }

            if (spanId.Length < 1 || 16 < spanId.Length)
            {
                throw new ArgumentException($"The specified {nameof(spanId)} was expected to have between 1 and 16 characters,"
                                          + $" but it actually contains {spanId.Length} characters.");
            }

            return CreateW3CIdCore(traceId, spanId, isSampled);
        }

        public static string CreateW3CId(ulong traceId, ulong spanId, bool isSampled)
        {
            string w3cId;
            unsafe
            {
                char* template = stackalloc char[55] { '0', '0', '-',
                                                       '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0',
                                                       '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '-', 
                                                       '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '-',
                                                       '0', '0'};
                w3cId = CreateW3CIdCore(traceId, spanId, isSampled, template);
            }

            return w3cId;
        }

        private static unsafe string CreateW3CIdCore(string traceId, string spanId, bool isSampled)
        {
            char* template = stackalloc char[55] { '0', '0', '-',
                                                   '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0',
                                                   '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '-', 
                                                   '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '-',
                                                   '0', '0'};

            int tPos = 34;
            for (int sPos = traceId.Length - 1; sPos >= 0; sPos--)
            {
                char c = traceId[sPos];
                if (!Format.IsLowerHexChar(c))
                {
                    throw new ArgumentException($"The specified {nameof(traceId)} was expected to only contain lower-case hex characters;"
                                              + $" however, it contains '{c}' at position {sPos} (specified {nameof(traceId)}=\"{traceId}\").");
                }

                *(template + tPos) = c;
                tPos--;
            }

            tPos = 51;
            for (int sPos = spanId.Length - 1; sPos >= 0; sPos--)
            {
                char c = spanId[sPos];
                if (!Format.IsLowerHexChar(c))
                {
                    throw new ArgumentException($"The specified {nameof(spanId)} was expected to only contain lower-case hex characters;"
                                              + $" however, it contains '{c}' at position {sPos} (specified {nameof(spanId)}=\"{spanId}\").");
                }

                *(template + tPos) = c;
                tPos--;
            }

            if (isSampled)
            {
                *(template + 54) = '1';
            }

            return new String(template, 0, 55);
        }

        private static unsafe string CreateW3CIdCore(ulong traceId, ulong spanId, bool isSampled, char* template)
        {
            if (isSampled == false && traceId == 0 && spanId == 0)
            {
                return null;
            }

            if (traceId == 0)
            {
                throw new ArgumentException($"Either ALL of {nameof(traceId)}, {nameof(spanId)}, {nameof(isSampled)} must"
                                          + $" be not set (i.e. = 0), or {nameof(traceId)} may not be zero.");
            }

            if (spanId == 0)
            {
                throw new ArgumentException($"Either ALL of {nameof(traceId)}, {nameof(spanId)}, {nameof(isSampled)} must"
                                          + $" be not set (i.e. = 0), or {nameof(spanId)} may not be zero.");
            }

            const ulong LastCharMask = 0x000000000000000F;
            const int charABase = ((int) 'a') - 10;
            const int char0Base = (int) '0';

            int pos = 34;
            while (traceId > 0)
            {
                int v = (int) (traceId & LastCharMask);
                *(template + pos) = (v < 10) ? (char) (v + char0Base) : (char) (v + charABase);

                pos--;
                traceId >>= 4;
            }

            pos = 51;
            while (spanId > 0)
            {
                int v = (int) (spanId & LastCharMask);
                *(template + pos) = (v < 10) ? (char)(v + char0Base) : (char)(v + charABase);

                pos--;
                spanId >>= 4;
            }

            if (isSampled)
            {
                *(template + 54) = '1';
            }

            return new String(template, 0, 55);
        }


        private readonly string _w3cTraceContext;
        private readonly string _traceState;
        private readonly bool _isRemote;

        private ActivityContextStub(string w3cTraceContext, string traceState, bool isRemote)
        {
            Validate.NotNullOrWhitespace(w3cTraceContext, nameof(w3cTraceContext));

            _w3cTraceContext = w3cTraceContext;
            _traceState = traceState;
            _isRemote = isRemote;
        }

        public bool IsNotInitialized()
        {
            return _w3cTraceContext == null && _traceState == null && _isRemote == false;
        }

        public bool IsRemote { get { return _isRemote; } }

        public string SpanIdHexString
        {
            get
            {
                if (_w3cTraceContext == null)
                {
                    return null;
                }

                return _w3cTraceContext.Substring(36, 16);
            }
        }

        public string TraceIdHexString
        {
            get
            {
                if (_w3cTraceContext == null)
                {
                    return null;
                }

                return _w3cTraceContext.Substring(3, 32);
            }
        }

        public string W3CTraceContext
        {
            get
            {
                return _w3cTraceContext;
            }
        }
    }
}
