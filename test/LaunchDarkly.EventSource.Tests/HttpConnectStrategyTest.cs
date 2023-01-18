using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LaunchDarkly.EventSource.Exceptions;
using LaunchDarkly.TestHelpers.HttpTest;
using Xunit;

using static LaunchDarkly.EventSource.TestHelpers;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// Tests of the basic client/request configuration methods and HTTP functionality
    /// in HttpConnectStrategy, using an embedded HTTP server as a target, but without
    /// using EventSource.
    /// </summary>
    public class HttpConnectStrategyTest : BaseTest
    {
        private static readonly Uri uri = new Uri("http://test");

        private static readonly HttpConnectStrategy baseStrategy = ConnectStrategy.Http(uri);

        [Fact]
        public void HttpClient()
        {
            using (var client = new HttpClient())
            {
                Assert.Same(client, MakeClientFrom(ConnectStrategy.Http(uri).HttpClient(client)));
            }
        }

        [Fact]
        public void HttpClientModifier()
        {
            using (var client = MakeClientFrom(ConnectStrategy.Http(uri)
                .HttpClientModifier(c => c.MaxResponseContentBufferSize = 999)))
            {
                Assert.Equal(999, client.MaxResponseContentBufferSize);
            }
        }

        [Fact]
        public void ResponseStartTimeout()
        {
            TimeSpan timeout = TimeSpan.FromMilliseconds(100);
            using (var client = MakeClientFrom(ConnectStrategy.Http(uri)
                .ResponseStartTimeout(timeout)))
            {
                Assert.Equal(timeout, client.Timeout);
            }
        }

        [Fact]
        public async Task RequestDefaultProperties()
        {
            var r = await DoRequestFrom(baseStrategy);

            Assert.Equal("GET", r.Method);
            Assert.Equal("", r.Body);
            Assert.Equal("text/event-stream", r.Headers["accept"]);
            Assert.Null(r.Headers["last-event-id"]);
        }

        [Fact]
        public async Task RequestCustomHeaders()
        {
            var headers = new Dictionary<string, string> { { "name1", "value1" }, { "name2", "value2" } };

            var r = await DoRequestFrom(baseStrategy
                .Headers(headers)
                .Header("name3", "value3")
                );

            Assert.Equal("value1", r.Headers["name1"]);
            Assert.Equal("value2", r.Headers["name2"]);
            Assert.Equal("value3", r.Headers["name3"]);
        }

        [Fact]
        public async Task RequestLastEventId()
        {
            var r = await DoRequestFrom(baseStrategy, "abc123");

            Assert.Equal("abc123", r.Headers["last-event-id"]);
        }

        [Fact]
        public async Task RequestCustomMethodWithBody()
        {
            var r = await DoRequestFrom(baseStrategy
                .Method(HttpMethod.Post)
                .RequestBodyFactory(() => new StringContent("{}"))
                );

            Assert.Equal("POST", r.Method);
            Assert.Equal("{}", r.Body);
        }

        [Fact]
        public async Task RequestModifier()
        {
            var r = await DoRequestFrom(baseStrategy
                .HttpRequestModifier(req => req.RequestUri = new Uri(req.RequestUri, "abc"))
                );

            Assert.Equal("/abc", r.Path);
        }

        [Fact]
        public async Task CanReadFromChunkedResponseStream()
        {
            var fakeStream = Handlers.SSE.Start().Then(Handlers.WriteChunkString("hello "))
                .Then(Handlers.WriteChunkString("world"));
            using (var server = HttpServer.Start(fakeStream))
            {
                using (var client = baseStrategy.Uri(server.Uri).CreateClient(_testLogger))
                {
                    var result = await client.ConnectAsync(new ConnectStrategy.Client.Params());
                    try
                    {
                        var stream = result.Stream;
                        var b = new byte[100];
                        Assert.Equal(6, await stream.ReadAsync(b, 0, 6));
                        Assert.Equal(5, await stream.ReadAsync(b, 6, 5));
                        Assert.Equal("hello world", Encoding.UTF8.GetString(b, 0, 11));
                    }
                    finally
                    {
                        result.Closer.Dispose();
                    }
                }
            }
        }

        [Theory]
        [InlineData(HttpStatusCode.NoContent)]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.RequestTimeout)]
        [InlineData(HttpStatusCode.Unauthorized)]
        public async Task RequestFailsWithHttpError(HttpStatusCode status)
        {
            var response = Handlers.Status(status);
            using (var server = HttpServer.Start(response))
            {
                using (var client = baseStrategy.Uri(server.Uri).CreateClient(_testLogger))
                {
                    var ex = await Assert.ThrowsAnyAsync<StreamHttpErrorException>(
                        async () => await client.ConnectAsync(new ConnectStrategy.Client.Params()));
                    Assert.Equal((int)status, ex.Status);
                }
            }
        }

        [Fact]
        public async Task RequestFailsWithIncorrectContentType()
        {
            var response = Handlers.Status(200).Then(Handlers.BodyString("text/html", "testing"));
            using (var server = HttpServer.Start(response))
            {
                using (var client = baseStrategy.Uri(server.Uri).CreateClient(_testLogger))
                {
                    var ex = await Assert.ThrowsAnyAsync<StreamContentException>(
                        async () => await client.ConnectAsync(new ConnectStrategy.Client.Params()));
                    Assert.Equal("text/html", ex.ContentType.ToString());
                }
            }
        }

        [Fact]
        public async Task RequestFailsWithIncorrectContentEncoding()
        {
            var badEncoding = Encoding.GetEncoding("iso-8859-1");
            var response = Handlers.StartChunks("text/event-stream", badEncoding)
                .Then(WriteComment(""));
            using (var server = HttpServer.Start(response))
            {
                using (var client = baseStrategy.Uri(server.Uri).CreateClient(_testLogger))
                {
                    var ex = await Assert.ThrowsAnyAsync<StreamContentException>(
                        async () => await client.ConnectAsync(new ConnectStrategy.Client.Params()));
                    Assert.Equal(badEncoding, ex.ContentEncoding);
                }
            }
        }

        private HttpClient MakeClientFrom(HttpConnectStrategy hcs) =>
            ((HttpConnectStrategy.ClientImpl)hcs.CreateClient(_testLogger)).HttpClient;

        private async Task<RequestInfo> DoRequestFrom(HttpConnectStrategy hcs, string lastEventId = null)
        {
            var fakeStream = StartStream().Then(WriteComment(""));
            using (var server = HttpServer.Start(fakeStream))
            {
                using (var client = hcs.Uri(server.Uri).CreateClient(_testLogger))
                {
                    var p = new ConnectStrategy.Client.Params { LastEventId = lastEventId };
                    var result = await client.ConnectAsync(p);
                    result.Closer.Dispose();
                    return server.Recorder.RequireRequest();
                }
            }
        }
    }
}
