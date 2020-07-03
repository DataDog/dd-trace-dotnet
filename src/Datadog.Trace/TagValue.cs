using System;

namespace Datadog.Trace
{
    /// <summary>
    /// Span tag value struct, use directly as a string or double
    /// </summary>
    public readonly struct TagValue
    {
        private readonly string _stringValue;
        private readonly double? _doubleValue;
        private readonly bool _isMetrics;

        private TagValue(string value)
        {
            _stringValue = value;
            _doubleValue = default;
            _isMetrics = false;
        }

        private TagValue(double? value)
        {
            _stringValue = null;
            _doubleValue = value;
            _isMetrics = true;
        }

        /// <summary>
        /// Gets a value indicating whether if the tag value is null
        /// </summary>
        public bool IsNull
        {
            get
            {
                return _isMetrics ? !_doubleValue.HasValue : _stringValue is null;
            }
        }

        /// <summary>
        /// Gets a value indicating whether if the value is a metric
        /// </summary>
        public bool IsMetrics
        {
            get
            {
                return _isMetrics;
            }
        }

        /// <summary>
        /// Implicit conversion to string
        /// </summary>
        /// <param name="tagValue">Span tag value</param>
        public static implicit operator string(TagValue tagValue)
        {
            return tagValue._stringValue;
        }

        /// <summary>
        /// Implicit conversion to double
        /// </summary>
        /// <param name="tagValue">Span tag value</param>
        public static implicit operator double(TagValue tagValue)
        {
            return tagValue._doubleValue ?? default;
        }

        /// <summary>
        /// Implicit conversion to nullable double
        /// </summary>
        /// <param name="tagValue">Span tag value</param>
        public static implicit operator double?(TagValue tagValue)
        {
            return tagValue._doubleValue;
        }

        /// <summary>
        /// Implicit conversion from string
        /// </summary>
        /// <param name="value">String value</param>
        public static implicit operator TagValue(string value)
        {
            return new TagValue(value);
        }

        /// <summary>
        /// Implicit conversion from double
        /// </summary>
        /// <param name="value">Double value</param>
        public static implicit operator TagValue(double value)
        {
            return new TagValue(value);
        }

        /// <summary>
        /// Implicit conversion from nullable double
        /// </summary>
        /// <param name="value">Double value</param>
        public static implicit operator TagValue(double? value)
        {
            return new TagValue(value);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj is null)
            {
                return IsNull;
            }

            if (obj is string stringValue)
            {
                return string.Equals(_stringValue, stringValue);
            }

            if (obj is double doubleValue)
            {
                return _doubleValue == doubleValue;
            }

            if (obj is TagValue tagValue)
            {
                return _isMetrics == tagValue._isMetrics &&
                    _stringValue == tagValue._stringValue &&
                    _doubleValue == tagValue._doubleValue;
            }

            return base.Equals(obj);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            if (_isMetrics)
            {
                return _doubleValue.GetHashCode();
            }

            return _stringValue.GetHashCode();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (_isMetrics)
            {
                return _doubleValue.ToString();
            }

            return _stringValue;
        }

        /// <summary>
        /// Converts a <see cref="TagValue"/> into a <see cref="bool"/> by comparing it to commonly used values
        /// such as "True", "yes", or "1". Case-insensitive. Defaults to <c>false</c> if string is not recognized.
        /// </summary>
        /// <returns><c>true</c> if is one of the accepted values for <c>true</c>; <c>false</c> otherwise.</returns>
        public bool? ToBoolean()
        {
            if (_isMetrics)
            {
                return _doubleValue == 1d;
            }

            if (_stringValue == null)
            {
                return null;
            }

            if (string.Compare("TRUE", _stringValue, StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare("YES", _stringValue, StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare("T", _stringValue, StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare("Y", _stringValue, StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare("1", _stringValue, StringComparison.Ordinal) == 0)
            {
                return true;
            }

            if (string.Compare("FALSE", _stringValue, StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare("NO", _stringValue, StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare("F", _stringValue, StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare("N", _stringValue, StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare("0", _stringValue, StringComparison.Ordinal) == 0)
            {
                return true;
            }

            return null;
        }
    }
}
