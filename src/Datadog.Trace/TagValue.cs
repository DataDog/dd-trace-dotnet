using System;

namespace Datadog.Trace
{
    /// <summary>
    /// Span tag value struct, use directly as a string or double
    /// </summary>
    public readonly struct TagValue : IEquatable<TagValue>
    {
        private readonly string _stringValue;
        private readonly double? _doubleValue;

        private TagValue(string value)
        {
            _stringValue = value;
            _doubleValue = default;
            IsMetrics = false;
        }

        private TagValue(double? value)
        {
            _stringValue = null;
            _doubleValue = value;
            IsMetrics = true;
        }

        /// <summary>
        /// Gets a value indicating whether if the tag value is null
        /// </summary>
        public bool IsNull
        {
            get
            {
                return IsMetrics ? !_doubleValue.HasValue : _stringValue is null;
            }
        }

        /// <summary>
        /// Gets a value indicating whether if the value is a metric
        /// </summary>
        public bool IsMetrics { get; }

        /// <summary>
        /// Gets the string value
        /// </summary>
        public string StringValue
        {
            get
            {
                return _stringValue;
            }
        }

        /// <summary>
        /// Gets the double value
        /// </summary>
        public double DoubleValue
        {
            get
            {
                return _doubleValue ?? default;
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

        /// <summary>
        /// Equal TagValue operator
        /// </summary>
        /// <param name="left">Left tag value</param>
        /// <param name="right">Right tag value</param>
        /// <returns>True if both tag values are equal; otherwise, false.</returns>
        public static bool operator ==(TagValue left, TagValue right)
            => left.Equals(right);

        /// <summary>
        /// Not Equal TagValue operator
        /// </summary>
        /// <param name="left">Left tag value</param>
        /// <param name="right">Right tag value</param>
        /// <returns>True if both tag values are not equal; otherwise, false.</returns>
        public static bool operator !=(TagValue left, TagValue right)
            => !left.Equals(right);

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
                return IsMetrics == tagValue.IsMetrics &&
                    _stringValue == tagValue._stringValue &&
                    _doubleValue == tagValue._doubleValue;
            }

            return base.Equals(obj);
        }

        /// <inheritdoc/>
        public bool Equals(TagValue value)
        {
            return IsMetrics == value.IsMetrics &&
                    _stringValue == value._stringValue &&
                    _doubleValue == value._doubleValue;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            int hash = 17;
            if (IsMetrics)
            {
                hash = (hash * 23) + _doubleValue.GetHashCode();
            }
            else
            {
                hash = (hash * 23) + _stringValue.GetHashCode();
            }

            return hash;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (IsMetrics)
            {
                return (_doubleValue ?? default).ToString("G17");
            }

            return _stringValue;
        }
    }
}
