using System;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using LaunchDarkly.EventSource.Exceptions;
using LaunchDarkly.TestHelpers;
using Xunit;

namespace LaunchDarkly.EventSource.Exceptions
{
    public class ExceptionTypesTest
    {
        [Fact]
        public void TestNonParameterizedExceptions()
        {
            TypeBehavior.CheckEqualsAndHashCode<Exception>(
                () => new ReadTimeoutException(),
                () => new StreamClosedByCallerException(),
                () => new StreamClosedByServerException(),
                () => new StreamClosedWithIncompleteMessageException()
                );
        }

        [Fact]
        public void TestStreamContentException()
        {
            var ex = new StreamContentException(
                new MediaTypeHeaderValue("text/html"),
                Encoding.UTF32);
            Assert.Equal(new MediaTypeHeaderValue("text/html"), ex.ContentType);
            Assert.Equal(Encoding.UTF32, ex.ContentEncoding);

            TypeBehavior.CheckEqualsAndHashCode(
                () => new StreamContentException(
                    new MediaTypeHeaderValue("text/event-stream"),
                    Encoding.UTF32),
                () => new StreamContentException(
                    new MediaTypeHeaderValue("text/html"),
                    Encoding.UTF32),
                () => new StreamContentException(
                    new MediaTypeHeaderValue("text/html"),
                    Encoding.UTF8)
                );
        }

        [Fact]
        public void TestStreamHttpErrorException()
        {
            Assert.Equal(HttpStatusCode.BadRequest,
                new StreamHttpErrorException(HttpStatusCode.BadRequest).Status);

            TypeBehavior.CheckEqualsAndHashCode(
                () => new StreamHttpErrorException(HttpStatusCode.BadRequest),
                () => new StreamHttpErrorException(HttpStatusCode.InternalServerError)
                );
        }
    }
}
