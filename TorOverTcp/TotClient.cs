using DotNetEssentials;
using DotNetEssentials.Logging;
using Nito.AsyncEx;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TorOverTcp.Exceptions;
using TorOverTcp.TorOverTcp.Models.Fields;
using TorOverTcp.TorOverTcp.Models.Messages;
using TorOverTcp.TorOverTcp.Models.Messages.Bases;

namespace TorOverTcp
{
	public class TotClient
	{
		public TcpClient TcpClient { get; }
		private AsyncLock InitLock { get; }

		private Task ListenNetworkStreamTask { get; set; }

		public event EventHandler<Exception> Disconnected;
		private void OnDisconnected(Exception exception) => Disconnected?.Invoke(this, exception);

		private AsyncLock StreamWriterLock { get; }

		public ConcurrentDictionary<TotMessageId, TotMessageBase> ResponseCache { get; }

		public event EventHandler<TotRequest> RequestArrived;
		private void OnRequestArrived(TotRequest request) => RequestArrived?.Invoke(this, request);

		/// <param name="connectedClient">Must be already connected.</param>
		public TotClient(TcpClient connectedClient)
		{
			Guard.NotNull(nameof(connectedClient), connectedClient);

			InitLock = new AsyncLock();

			using (InitLock.Lock())
			{
				if (!connectedClient.Connected)
				{
					throw new ConnectionException($"{nameof(connectedClient)} is not connected.");
				}
				TcpClient = connectedClient;

				ListenNetworkStreamTask = null;
				StreamWriterLock = new AsyncLock();
				ResponseCache = new ConcurrentDictionary<TotMessageId, TotMessageBase>();
			}
		}

		public async Task StartAsync()
		{
			using (await InitLock.LockAsync().ConfigureAwait(false))
			{
				ListenNetworkStreamTask = ListenNetworkStreamAsync();
			}
		}

		private async Task ListenNetworkStreamAsync()
		{
			while (true)
			{
				try
				{
					var bufferSize = TcpClient.ReceiveBufferSize;
					var buffer = new byte[bufferSize];
					var stream = TcpClient.GetStream();
					var receiveCount = await stream.ReadAsync(buffer, 0, bufferSize).ConfigureAwait(false); // TcpClient.Disposep() will trigger ObjectDisposedException
					if (receiveCount <= 0)
					{
						throw new ConnectionException($"Client lost connection.");
					}

					// if we could fit everything into our buffer, then we get our message
					if (!stream.DataAvailable)
					{
						foreach (var messageBytes in TotMessageBase.SplitByMessages(buffer.Take(receiveCount).ToArray()))
						{
							ProcessMessageBytesAsync(messageBytes);
						}
						continue;
					}

					// while we have data available, start building a bytearray
					var builder = new ByteArrayBuilder();
					builder.Append(buffer.Take(receiveCount).ToArray());
					while (stream.DataAvailable)
					{
						Array.Clear(buffer, 0, buffer.Length);
						receiveCount = await stream.ReadAsync(buffer, 0, bufferSize).ConfigureAwait(false);
						if (receiveCount <= 0)
						{
							throw new ConnectionException($"Client lost connection.");
						}
						builder.Append(buffer.Take(receiveCount).ToArray());
					}

					foreach (var messageBytes in TotMessageBase.SplitByMessages(builder.ToArray()))
					{
						ProcessMessageBytesAsync(messageBytes);
					}
					continue;
				}
				catch (ObjectDisposedException ex)
				{
					// If TcpListener.Stop() is called, this exception will be triggered.
					Logger.LogInfo<TotClient>("Client is disposed incoming connections.");
					Logger.LogTrace<TotClient>(ex);
					OnDisconnected(ex);
					return;
				}
				catch (ConnectionException ex)
				{
					Logger.LogDebug<TotClient>(ex, LogLevel.Debug);

					OnDisconnected(ex);
					await Task.Delay(3000).ConfigureAwait(false); // wait 3 sec, then retry
				}
				catch (IOException ex)
				{
					Logger.LogDebug<TotClient>(ex, LogLevel.Debug);

					OnDisconnected(ex);
					await Task.Delay(3000).ConfigureAwait(false); // wait 3 sec, then retry
				}
				catch (Exception ex)
				{
					Logger.LogWarning<TotClient>(ex, LogLevel.Debug);

					OnDisconnected(ex);
					await Task.Delay(3000).ConfigureAwait(false); // wait 3 sec, then retry
				}
			}
		}
		
		// async void is fine, because in case of exception it logs and it should not be awaited
		private async void ProcessMessageBytesAsync(byte[] bytes)
		{
			try
			{
				Guard.NotNull(nameof(bytes), bytes);
				
				var messageType = new TotMessageType();
				messageType.FromByte(bytes[1]);

				if (messageType == TotMessageType.Pong)
				{
					var response = new TotPong();
					response.FromBytes(bytes);
					ResponseCache.TryAdd(response.MessageId, response);

				}
				if (messageType == TotMessageType.Response)
				{
					var response = new TotResponse();
					response.FromBytes(bytes);
					ResponseCache.TryAdd(response.MessageId, response);
					return;
				}

				var stream = TcpClient.GetStream();

				var requestId = TotMessageId.Random;
				try
				{
					if (messageType == TotMessageType.Ping)
					{
						var request = new TotPing();
						request.FromBytes(bytes);
						requestId = request.MessageId;
						AssertVersion(request.Version, TotVersion.Version1);

						var responseBytes = TotPong.Instance(request.MessageId).ToBytes();
						await SendAsync(responseBytes).ConfigureAwait(false);
						return;
					}

					if (messageType == TotMessageType.Request)
					{
						var request = new TotRequest();
						request.FromBytes(bytes);
						requestId = request.MessageId;
						AssertVersion(request.Version, TotVersion.Version1);						

						OnRequestArrived(request);
						return;
					}
				}
				catch (TotRequestException)
				{
					await RespondVersionMismatchAsync(requestId).ConfigureAwait(false);
					throw;
				}
				catch
				{
					await SendAsync(new TotResponse(TotPurpose.BadRequest, new TotContent("Couldn't process the received message bytes."), requestId).ToBytes()).ConfigureAwait(false);
					throw;
				}
			}
			catch (Exception ex)
			{
				var exception = new TotRequestException("Couldn't process the received message bytes.", ex);
				Logger.LogWarning<TotClient>(exception, LogLevel.Debug);
			}
		}

		#region Requests

		public async Task PingAsync(int timeout = 3000)
		{
			var request = TotPing.Instance;
			var requestBytes = request.ToBytes();

			await SendAsync(requestBytes).ConfigureAwait(false);

			var response = await ReceiveAsync(request.MessageId, request.Version, timeout).ConfigureAwait(false) as TotPong;
		}

		/// <summary>
		/// Throws TotRequestException if not success response.
		/// </summary>
		public async Task<TotContent> RequestAsync(string request, int timeout = 3000)
		{
			Guard.NotNullOrEmptyOrWhitespace(nameof(request), request);
			return await RequestAsync(new TotRequest(request), timeout).ConfigureAwait(false);
		}

		/// <summary>
		/// Throws TotRequestException if not success response.
		/// </summary>
		public async Task<TotContent> RequestAsync(TotRequest request, int timeout = 3000)
		{
			Guard.MinimumAndNotNull(nameof(timeout), timeout, 1);

			var requestBytes = request.ToBytes();

			await SendAsync(requestBytes).ConfigureAwait(false);

			var response = await ReceiveAsync(request.MessageId, request.Version, timeout).ConfigureAwait(false) as TotResponse;

			// Note: version assertion is inside receive.
			AssertSuccess(response);

			return response.Content;
		}

		/// <summary>
		/// Throws TotRequestException if not success response.
		/// </summary>
		public async Task<TotContent> SubscribeAsync(string purpose, int timeout = 3000)
		{
			purpose = Guard.NotNullOrEmptyOrWhitespace(nameof(purpose), purpose, trim: true);
			Guard.MinimumAndNotNull(nameof(timeout), timeout, 1);

			var request = new TotSubscribeRequest(purpose);
			var requestBytes = request.ToBytes();

			await SendAsync(requestBytes).ConfigureAwait(false);

			var response = await ReceiveAsync(request.MessageId, request.Version, timeout).ConfigureAwait(false) as TotResponse;
			
			// Note: version assertion is inside receive.
			AssertSuccess(response);

			return response.Content;
		}

		private async Task<TotMessageBase> ReceiveAsync(TotMessageId expectedMessageId, TotVersion expectedVersion, int timeout)
		{
			Guard.NotNull(nameof(expectedMessageId), expectedMessageId);
			Guard.NotNull(nameof(expectedVersion), expectedVersion);
			Guard.MinimumAndNotNull(nameof(timeout), timeout, 1);

			int delay = 10;
			int maxDelayCount = timeout / delay;
			int delayCount = 0;
			while (!ResponseCache.ContainsKey(expectedMessageId))
			{
				if (delayCount > maxDelayCount)
				{
					throw new TimeoutException($"No response arrived within the specified timeout: {timeout} milliseconds.");
				}

				await Task.Delay(delay).ConfigureAwait(false);

				delayCount++;
			}
			
			ResponseCache.TryRemove(expectedMessageId, out TotMessageBase response);
			AssertVersion(expectedVersion, response.Version);
			return response;
		}

		public async Task SendAsync(byte[] requestBytes)
		{
			Guard.NotNull(nameof(requestBytes), requestBytes);

			var stream = TcpClient.GetStream();

			using (await StreamWriterLock.LockAsync().ConfigureAwait(false))
			{
				await stream.WriteAsync(requestBytes, 0, requestBytes.Length).ConfigureAwait(false);
				await stream.FlushAsync().ConfigureAwait(false);
			}
		}

		/// <summary>
		/// The request was malformed.
		/// </summary>
		public async Task RespondBadRequestAsync(TotMessageId messageId, string errorContent = "")
		{
			Guard.NotNull(nameof(messageId), messageId);
			var response = new TotResponse(TotPurpose.BadRequest, messageId);

			if (!string.IsNullOrWhiteSpace(errorContent))
			{ 
				response.Content = new TotContent(errorContent);
			}
			await SendAsync(response.ToBytes()).ConfigureAwait(false);
		}

		/// <summary>
		/// The server was not able to execute the Request properly.
		/// </summary>
		public async Task RespondUnsuccessfulRequestAsync(TotMessageId messageId, string errorContent = "")
		{
			Guard.NotNull(nameof(messageId), messageId);
			var response = new TotResponse(TotPurpose.UnsuccessfulRequest, messageId);

			if (!string.IsNullOrWhiteSpace(errorContent))
			{
				response.Content = new TotContent(errorContent);
			}
			await SendAsync(response.ToBytes()).ConfigureAwait(false);
		}

		public async Task RespondSuccessAsync(TotMessageId messageId)
		{
			Guard.NotNull(nameof(messageId), messageId);
			var response = new TotResponse(TotPurpose.Success, messageId);
			
			await SendAsync(response.ToBytes()).ConfigureAwait(false);
		}

		public async Task RespondSuccessAsync(TotMessageId messageId, TotContent content)
		{
			Guard.NotNull(nameof(messageId), messageId);
			Guard.NotNull(nameof(content), content);
			var response = new TotResponse(TotPurpose.Success, content, messageId);

			await SendAsync(response.ToBytes()).ConfigureAwait(false);
		}

		private async Task RespondVersionMismatchAsync(TotMessageId messageId)
		{
			Guard.NotNull(nameof(messageId), messageId);
			await SendAsync(TotResponse.VersionMismatch(messageId).ToBytes()).ConfigureAwait(false);
		}

		#endregion

		private static void AssertSuccess(TotResponse response)
		{
			if (response.Purpose != TotPurpose.Success)
			{
				if (response.Content == TotContent.Empty)
				{
					throw new TotRequestException($"Server responded with {response.Purpose}.");
				}
				else
				{
					throw new TotRequestException($"Server responded with {response.Purpose}. Details: {response.Content}"); // DON'T put . at the end, that's the responsibility of the creator of the content.
				}
			}
		}

		private static void AssertVersion(TotVersion expected, TotVersion actual)
		{
			if (expected != actual)
			{
				throw new TotRequestException($"Server responded with wrong version. Expected: {expected}. Actual: {actual}.");
			}
		}

		/// <summary>
		/// Also disposes the underlying TcpClient
		/// </summary>
		public async Task StopAsync()
		{
			try
			{
				using (await InitLock.LockAsync().ConfigureAwait(false))
				{
					TcpClient?.Dispose();

					if (ListenNetworkStreamTask != null)
					{
						await ListenNetworkStreamTask.ConfigureAwait(false);
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning<TotClient>(ex, LogLevel.Debug);
			}
		}
	}
}
