using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace LaunchDarkly.EventSource.Tests
{
    public class EventParserTests
    {
        [Theory]
        [InlineData(":")]
        [InlineData(": ")]
        [InlineData(": some data")]
        public void IsComment_Given_a_comment_then_return_true(string data)
        {
            Assert.True(EventParser.IsComment(data));
        }

        [Theory]
        [InlineData("id: 123")]
        [InlineData(" : ")]
        [InlineData("some data")]
        [InlineData("event: put")]
        [InlineData(null)]
        public void IsComment_Given_a_value_other_than_a_comment_then_return_false(string data)
        {
            Assert.False(EventParser.IsComment(data));
        }

        [Theory]
        [InlineData("data")]
        [InlineData("DATA")]
        [InlineData("DaTa")]
        public void IsDataFieldName_Given_a_field_name_then_return_true(string data)
        {
            Assert.True(EventParser.IsDataFieldName(data));
        }

        [Theory]
        [InlineData("test")]
        [InlineData("data:")]
        [InlineData("event:")]
        [InlineData(null)]
        public void IsDataFieldName_Given_a_value_other_than_a_data_field_then_return_false(string data)
        {
            Assert.False(EventParser.IsDataFieldName(data));
        }

        [Theory]
        [InlineData("id")]
        [InlineData("ID")]
        [InlineData("Id")]
        public void IsIdFieldName_Given_a_field_name_then_return_true(string data)
        {
            Assert.True(EventParser.IsIdFieldName(data));
        }

        [Theory]
        [InlineData("test")]
        [InlineData("id:")]
        [InlineData("event:")]
        [InlineData(null)]
        public void IsIdFieldName_Given_a_value_other_than_an_id_field_then_return_false(string data)
        {
            Assert.False(EventParser.IsIdFieldName(data));
        }

        [Theory]
        [InlineData("event")]
        [InlineData("EVENT")]
        [InlineData("EvEnT")]
        public void IsEventFieldName_Given_a_field_name_then_return_true(string data)
        {
            Assert.True(EventParser.IsEventFieldName(data));
        }

        [Theory]
        [InlineData("test")]
        [InlineData("id:")]
        [InlineData("event:")]
        [InlineData(null)]
        public void IsEventFieldName_Given_a_value_other_than_an_event_field_then_return_false(string data)
        {
            Assert.False(EventParser.IsEventFieldName(data));
        }

        [Theory]
        [InlineData("retry")]
        [InlineData("RETRY")]
        [InlineData("rETry")]
        public void IsRetryFieldName_Given_a_field_name_then_return_true(string data)
        {
            Assert.True(EventParser.IsRetryFieldName(data));
        }

        [Theory]
        [InlineData("test")]
        [InlineData("retry:")]
        [InlineData("event:")]
        [InlineData(null)]
        public void IsRetryFieldName_Given_a_value_other_than_a_retry_field_then_return_false(string data)
        {
            Assert.False(EventParser.IsRetryFieldName(data));
        }

        [Theory]
        [InlineData("data:")]
        [InlineData("data: something")]
        [InlineData("event: put")]
        [InlineData("id: put")]
        public void ContainsField_Given_a_field_then_return_true(string data)
        {
            Assert.True(EventParser.ContainsField(data));
        }

        [Theory]
        [InlineData(": some comment")]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("\n\n")]
        public void ContainsField_Given_a_value_other_than_a_field_then_return_false(string data)
        {
            Assert.False(EventParser.ContainsField(data));
        }

        [Theory]
        [InlineData("0")]
        [InlineData("12345")]
        [InlineData("123456789123456789123456789")]
        public void IsStringNumeric_Given_a_value_containing_numbers_then_return_true(string data)
        {
            Assert.True(EventParser.IsStringNumeric(data));
        }

        [Theory]
        [InlineData("A1")]
        [InlineData("AAA")]
        [InlineData(null)]
        [InlineData("111-11")]
        public void IsStringNumeric_Given_a_value_containing_alphanumeric_values_then_return_false(string data)
        {
            Assert.False(EventParser.IsStringNumeric(data));
        }

        [Theory]
        [InlineData("id", "12")]
        [InlineData("event", "put")]
        [InlineData("data", "here's your message")]
        [InlineData("retry", "3000")]
        public void GetFieldFromLine_Given_a_server_sent_event_then_return_field_name_and_value(string key, string value)
        {
            string sse = string.Format("{0}: {1}", key, value);

            var result = EventParser.GetFieldFromLine(sse);

            Assert.Equal(result.Key, key);
            Assert.Equal(result.Value, value);
        }

        [Theory]
        [InlineData(":something")]
        [InlineData("junk")]
        [InlineData("\n")]
        public void GetFieldFromLine_Given_an_invalid_server_sent_event_then_return_default_key_value_pair(string value)
        {
            var result = EventParser.GetFieldFromLine(value);

            Assert.Null(result.Key);
            Assert.Null(result.Value);
        }
    }
}
