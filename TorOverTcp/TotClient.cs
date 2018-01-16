using DotNetEssentials;
using DotNetEssentials.Logging;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TorOverTcp.Exceptions;
using TorOverTcp.TorOverTcp.Models.Fields;
using TorOverTcp.TorOverTcp.Models.Messages;

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
						ProcessMessageBytesAsync(buffer.Take(receiveCount).ToArray());
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

					ProcessMessageBytesAsync(builder.ToArray());
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
					Logger.LogWarning<TotClient>(ex, LogLevel.Debug);

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

				using (await StreamWriterLock.LockAsync().ConfigureAwait(false))
				{
					var messageType = new TotMessageType();
					messageType.FromByte(bytes[1]);

					var stream = TcpClient.GetStream();

					if (messageType == TotMessageType.Ping)
					{
						var request = new TotPing();
						request.FromBytes(bytes);

						var responseBytes = TotPong.Instance.ToBytes();
						
						await stream.WriteAsync(responseBytes, 0, responseBytes.Length).ConfigureAwait(false);
						await stream.FlushAsync().ConfigureAwait(false);
						return;						
					}
				}
			}
			catch (Exception ex)
			{
				var exception = new TotRequestException("Couldn't process the received message bytes.", ex);
				Logger.LogWarning<TotClient>(exception, LogLevel.Debug);
			}
		}

		#region Requests

		public async Task PingAsync()
		{
			using (await StreamWriterLock.LockAsync().ConfigureAwait(false))
			{
				var responseBytes = TotPong.Instance.ToBytes();

				var stream = TcpClient.GetStream();

				await stream.WriteAsync(responseBytes, 0, responseBytes.Length).ConfigureAwait(false);
				await stream.FlushAsync().ConfigureAwait(false);
			}
		}

		#endregion

		/// <summary>
		/// Also disposes the underlying TcpClient
		/// </summary>
		public async Task StopAsync()
		{
			try
			{
				using (await InitLock.LockAsync().ConfigureAwait(false))
				{
					TcpClient.Dispose();

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
