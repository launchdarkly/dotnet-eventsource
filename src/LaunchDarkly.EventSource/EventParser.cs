using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// An internal class containing helper methods to parse Server Sent Event data.
    /// </summary>
    internal static class EventParser
    {
        /// <summary>
        /// The result returned by <see cref="ParseLineString(string)"/> or <see cref="ParseLineUtf8Bytes(Utf8ByteSpan)"/>.
        /// </summary>
        internal struct Result
        {
            internal string FieldName { get; set; } // null means it's a comment
            internal string ValueString { get; set; }
            internal Utf8ByteSpan ValueBytes { get; set; }

            internal string GetValueAsString() => ValueString is null ? ValueBytes.GetString() : ValueString;

            internal bool IsComment => FieldName is null;
            internal bool IsDataField => FieldName == "data";
            internal bool IsEventField => FieldName == "event";
            internal bool IsIdField => FieldName == "id";
            internal bool IsRetryField => FieldName == "retry";
        }

        /// <summary>
        /// Attempts to parse a single non-empty line of SSE content that was read as a string. Empty lines
        /// shoudl not be not passed to this method.
        /// </summary>
        /// <param name="line">a line that was read from the stream, not including any trailing CR/LF</param>
        /// <returns>a <see cref="Result"/> containing the parsed field or comment; <c>ValueString</c> will
        /// be set rather than <c>ValueBytes</c></returns>
        internal static Result ParseLineString(string line)
        {
            var colonPos = line.IndexOf(':');
            if (colonPos == 0) // comment
            {
                return new Result { ValueString = line };
            }
            if (colonPos < 0) // field name without a value - assume empty value
            {
                return new Result { FieldName = line, ValueString = "" };
            }
            int valuePos = colonPos + 1;
            if (valuePos < line.Length && line[valuePos] == ' ')
            {
                valuePos++; // trim a single leading space from the value, if present
            }
            return new Result
            {
                FieldName = line.Substring(0, colonPos),
                ValueString = line.Substring(valuePos)
            };
        }

        /// <summary>
        /// Attempts to parse a single non-empty line of SSE content that was read as UTF-8 bytes. Empty lines
        /// shoudl not be not passed to this method.
        /// </summary>
        /// <param name="line">a line that was read from the stream, not including any trailing CR/LF</param>
        /// <returns>a <see cref="Result"/> containing the parsed field or comment; <c>ValueBytes</c>
        /// will be set rather than <c>ValueString</c></returns>
        public static Result ParseLineUtf8Bytes(Utf8ByteSpan line)
        {
            if (line.Length > 0 && line.Data[line.Offset] == ':') // comment
            {
                return new Result { ValueBytes = line };
            }
            int colonPos = 0;
            for (; colonPos < line.Length && line.Data[line.Offset + colonPos] != ':'; colonPos++) { }
            string fieldName = Encoding.UTF8.GetString(line.Data, line.Offset, colonPos);
            if (colonPos == line.Length) // field name without a value - assume empty value
            {
                return new Result {
                    FieldName = fieldName,
                    ValueBytes = new Utf8ByteSpan()
                };
            }
            int valuePos = colonPos + 1;
            if (valuePos < line.Length && line.Data[line.Offset + valuePos] == ' ')
            {
                valuePos++; // trim a single leading space from the value, if present
            }
            return new Result
            {
                FieldName = fieldName,
                ValueBytes = new Utf8ByteSpan(line.Data, line.Offset + valuePos, line.Length - valuePos)
            };
        }
    }
}
