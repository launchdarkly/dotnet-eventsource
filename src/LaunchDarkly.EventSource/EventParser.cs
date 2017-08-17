using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// An internal class containing helper methods to parse Server Sent Event data.
    /// </summary>
    internal static class EventParser
    {

        /// <summary>
        /// Determines if the specified value is a Comment in a Server Sent Event message.
        /// </summary>
        /// <param name="value">A string value.</param>
        /// <returns>
        ///   <c>true</c> if the specified value is a comment; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsComment(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;

            return value.StartsWith(":", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines if the specified value is a data field in a Server Sent Event message.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        ///   <c>true</c> if the specified value is a data field; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsDataFieldName(string value)
        {
            return Constants.DataField.Equals(value, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines if the specified value is an ID field in a Server Sent Event message.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        ///   <c>true</c> if the specified value is an ID field; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsIdFieldName(string value)
        {
            return Constants.IdField.Equals(value, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines if the specified value is an event field in a Server Sent Event message.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        ///   <c>true</c> if the specified value is an event field; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsEventFieldName(string value)
        {
            return Constants.EventField.Equals(value, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines if the specified value is a retry field in a Server Sent Event message.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        ///   <c>true</c> if the specified value is a retry field; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsRetryFieldName(string value)
        {
            return Constants.RetryField.Equals(value, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines if the specified value contains a field in a Server Sent Event message.
        /// </summary>
        /// <remarks>
        /// This method looks for the index of a first occuring colon character in the specified value. Returns true if the index is greater than zero (a zero index value would indicate a comment rather than a field).
        /// </remarks>
        /// <param name="value">The value.</param>
        /// <returns>
        ///   <c>true</c> if the specified value contains a field; otherwise, <c>false</c>.
        /// </returns>
        public static bool ContainsField(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;

            return value.IndexOf(":", StringComparison.Ordinal) > 0;
        }

        /// <summary>
        /// Gets the field and value from a server sent event message.
        /// </summary>
        /// <remarks>
        /// For processing server sent event messages, see the documentation titled <a href="https://html.spec.whatwg.org/multipage/server-sent-events.html#processField">Process the Field</a>
        /// </remarks>
        /// <param name="value">The server sent event message.</param>
        /// <returns></returns>
        public static KeyValuePair<string, string> GetFieldFromLine(string value)
        {
            if (!ContainsField(value)) return new KeyValuePair<string, string>();

            var colonIndex = value.IndexOf(":", StringComparison.Ordinal);

            var fieldName = value.Substring(0, colonIndex);
            var fieldValue = value.Substring(colonIndex + 1).TrimStart(' ');

            return new KeyValuePair<string, string>(fieldName, fieldValue);
        }

        /// <summary>
        /// Determines if the specified string is numeric (contains only numeric characters).
        /// </summary>
        /// <param name="value">The string value to inspect.</param>
        /// <returns>
        ///   <c>true</c> if the string value is numeric; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsStringNumeric(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;

            return Regex.IsMatch(value, @"^[\d]+$");
        }
        
    }
}
