namespace Datadog.Trace.Tools.AotProcessor.Interfaces;

internal readonly struct HResult
{
    public const int S_OK = 0;
    public const int S_FALSE = 1;
    public const int E_NOTIMPL = unchecked((int)0x80004001);
    public const int E_NOINTERFACE = unchecked((int)0x80004002);
    public const int E_POINTER = unchecked((int)0x80004003);
    public const int E_FAIL = unchecked((int)0x80004005);
    public const int E_INVALIDARG = unchecked((int)0x80070057);
    public const int E_RECORD_NOT_FOUND = unchecked((int)0x80131130);
    public const int CORPROF_E_UNSUPPORTED_CALL_SEQUENCE = unchecked((int)0x80131363);

    public HResult(int hr) => Code = hr;

    public bool IsOK => Code == S_OK;

    public bool Succeeded => Code >= 0;
    public bool Failed => Code < 0;

    public int Code { get; }

    public static implicit operator HResult(int hr) => new(hr);

    /// <summary>
    /// Helper to convert to int for comparisons.
    /// </summary>
    public static implicit operator int(HResult hr) => (int)hr.Code;

    /// <summary>
    /// This makes "if (hr)" equivalent to SUCCEEDED(hr).
    /// </summary>
    public static implicit operator bool(HResult hr) => hr.Code >= 0;

    public static string ToString(int code)
    {
        return code switch
        {
            S_OK => "S_OK",
            S_FALSE => "S_FALSE",
            E_FAIL => "E_FAIL",
            E_INVALIDARG => "E_INVALIDARG",
            E_NOTIMPL => "E_NOTIMPL",
            E_NOINTERFACE => "E_NOINTERFACE",
            CORPROF_E_UNSUPPORTED_CALL_SEQUENCE => "CORPROF_E_UNSUPPORTED_CALL_SEQUENCE",
            _ => $"{code:x8}",
        };
    }

    public override string ToString() => ToString(Code);
}
