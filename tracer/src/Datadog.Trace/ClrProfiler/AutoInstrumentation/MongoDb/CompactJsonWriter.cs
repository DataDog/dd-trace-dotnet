// <copyright file="CompactJsonWriter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MongoDB.Bson;
using MongoDB.Bson.IO;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb
{
    /// <summary>
    /// Represents a BSON writer to a TextWriter (in JSON format).
    /// </summary>
    public class CompactJsonWriter : JsonWriter
    {
        private TextWriter _textWriter;
        private JsonWriterContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompactJsonWriter"/> class.
        /// </summary>
        /// <param name="writer">writer</param>
        public CompactJsonWriter(TextWriter writer)
            : base(writer)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CompactJsonWriter"/> class.
        /// </summary>
        /// <param name="writer">writer</param>
        /// <param name="settings">settings</param>
        public CompactJsonWriter(TextWriter writer, JsonWriterSettings settings)
            : base(writer, settings)
        {
            if (writer == null)
            {
                throw new ArgumentNullException("writer");
            }

            _textWriter = writer;
            _context = new JsonWriterContext(null, ContextType.TopLevel, string.Empty);
            State = BsonWriterState.Initial;
        }

        // public properties

        /// <summary>
        /// Gets the base TextWriter.
        /// </summary>
        /// <value>
        /// The base TextWriter.
        /// </value>
        public new TextWriter BaseTextWriter
        {
            get { return _textWriter; }
        }

        /// <summary>
        /// Writes BSON binary data to the writer.
        /// </summary>
        /// <param name="binaryData">The binary data.</param>
        public override void WriteBinaryData(BsonBinaryData binaryData)
        {
            if (Disposed) { throw new ObjectDisposedException("CompactJsonWriter"); }
            if (State != BsonWriterState.Value && State != BsonWriterState.Initial)
            {
                ThrowInvalidState("WriteBinaryData", BsonWriterState.Value, BsonWriterState.Initial);
            }

            var subType = binaryData.SubType;
            var bytes = binaryData.Bytes;
#pragma warning disable 618
            GuidRepresentation guidRepresentation;
            if (BsonDefaults.GuidRepresentationMode == GuidRepresentationMode.V2)
            {
                guidRepresentation = binaryData.GuidRepresentation;
            }
            else
            {
                guidRepresentation = subType == BsonBinarySubType.UuidStandard ? GuidRepresentation.Standard : GuidRepresentation.Unspecified;
            }
#pragma warning restore 618
            WriteNameHelper(Name);
            switch (Settings.OutputMode)
            {
#pragma warning disable 618
                case JsonOutputMode.Strict:
#pragma warning restore 618
                    _textWriter.Write("{{ \"$binary\" : \"{0}\", \"$type\" : \"{1}\" }}", Convert.ToBase64String(bytes), ((int)subType).ToString("x2"));
                    break;

                case JsonOutputMode.CanonicalExtendedJson:
                case JsonOutputMode.RelaxedExtendedJson:
                    _textWriter.Write("{{ \"$binary\" : {{ \"base64\" : \"{0}\", \"subType\" : \"{1}\" }} }}", Convert.ToBase64String(bytes), ((int)subType).ToString("x2"));
                    break;

                case JsonOutputMode.Shell:
                default:
                    switch (subType)
                    {
                        case BsonBinarySubType.UuidLegacy:
                        case BsonBinarySubType.UuidStandard:
                            _textWriter.Write(GuidToString(subType, bytes, guidRepresentation));
                            break;

                        default:
                            _textWriter.Write("new BinData({0}, \"{1}\")", (int)subType, Convert.ToBase64String(bytes));
                            break;
                    }

                    break;
            }

            State = GetNextState();
        }

        // Private methods used in based class
        private static bool NeedsEscaping(char c)
        {
            switch (c)
            {
                case '"':
                case '\\':
                case '\b':
                case '\f':
                case '\n':
                case '\r':
                case '\t':
                    return true;

                default:
                    switch (CharUnicodeInfo.GetUnicodeCategory(c))
                    {
                        case UnicodeCategory.UppercaseLetter:
                        case UnicodeCategory.LowercaseLetter:
                        case UnicodeCategory.TitlecaseLetter:
                        case UnicodeCategory.OtherLetter:
                        case UnicodeCategory.DecimalDigitNumber:
                        case UnicodeCategory.LetterNumber:
                        case UnicodeCategory.OtherNumber:
                        case UnicodeCategory.SpaceSeparator:
                        case UnicodeCategory.ConnectorPunctuation:
                        case UnicodeCategory.DashPunctuation:
                        case UnicodeCategory.OpenPunctuation:
                        case UnicodeCategory.ClosePunctuation:
                        case UnicodeCategory.InitialQuotePunctuation:
                        case UnicodeCategory.FinalQuotePunctuation:
                        case UnicodeCategory.OtherPunctuation:
                        case UnicodeCategory.MathSymbol:
                        case UnicodeCategory.CurrencySymbol:
                        case UnicodeCategory.ModifierSymbol:
                        case UnicodeCategory.OtherSymbol:
                            return false;

                        default:
                            return true;
                    }
            }
        }

        private string EscapedString(string value)
        {
            if (value.All(c => !NeedsEscaping(c)))
            {
                return value;
            }

            var sb = new StringBuilder(value.Length);

            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        switch (CharUnicodeInfo.GetUnicodeCategory(c))
                        {
                            case UnicodeCategory.UppercaseLetter:
                            case UnicodeCategory.LowercaseLetter:
                            case UnicodeCategory.TitlecaseLetter:
                            case UnicodeCategory.OtherLetter:
                            case UnicodeCategory.DecimalDigitNumber:
                            case UnicodeCategory.LetterNumber:
                            case UnicodeCategory.OtherNumber:
                            case UnicodeCategory.SpaceSeparator:
                            case UnicodeCategory.ConnectorPunctuation:
                            case UnicodeCategory.DashPunctuation:
                            case UnicodeCategory.OpenPunctuation:
                            case UnicodeCategory.ClosePunctuation:
                            case UnicodeCategory.InitialQuotePunctuation:
                            case UnicodeCategory.FinalQuotePunctuation:
                            case UnicodeCategory.OtherPunctuation:
                            case UnicodeCategory.MathSymbol:
                            case UnicodeCategory.CurrencySymbol:
                            case UnicodeCategory.ModifierSymbol:
                            case UnicodeCategory.OtherSymbol:
                                sb.Append(c);
                                break;
                            default:
                                sb.AppendFormat("\\u{0:x4}", (int)c);
                                break;
                        }

                        break;
                }
            }

            return sb.ToString();
        }

        private BsonWriterState GetNextState()
        {
            if (_context.ContextType == ContextType.Array || _context.ContextType == ContextType.TopLevel)
            {
                return BsonWriterState.Value;
            }
            else
            {
                return BsonWriterState.Name;
            }
        }

        private string GuidToString(BsonBinarySubType subType, byte[] bytes, GuidRepresentation guidRepresentation)
        {
            if (bytes.Length != 16)
            {
                var message = string.Format("Length of binary subtype {0} must be 16, not {1}.", subType, bytes.Length);
                throw new ArgumentException(message);
            }

            if (subType == BsonBinarySubType.UuidLegacy)
            {
                if (guidRepresentation == GuidRepresentation.Standard)
                {
                    throw new ArgumentException("GuidRepresentation for binary subtype UuidLegacy must not be Standard.");
                }
            }

            if (subType == BsonBinarySubType.UuidStandard)
            {
                if (guidRepresentation == GuidRepresentation.Unspecified)
                {
                    guidRepresentation = GuidRepresentation.Standard;
                }

                if (guidRepresentation != GuidRepresentation.Standard)
                {
                    var message = string.Format("GuidRepresentation for binary subtype UuidStandard must be Standard, not {0}.", guidRepresentation);
                    throw new ArgumentException(message);
                }
            }

            if (guidRepresentation == GuidRepresentation.Unspecified)
            {
                var s = BsonUtils.ToHexString(bytes);
                var parts = new string[]
                {
                    s.Substring(0, 8),
                    s.Substring(8, 4),
                    s.Substring(12, 4),
                    s.Substring(16, 4),
                    s.Substring(20, 12)
                };
                return string.Format("HexData({0}, \"{1}\")", (int)subType, string.Join("-", parts));
            }
            else
            {
                string uuidConstructorName;
                switch (guidRepresentation)
                {
                    case GuidRepresentation.CSharpLegacy: uuidConstructorName = "CSUUID"; break;
                    case GuidRepresentation.JavaLegacy: uuidConstructorName = "JUUID"; break;
                    case GuidRepresentation.PythonLegacy: uuidConstructorName = "PYUUID"; break;
                    case GuidRepresentation.Standard: uuidConstructorName = "UUID"; break;
                    default: throw new BsonInternalException("Unexpected GuidRepresentation");
                }

                var guid = GuidConverter.FromBytes(bytes, guidRepresentation);
                return string.Format("{0}(\"{1}\")", uuidConstructorName, guid.ToString());
            }
        }

        private void WriteNameHelper(string name)
        {
            switch (_context.ContextType)
            {
                case ContextType.Array:
                    // don't write Array element names in JSON
                    if (_context.HasElements)
                    {
                        _textWriter.Write(", ");
                    }

                    break;
                case ContextType.Document:
                case ContextType.ScopeDocument:
                    if (_context.HasElements)
                    {
                        _textWriter.Write(",");
                    }

                    if (Settings.Indent)
                    {
                        _textWriter.Write(Settings.NewLineChars);
                        _textWriter.Write(_context.Indentation);
                    }
                    else
                    {
                        _textWriter.Write(" ");
                    }

                    WriteQuotedString(name);
                    _textWriter.Write(" : ");
                    break;
                case ContextType.TopLevel:
                    break;
                default:
                    throw new BsonInternalException("Invalid ContextType.");
            }

            _context.HasElements = true;
        }

        private void WriteQuotedString(string value)
        {
            _textWriter.Write("\"");
            _textWriter.Write(EscapedString(value));
            _textWriter.Write("\"");
        }
    }
}
