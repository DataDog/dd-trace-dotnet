// <copyright file="PProfSampleLabel.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.PProf.Export
{
    internal struct PProfSampleLabel
    {
        private readonly PProfSampleLabel.Kind _valueKind;
        private readonly string _key;
        private readonly string _strValueOrNumUnit;
        private readonly long _numValue;

        public PProfSampleLabel(string key, long num, string numUnit)
        {
            _valueKind = PProfSampleLabel.Kind.Number;
            _key = key;
            _numValue = num;
            _strValueOrNumUnit = numUnit;
        }

        public PProfSampleLabel(string key, string str)
        {
            _valueKind = PProfSampleLabel.Kind.String;
            _key = key;
            _numValue = 0;
            _strValueOrNumUnit = str;
        }

        public enum Kind : byte
        {
            Unknown = 0,
            Number = 2,
            String = 3
        }

        public PProfSampleLabel.Kind ValueKind
        {
            get
            {
                return _valueKind;
            }
        }

        public string Key
        {
            get
            {
                return _key;
            }
        }

        public long NumberValue
        {
            get
            {
                ValidateValueKind(_valueKind, PProfSampleLabel.Kind.Number);
                return _numValue;
            }
        }

        public string StringValue
        {
            get
            {
                ValidateValueKind(_valueKind, PProfSampleLabel.Kind.String);
                return _strValueOrNumUnit;
            }
        }

        public string NumberUnit
        {
            get
            {
                ValidateValueKind(_valueKind, PProfSampleLabel.Kind.Number);
                return _strValueOrNumUnit;
            }
        }

        internal static void ValidateValueKind(PProfSampleLabel.Kind actualValueKind, PProfSampleLabel.Kind expectedValueKind)
        {
            if (actualValueKind == expectedValueKind)
            {
                return;
            }

            switch (actualValueKind)
            {
                case PProfSampleLabel.Kind.Number:
                case PProfSampleLabel.Kind.String:
                    throw new InvalidOperationException($"Cannot perform this operation because the expected {nameof(ValueKind)} of this"
                                                      + $" instance was {expectedValueKind}, but the actual {nameof(ValueKind)} is {actualValueKind}.");

                case PProfSampleLabel.Kind.Unknown:
                default:
                    throw new InvalidOperationException($"Cannot perform this operation because the expected {nameof(ValueKind)} of this"
                                                      + $" instance was {expectedValueKind}, but the actual {nameof(ValueKind)} is {actualValueKind}."
                                                      + $" (Did you use the default ctor for {nameof(PProfSampleLabel)}?"
                                                      + " If so, use a different ctor overload.)");
            }
        }
    }
}
