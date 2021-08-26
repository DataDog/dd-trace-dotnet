using System;
using System.Text;

namespace Datadog.Util
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0049:Simplify Names", Justification = "Uses Framework type names for correspondance with native types.")]
    public static class HResult
    {
#pragma warning disable IDE1006  // Non-CodeStyle-conform names required here {
        public const UInt32 S_OK = 0x00000000;
        public const UInt32 E_ABORT = 0x80004004;               // Operation aborted
        public const UInt32 E_FAIL = 0x80004005;                // Unspecified failure
        public const UInt32 E_NOTIMPL = 0x80004001;             // Not implemented
        public const UInt32 E_POINTER = 0x80004003;             // Pointer that is not valid
        public const UInt32 E_UNEXPECTED = 0x8000FFFF;          // Unexpected/catastrophic failure
        public const UInt32 E_OUTOFMEMORY = 0x8007000E;         // Ran out of memory    
        public const UInt32 E_INVALIDARG = 0x80070057;          // One or more arguments are invalid
        public const UInt32 E_ACCESSDENIED = 0x80070005;        // General access denied error
        public const UInt32 E_PENDING = 0x8000000A;             // The data necessary to complete this operation is not yet available.
        public const UInt32 E_BOUNDS = 0x8000000B;              // The operation attempted to access data outside the valid range
        public const UInt32 E_CHANGED_STATE = 0x8000000C;       // A concurrent operation changed the state of the object, invalidating this operation.
        public const UInt32 E_ILLEGAL_STATE_CHANGE = 0x8000000D;// An illegal state change was requested.
        public const UInt32 E_ILLEGAL_METHOD_CALL = 0x8000000E; // A method was called at an unexpected time.
#pragma warning restore IDE1006  // } Non-CodeStyle-conform names required here

        private const UInt32 SeverityBitMask = 0x80000000;

        public static bool IsSuccess(UInt32 hr)
        {
            return (hr & SeverityBitMask) == 0;
        }
        public static bool IsSuccess(Int32 hr)
        {
            return (hr & SeverityBitMask) == 0;
        }

        public static bool IsFailure(UInt32 hr)
        {
            return (hr & SeverityBitMask) != 0;
        }

        public static bool IsFailure(Int32 hr)
        {
            return (hr & SeverityBitMask) != 0;
        }

        public static string ToString(Int32 hr)
        {
            return ToString((UInt32) hr);
        }

        public static string ToString(UInt32 hr)
        {
            switch (hr)
            {
                case S_OK:
                    return "S_OK";

                case E_ABORT:
                    return "E_ABORT";
                case E_FAIL:
                    return "E_FAIL";

                case E_NOTIMPL:
                    return "E_NOTIMPL";

                case E_POINTER:
                    return "E_POINTER";

                case E_UNEXPECTED:
                    return "E_UNEXPECTED";

                case E_OUTOFMEMORY:
                    return "E_OUTOFMEMORY";

                case E_INVALIDARG:
                    return "E_INVALIDARG";

                case E_ACCESSDENIED:
                    return "E_ACCESSDENIED";

                case E_PENDING:
                    return "E_PENDING";

                case E_BOUNDS:
                    return "E_BOUNDS";

                case E_CHANGED_STATE:
                    return "E_CHANGED_STATE";

                case E_ILLEGAL_STATE_CHANGE:
                    return "E_ILLEGAL_STATE_CHANGE";

                case E_ILLEGAL_METHOD_CALL:
                    return "E_ILLEGAL_METHOD_CALL";

                default:
                    if (IsSuccess(hr))
                    {
                        return "UnknownCode_Success";
                    }
                    else
                    {
                        return "UnknownCode_Failure";
                    }
            }
        }

        public static string ToStringWithCode(Int32 hr)
        {
            return ToStringWithCode((UInt32) hr);
        }

        public static string ToStringWithCode(UInt32 hr)
        {
            var s = new StringBuilder(ToString(hr));
            s.Append(" (0x");
            s.Append(hr.ToString("X8"));
            s.Append(')');
            return s.ToString();
        }

        public static UInt32 GetFailureCode(Exception ex)
        {
            if (ex == null)
            {
                return HResult.E_FAIL;
            }

            uint hr = (UInt32) ex.HResult;
            return HResult.IsFailure(hr) ? hr : HResult.E_FAIL;
        }
    }
}