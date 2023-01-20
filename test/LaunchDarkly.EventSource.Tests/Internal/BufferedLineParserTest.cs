using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LaunchDarkly.EventSource.Exceptions;
using Xunit;

namespace LaunchDarkly.EventSource.Internal
{
    public class BufferedLineParserTest
    {
        // This test uses many permutations of how long the input lines are, how long are
        // the chunks that are returned by each read of the (fake) stream, how big the
        // read buffer is, and what the line terminators are, with a mix of single-byte
        // and multi-byte UTF8 characters, to verify that our parsing logic doesn't have
        // edge cases that fail.

        public struct LineTerminatorType
        {
            public string Name, Value;
        }

        private static readonly LineTerminatorType[] AllLineTerminators =
        {
            new LineTerminatorType { Name = "CR", Value = "\r" },
            new LineTerminatorType { Name = "LF", Value = "\n" },
            new LineTerminatorType { Name = "CRLF", Value = "\r\n" }
        };

        public static IEnumerable<object[]> AllParameters()
        {
            yield return new object[] { 100, AllLineTerminators[1] };
            //foreach (var chunkSize in new int[] { 1, 2, 3, 200 })
            //{
            //    foreach (LineTerminatorType lt in AllLineTerminators)
            //    {
            //        yield return new object[] { chunkSize, lt };
            //    }
            //}
        }
        
        [Theory]
        [MemberData(nameof(AllParameters))]
        public void ParseLinesShorterThanBuffer(int chunkSize, LineTerminatorType lt)
        {
            ParseLinesWithLengthsAndBufferSize(20, 25, 100, chunkSize, lt);
        }

        [Theory]
        [MemberData(nameof(AllParameters))]
        public void ParseLinesLongerThanBuffer(int chunkSize, LineTerminatorType lt)
        {
            ParseLinesWithLengthsAndBufferSize(20, 25, 100, chunkSize, lt);
        }

        private void ParseLinesWithLengthsAndBufferSize(int minLength, int maxLength, int bufferSize,
            int chunkSize, LineTerminatorType lt)
        {
            var lines = MakeLines(20, minLength, maxLength);
            var linesWithEnds = lines.Select(line => line + lt.Value);
            ParseLinesWithBufferSize(lines, linesWithEnds, bufferSize, chunkSize);
        }

        private async void ParseLinesWithBufferSize(
            IEnumerable<string> lines,
            IEnumerable<string> linesWithEnds,
            int bufferSize,
            int chunkSize
            )
        {
            IEnumerable<byte[]> chunks;
            if (chunkSize == 0)
            {
                chunks = linesWithEnds.Select(line => Encoding.UTF8.GetBytes(line));
            }
            else
            {
                var allBytes = Encoding.UTF8.GetBytes(string.Join("", linesWithEnds));
                chunks = allBytes
                    .Select((x, i) => new { Index = i, Value = x })
                    .GroupBy(x => x.Index / chunkSize)
                    .Select(x => x.Select(v => v.Value).ToArray());
            }

            var input = new FakeInputStream(chunks.ToArray());
            var parser = new BufferedLineParser(input.ReadAsync, bufferSize);

            var actualLines = new List<string>();
            var buf = new MemoryStream();
            var token = new CancellationTokenSource().Token;
            while (true)
            {
                try
                {
                    var chunk = await parser.ReadAsync(token);
                    buf.Write(chunk.Span.Data, chunk.Span.Offset, chunk.Span.Length);
                    if (chunk.EndOfLine)
                    {
                        actualLines.Add(Encoding.UTF8.GetString(buf.GetBuffer(), 0, (int)buf.Length));
                        buf.SetLength(0);
                    }
                }
                catch (StreamClosedByServerException)
                {
                    break;
                }
            }
            Assert.Equal(lines.ToList(), actualLines);
        }


        private static List<string> MakeLines(int count, int minLength, int maxLength)
        {
            String allChars = makeUtf8CharacterSet();
            var ret = new List<string>();
            for (int i = 0; i < count; i++)
            {
                int length = minLength + ((maxLength - minLength) * i) / count;
                StringBuilder s = new StringBuilder();
                for (int j = 0; j < length; j++)
                {
                    char ch = allChars[(i + j) % allChars.Length];
                    s.Append(ch);
                }
                ret.Add(s.ToString());
            }
            return ret;
        }

        private static string makeUtf8CharacterSet()
        {
            // Here we're mixing in some multi-byte characters so that we will sometimes end up
            // dividing a character across chunks.
            string singleByteCharacters = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
            string multiByteCharacters = "ØĤǶȵ϶ӾﺯᜅጺᏡ";
            StringBuilder s = new StringBuilder();
            int mi = 0;
            for (int si = 0; si < singleByteCharacters.Length; si++)
            {
                s.Append(singleByteCharacters[si]);
                if (si % 5 == 4)
                {
                    s.Append(multiByteCharacters[(mi++) % multiByteCharacters.Length]);
                }
            }
            return s.ToString();
        }
    }
}
