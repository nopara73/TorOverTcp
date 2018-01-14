using DotNetEssentials;
using DotNetEssentials.Logging;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TorOverTcp.Exceptions;

namespace TorOverTcp
{
	public class TotClient
	{
		public TcpClient TcpClient { get; }
		private AsyncLock InitLock { get; }

		private Task ListenNetworkStreamTask { get; set; }

		public event EventHandler<Exception> Disconnected;
		private void OnDisconnected(Exception exception) => Disconnected?.Invoke(this, exception);

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
					var buffer = new byte[1024];
					var receiveCount = await TcpClient.GetStream().ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false); // TcpClient.Disposep() will trigger ObjectDisposedException
					if (receiveCount <= 0)
					{
						throw new ConnectionException($"Client lost connection.");
					}
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
