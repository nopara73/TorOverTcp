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
using TorOverTcp.TorOverTcp.Models.Messages;

namespace TorOverTcp
{
	public class TotServer
	{
		public TcpListener TcpListener { get; }
		private AsyncLock InitLock { get; }

		private Task AcceptTcpClientsTask { get; set; }

		private List<TotClient> Clients { get; }
		private AsyncLock ClientsLock { get; }

		public event EventHandler<TotRequest> RequestArrived;
		private void OnRequestArrived(TotClient client, TotRequest request) => RequestArrived?.Invoke(client, request);

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

					await totClient.StartAsync().ConfigureAwait(false);
					totClient.RequestArrived += TotClient_RequestArrived;
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

		private void TotClient_RequestArrived(object sender, TotRequest request) => OnRequestArrived(sender as TotClient, request);

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
							client.RequestArrived -= TotClient_RequestArrived;
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
