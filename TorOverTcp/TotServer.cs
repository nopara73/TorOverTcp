using DotNetEssentials;
using DotNetEssentials.Logging;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TorOverTcp
{
	public class TotServer
	{
		public TcpListener TcpListener { get; }
		private AsyncLock InitLock { get; }

		private Task AcceptTcpClientsTask { get; set; }

		private List<TotClient> Clients { get; }
		private AsyncLock ClientsLock { get; }

		public TotServer(IPEndPoint bindToEndPoint)
		{
			Guard.NotNull(nameof(bindToEndPoint), bindToEndPoint);

			InitLock = new AsyncLock();

			using (InitLock.Lock())
			{
				TcpListener = new TcpListener(bindToEndPoint);

				ClientsLock = new AsyncLock();

				using (ClientsLock.Lock())
				{
					Clients = new List<TotClient>();
				}

				AcceptTcpClientsTask = null;
			}
		}

		public async Task StartAsync()
		{
			using (await InitLock.LockAsync().ConfigureAwait(false))
			{
				TcpListener.Start();

				AcceptTcpClientsTask = AcceptTcpClientsAsync();

				Logger.LogInfo<TotServer>("Server started.");
			}
		}

		private async Task AcceptTcpClientsAsync()
		{
			while (true)
			{
				try
				{
					var tcpClient = await TcpListener.AcceptTcpClientAsync().ConfigureAwait(false); // TcpListener.Stop() will trigger ObjectDisposedException
					var totClient = new TotClient(tcpClient);

					using (await ClientsLock.LockAsync().ConfigureAwait(false))
					{
						Clients.Add(totClient);
					}
				}
				catch (ObjectDisposedException ex)
				{
					// If TcpListener.Stop() is called, this exception will be triggered.
					Logger.LogInfo<TotServer>("Server stopped accepting incoming connections.");
					Logger.LogTrace<TotServer>(ex);
					return;

				}
				catch (Exception ex)
				{
					Logger.LogWarning<TotServer>(ex, LogLevel.Debug);
				}
			}
		}

		public async Task StopAsync()
		{

			try
			{
				using (await InitLock.LockAsync().ConfigureAwait(false))
				{
					TcpListener.Stop();

					if (AcceptTcpClientsTask != null)
					{
						await AcceptTcpClientsTask.ConfigureAwait(false);
					}

					using (await ClientsLock.LockAsync().ConfigureAwait(false))
					{
						foreach (var client in Clients)
						{
							await client.StopAsync().ConfigureAwait(false);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning<TotServer>(ex, LogLevel.Debug);
			}
			finally
			{
				Logger.LogInfo<TotServer>("Server stopped.");
			}
		}
	}
}
