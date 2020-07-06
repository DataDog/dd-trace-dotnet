using System;

namespace Datadog.Trace
{
    /// <summary>
    /// Span tag value struct, use directly as a string or double
    /// </summary>
    public readonly struct TagValue : IEquatable<TagValue>
    {
        private TagValue(string value)
        {
            StringValue = value;
            DoubleValue = default;
            IsMetrics = false;
        }

        private TagValue(double value)
        {
            StringValue = null;
            DoubleValue = value;
            IsMetrics = true;
        }

        /// <summary>
        /// Gets a value indicating whether if the value is a metric
        /// </summary>
        public bool IsMetrics { get; }

        /// <summary>
        /// Gets the string value
        /// </summary>
        public string StringValue { get; }

        /// <summary>
        /// Gets the double value
        /// </summary>
        public double DoubleValue { get; }

        /// <summary>
        /// Implicit conversion to string
        /// </summary>
        /// <param name="tagValue">Span tag value</param>
        public static implicit operator string(TagValue tagValue)
        {
            if (tagValue.IsMetrics) { throw new InvalidCastException(); }
            return tagValue.StringValue;
        }

        /// <summary>
        /// Implicit conversion to double
        /// </summary>
        /// <param name="tagValue">Span tag value</param>
        public static implicit operator double(TagValue tagValue)
        {
            if (!tagValue.IsMetrics) { throw new InvalidCastException(); }
            return tagValue.DoubleValue;
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
                return false;
            }

            if (obj is string stringValue)
            {
                return string.Equals(StringValue, stringValue);
            }

            if (obj is double doubleValue)
            {
                return DoubleValue == doubleValue;
            }

            if (obj is TagValue tagValue)
            {
                return IsMetrics == tagValue.IsMetrics &&
                    StringValue == tagValue.StringValue &&
                    DoubleValue == tagValue.DoubleValue;
            }

            return base.Equals(obj);
        }

        /// <inheritdoc/>
        public bool Equals(TagValue value)
        {
            return IsMetrics == value.IsMetrics &&
                    StringValue == value.StringValue &&
                    DoubleValue == value.DoubleValue;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            int hash = 17;
            if (IsMetrics)
            {
                hash = (hash * 23) + DoubleValue.GetHashCode();
            }
            else
            {
                hash = (hash * 23) + StringValue.GetHashCode();
            }

            return hash;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (IsMetrics)
            {
                return DoubleValue.ToString("G17");
            }

            return StringValue;
        }
    }
}
