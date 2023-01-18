using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LaunchDarkly.Logging;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// A test implementation of ConnectStrategy that provides input streams to simulate server
	/// responses without running an HTTP server. We still do use an embedded HTTP server for
	/// tests that are specifically about HTTP behavior, but otherwise this class is better.
	/// There are some conditions that the embedded HTTP server cannot properly recreate, that
	/// could happen in real life with an external server: for instance, due to implementation
	/// details of the test server, it cannot send response headers until there is non-empty
	/// stream data.
	/// </summary>
	public class MockConnectStrategy : ConnectStrategy
	{
		public static readonly Uri MockOrigin = new Uri("http://test/origin");
		
        public override Uri Origin => MockOrigin;

		private readonly List<RequestHandler> _requestConfigs = new List<RequestHandler>();
		private volatile int _requestCount = 0;

		public readonly BlockingCollection<Client.Params> ReceivedConnections =
			new BlockingCollection<Client.Params>();
		public volatile bool Closed;

		public override Client CreateClient(Logger logger) =>
			new DelegatingClientImpl(this);

		public void ConfigureRequests(params RequestHandler[] requestConfigs)
		{
			foreach (RequestHandler r in requestConfigs)
			{
				_requestConfigs.Add(r);
			}
		}

		public abstract class RequestHandler : IDisposable
		{
			public abstract Task<Client.Result> ConnectAsync(Client.Params parameters);

			public virtual void Dispose() { }
		}

		public static RequestHandler RejectConnection(Exception ex) =>
			new ConnectionFailureHandler { Exception = ex };

		public static RequestHandler RespondWithDataAndThenEnd(string data) =>
			new StreamRequestHandler(new MemoryStream(Encoding.UTF8.GetBytes(data)));

		public static PipedStreamRequestHandler RespondWithStream() =>
			new PipedStreamRequestHandler();

		public static PipedStreamRequestHandler RespondWithDataAndStayOpen(params string[] chunks)
		{
			var s = RespondWithStream();
			s.ProvideData(chunks);
			return s;
		}

		private class ConnectionFailureHandler : RequestHandler
		{
			public Exception Exception { get; set; }

			public override Task<Client.Result> ConnectAsync(Client.Params p) =>
				throw Exception;
        }

		public class StreamRequestHandler : RequestHandler
		{
			private readonly Stream _stream;

			public StreamRequestHandler(Stream stream) { _stream = stream; }

            public override Task<Client.Result> ConnectAsync(Client.Params p) =>
                Task.FromResult(new Client.Result
                {
                    Stream = _stream,
                    Closer = this
                });
        }

		public class PipedStreamRequestHandler : RequestHandler, IDisposable
		{
            private readonly Stream _readStream;
            private readonly Stream _writeStream;
			private readonly BlockingCollection<byte[]> _chunks = new BlockingCollection<byte[]>();
			private volatile bool _closed = false;

			public PipedStreamRequestHandler()
			{
				var pipe = new Pipe();
                _readStream = pipe.Reader.AsStream();
                _writeStream = pipe.Writer.AsStream();
            }

			public override Task<Client.Result> ConnectAsync(Client.Params p)
			{
                var thread = new Thread(() =>
                {
                    while (true)
                    {
						try
						{
							var chunk = _chunks.Take(p.CancellationToken);
							if (_closed)
							{
								return;
							}
							_writeStream.Write(chunk, 0, chunk.Length);
						}
						catch (Exception) { }
                    }
                });
                thread.Start();
				return Task.FromResult(new Client.Result
				{
					Stream = _readStream,
					Closer = this
				});
			}

            public void ProvideData(params string[] chunks)
			{
				foreach (var chunk in chunks)
				{
					_chunks.Add(Encoding.UTF8.GetBytes(chunk));
				}
			}

			public override void Dispose()
			{
				_closed = true;
			}
		}

		private class DelegatingClientImpl : Client
		{
			private readonly MockConnectStrategy _owner;

			public DelegatingClientImpl(MockConnectStrategy owner) { _owner = owner; }

            public override Task<Result> ConnectAsync(Params parameters)
            {
				if (_owner._requestConfigs.Count == 0)
				{
					throw new InvalidOperationException("MockConnectStrategy was not configured for any requests");
				}
				_owner.ReceivedConnections.Add(parameters);
				var handler = _owner._requestConfigs[_owner._requestCount];
				if (_owner._requestCount < _owner._requestConfigs.Count - 1)
				{
					_owner._requestCount++; // reuse last entry for all subsequent requests
				}
				return handler.ConnectAsync(parameters);
            }

            public override void Dispose()
            {
				_owner.Closed = true;
            }
        }
	}
}

