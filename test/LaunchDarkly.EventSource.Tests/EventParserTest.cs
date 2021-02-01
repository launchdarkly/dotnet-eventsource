using System.Text;
using Xunit;

namespace LaunchDarkly.EventSource.Tests
{
    public class EventParserTest
    {
        private static Utf8ByteSpan ToBytes(string s)
        {
            // Use a non-zero offset here and add some characters to skip at the beginning,
            // to make sure the parser is taking the offset into account.
            return new Utf8ByteSpan(Encoding.UTF8.GetBytes("xx" + s), 2, s.Length);
        }

        [Theory]
        [InlineData(":")]
        [InlineData(": ")]
        [InlineData(": some data")]
        public void IsCommentIsTrueForComment(string data)
        {
            Assert.True(EventParser.ParseLineString(data).IsComment);
            Assert.True(EventParser.ParseLineUtf8Bytes(ToBytes(data)).IsComment);
        }

        [Theory]
        [InlineData("id: 123")]
        [InlineData(" : ")]
        [InlineData("some data")]
        [InlineData("event: put")]
        public void IsCommentIsFalseForNonComment(string data)
        {
            Assert.False(EventParser.ParseLineString(data).IsComment);
            Assert.False(EventParser.ParseLineUtf8Bytes(ToBytes(data)).IsComment);
        }

        [Theory]
        [InlineData("data:", "data", "")]
        [InlineData("data: something", "data", "something")]
        [InlineData("data", "data", "")]
        public void ParsesFieldNameAndValue(string data, string expectedName, string expectedValue)
        {
            var result1 = EventParser.ParseLineString(data);
            Assert.Equal(expectedName, result1.FieldName);
            Assert.Equal(expectedValue, result1.ValueString);
            Assert.Equal(expectedValue, result1.GetValueAsString());

            var result2 = EventParser.ParseLineUtf8Bytes(ToBytes(data));
            Assert.Equal(expectedName, result2.FieldName);
            Assert.Null(result2.ValueString);
            Assert.True(new Utf8ByteSpan(expectedValue).Equals(result2.ValueBytes));
            Assert.Equal(expectedValue, result2.GetValueAsString());
        }
    }
}
