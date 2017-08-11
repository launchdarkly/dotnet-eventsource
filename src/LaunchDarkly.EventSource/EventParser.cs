using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace LaunchDarkly.EventSource
{
    public static class EventParser
    {

        public static bool IsComment(string value)
        {
            return value.StartsWith(":", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsDataField(string value)
        {
            return Constants.DataField.Equals(value, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsIdField(string value)
        {
            return Constants.IdField.Equals(value, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsEventField(string value)
        {
            return Constants.EventField.Equals(value, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsRetryField(string value)
        {
            return Constants.RetryField.Equals(value, StringComparison.OrdinalIgnoreCase);
        }

        public static bool ContainsField(string value)
        {
            return value.IndexOf(":", StringComparison.Ordinal) > 0;
        }

        public static KeyValuePair<string, string> GetFieldFromLine(string value)
        {
            if (!ContainsField(value)) return new KeyValuePair<string, string>();

            var colonIndex = value.IndexOf(":", StringComparison.Ordinal);

            var fieldName = value.Substring(0, colonIndex);
            var fieldValue = value.Substring(colonIndex + 1).TrimStart(' ');

            return new KeyValuePair<string, string>(fieldName, fieldValue);
        }

        public static bool IsStringNumeric(string input)
        {
            return Regex.IsMatch(input, @"^[\d]+$");
        }
        
    }
}
