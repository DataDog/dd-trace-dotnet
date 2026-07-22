// <copyright file="GlobalCoverageJsonPreflightScanner.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Text;

namespace Datadog.Trace.Ci.Coverage;

internal sealed class GlobalCoverageJsonPreflightScanner
{
    private readonly GlobalCoverageArtifactLimits _limits;
    private readonly char[] _readBuffer;
    private readonly char[] _propertyBuffer;
    private readonly Frame[] _frames;
    private int _readOffset;
    private int _readCount;
    private int _depth;
    private int _componentCount;
    private int _entryCount;
    private long _identityCharacters;
    private long _bitmapBytes;
    private TextReader? _reader;

    internal GlobalCoverageJsonPreflightScanner(GlobalCoverageArtifactLimits limits)
    {
        _limits = limits;
        _readBuffer = new char[limits.ScannerBufferCharacters];
        _propertyBuffer = new char[limits.MaximumPropertyCharacters];
        _frames = new Frame[limits.MaximumDepth];
    }

    private enum ContainerKind
    {
        Object,
        Array,
    }

    private enum ArrayKind
    {
        Unknown,
        Components,
        Files,
    }

    private enum PropertyKind
    {
        Unknown,
        Components,
        Files,
        Name,
        Path,
        ExecutableBitmap,
        ExecutedBitmap,
    }

    private static bool IsBase64Character(char character, ref bool paddingStarted, ref int padding)
    {
        if (character == '=')
        {
            paddingStarted = true;
            padding++;
            return true;
        }

        if (paddingStarted)
        {
            return false;
        }

        return (character >= 'A' && character <= 'Z') ||
               (character >= 'a' && character <= 'z') ||
               (character >= '0' && character <= '9') ||
               character is '+' or '/';
    }

    internal void Scan(Stream stream)
    {
        _reader = new StreamReader(stream, new UTF8Encoding(false, true), true, _limits.ScannerBufferCharacters, true);
        _readOffset = 0;
        _readCount = 0;
        _depth = 0;
        _componentCount = 0;
        _entryCount = 0;
        _identityCharacters = 0;
        _bitmapBytes = 0;

        var sawToken = false;
        int value;
        while ((value = Read()) >= 0)
        {
            var character = (char)value;
            if (char.IsWhiteSpace(character))
            {
                continue;
            }

            sawToken = true;
            switch (character)
            {
                case '{':
                    BeginContainer(ContainerKind.Object);
                    break;
                case '[':
                    BeginContainer(ContainerKind.Array);
                    break;
                case '}':
                    EndContainer(ContainerKind.Object);
                    break;
                case ']':
                    EndContainer(ContainerKind.Array);
                    break;
                case ',':
                    OnComma();
                    break;
                case ':':
                    break;
                case '"':
                    ReadStringToken();
                    break;
                default:
                    ReadScalarToken(character);
                    CompleteScalarValue();
                    break;
            }
        }

        if (!sawToken || _depth != 0)
        {
            throw new InvalidDataException("The global coverage JSON is incomplete.");
        }
    }

    private void BeginContainer(ContainerKind kind)
    {
        var arrayKind = ArrayKind.Unknown;
        if (_depth > 0)
        {
            ref var parent = ref _frames[_depth - 1];
            if (parent.Kind == ContainerKind.Array)
            {
                if (kind == ContainerKind.Object && parent.ArrayKind == ArrayKind.Components)
                {
                    _componentCount = checked(_componentCount + 1);
                    if (_componentCount > _limits.MaximumComponents)
                    {
                        throw new InvalidDataException("The global coverage component limit was exceeded.");
                    }
                }
                else if (kind == ContainerKind.Object && parent.ArrayKind == ArrayKind.Files)
                {
                    _entryCount = checked(_entryCount + 1);
                    if (_entryCount > _limits.MaximumEntries)
                    {
                        throw new InvalidDataException("The global coverage entry limit was exceeded.");
                    }
                }
            }
            else if (kind == ContainerKind.Array)
            {
                arrayKind = parent.PendingProperty switch
                {
                    PropertyKind.Components => ArrayKind.Components,
                    PropertyKind.Files => ArrayKind.Files,
                    _ => ArrayKind.Unknown,
                };
            }

            parent.PendingProperty = PropertyKind.Unknown;
        }

        if (_depth >= _limits.MaximumDepth)
        {
            throw new InvalidDataException("The global coverage JSON nesting limit was exceeded.");
        }

        _frames[_depth++] = new Frame(kind, arrayKind, kind == ContainerKind.Object);
    }

    private void EndContainer(ContainerKind expectedKind)
    {
        if (_depth == 0 || _frames[_depth - 1].Kind != expectedKind)
        {
            throw new InvalidDataException("The global coverage JSON contains mismatched containers.");
        }

        _depth--;
    }

    private void OnComma()
    {
        if (_depth > 0 && _frames[_depth - 1].Kind == ContainerKind.Object)
        {
            ref var frame = ref _frames[_depth - 1];
            frame.ExpectingProperty = true;
            frame.PendingProperty = PropertyKind.Unknown;
        }
    }

    private void ReadStringToken()
    {
        var isProperty = _depth > 0 &&
                         _frames[_depth - 1].Kind == ContainerKind.Object &&
                         _frames[_depth - 1].ExpectingProperty;
        var valueProperty = !isProperty && _depth > 0 && _frames[_depth - 1].Kind == ContainerKind.Object
                                ? _frames[_depth - 1].PendingProperty
                                : PropertyKind.Unknown;
        var decodedLength = 0;
        var base64Characters = 0;
        var base64Padding = 0;
        var base64PaddingStarted = false;

        while (true)
        {
            var value = Read();
            if (value < 0)
            {
                throw new InvalidDataException("The global coverage JSON contains an unterminated string.");
            }

            var character = (char)value;
            if (character == '"')
            {
                break;
            }

            if (character < 0x20)
            {
                throw new InvalidDataException("The global coverage JSON contains an invalid control character.");
            }

            if (character == '\\')
            {
                character = ReadEscapedCharacter();
            }

            decodedLength = checked(decodedLength + 1);
            if (decodedLength > _limits.MaximumScalarCharacters)
            {
                throw new InvalidDataException("The global coverage scalar-token limit was exceeded.");
            }

            if (isProperty)
            {
                if (decodedLength > _limits.MaximumPropertyCharacters)
                {
                    throw new InvalidDataException("The global coverage property-name limit was exceeded.");
                }

                _propertyBuffer[decodedLength - 1] = character;
            }
            else if (valueProperty is PropertyKind.ExecutableBitmap or PropertyKind.ExecutedBitmap)
            {
                if (!IsBase64Character(character, ref base64PaddingStarted, ref base64Padding))
                {
                    throw new InvalidDataException("The global coverage JSON contains an invalid base64 bitmap.");
                }

                base64Characters = checked(base64Characters + 1);
            }
        }

        if (isProperty)
        {
            ref var frame = ref _frames[_depth - 1];
            frame.PendingProperty = GetPropertyKind(decodedLength);
            frame.ExpectingProperty = false;
            return;
        }

        if (valueProperty is PropertyKind.Name or PropertyKind.Path)
        {
            _identityCharacters = checked(_identityCharacters + decodedLength);
            if (_identityCharacters > _limits.MaximumIdentityCharacters)
            {
                throw new InvalidDataException("The global coverage path/name character limit was exceeded.");
            }
        }
        else if (valueProperty is PropertyKind.ExecutableBitmap or PropertyKind.ExecutedBitmap)
        {
            if ((base64Characters & 3) != 0 || base64Padding > 2)
            {
                throw new InvalidDataException("The global coverage JSON contains an invalid base64 bitmap length.");
            }

            var decodedBytes = checked(((long)base64Characters / 4 * 3) - base64Padding);
            if (decodedBytes > _limits.MaximumBitmapBytes)
            {
                throw new InvalidDataException("A global coverage bitmap exceeds the per-bitmap limit.");
            }

            _bitmapBytes = checked(_bitmapBytes + decodedBytes);
            if (_bitmapBytes > _limits.MaximumModelBitmapBytes)
            {
                throw new InvalidDataException("The global coverage model bitmap limit was exceeded.");
            }
        }

        CompleteScalarValue();
    }

    private char ReadEscapedCharacter()
    {
        var value = Read();
        if (value < 0)
        {
            throw new InvalidDataException("The global coverage JSON contains an incomplete escape.");
        }

        return (char)value switch
        {
            '"' => '"',
            '\\' => '\\',
            '/' => '/',
            'b' => '\b',
            'f' => '\f',
            'n' => '\n',
            'r' => '\r',
            't' => '\t',
            'u' => ReadUnicodeEscape(),
            _ => throw new InvalidDataException("The global coverage JSON contains an invalid escape."),
        };
    }

    private char ReadUnicodeEscape()
    {
        var result = 0;
        for (var i = 0; i < 4; i++)
        {
            var value = Read();
            if (value < 0)
            {
                throw new InvalidDataException("The global coverage JSON contains an incomplete unicode escape.");
            }

            var digit = (char)value;
            result = checked((result << 4) + (digit switch
            {
                >= '0' and <= '9' => digit - '0',
                >= 'a' and <= 'f' => digit - 'a' + 10,
                >= 'A' and <= 'F' => digit - 'A' + 10,
                _ => throw new InvalidDataException("The global coverage JSON contains an invalid unicode escape."),
            }));
        }

        return (char)result;
    }

    private PropertyKind GetPropertyKind(int length)
    {
        if (Matches("components", length))
        {
            return PropertyKind.Components;
        }

        if (Matches("files", length))
        {
            return PropertyKind.Files;
        }

        if (Matches("name", length))
        {
            return PropertyKind.Name;
        }

        if (Matches("path", length))
        {
            return PropertyKind.Path;
        }

        if (Matches("executableBitmap", length))
        {
            return PropertyKind.ExecutableBitmap;
        }

        return Matches("executedBitmap", length) ? PropertyKind.ExecutedBitmap : PropertyKind.Unknown;
    }

    private bool Matches(string expected, int actualLength)
    {
        if (expected.Length != actualLength)
        {
            return false;
        }

        for (var i = 0; i < actualLength; i++)
        {
            if (_propertyBuffer[i] != expected[i])
            {
                return false;
            }
        }

        return true;
    }

    private void ReadScalarToken(char firstCharacter)
    {
        var length = 1;
        var character = firstCharacter;
        while (true)
        {
            var value = Read();
            if (value < 0)
            {
                return;
            }

            character = (char)value;
            if (char.IsWhiteSpace(character) || character is ',' or ']' or '}')
            {
                Unread();
                return;
            }

            length = checked(length + 1);
            if (length > _limits.MaximumScalarCharacters)
            {
                throw new InvalidDataException("The global coverage scalar-token limit was exceeded.");
            }
        }
    }

    private void CompleteScalarValue()
    {
        if (_depth > 0 && _frames[_depth - 1].Kind == ContainerKind.Object)
        {
            _frames[_depth - 1].PendingProperty = PropertyKind.Unknown;
        }
    }

    private int Read()
    {
        if (_readOffset >= _readCount)
        {
            _readCount = _reader!.Read(_readBuffer, 0, _readBuffer.Length);
            _readOffset = 0;
            if (_readCount == 0)
            {
                return -1;
            }
        }

        return _readBuffer[_readOffset++];
    }

    private void Unread()
    {
        if (_readOffset == 0)
        {
            throw new InvalidOperationException("The global coverage scanner cannot unread across a buffer boundary.");
        }

        _readOffset--;
    }

    private struct Frame
    {
        internal ContainerKind Kind;
        internal ArrayKind ArrayKind;
        internal bool ExpectingProperty;
        internal PropertyKind PendingProperty;

        internal Frame(ContainerKind kind, ArrayKind arrayKind, bool expectingProperty)
        {
            Kind = kind;
            ArrayKind = arrayKind;
            ExpectingProperty = expectingProperty;
            PendingProperty = PropertyKind.Unknown;
        }
    }
}
