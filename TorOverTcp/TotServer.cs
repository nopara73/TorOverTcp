﻿using DotNetEssentials;
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
		private CancellationTokenSource AcceptTcpClientsTaskCancel { get; }

		private List<TotClient> Clients { get; }
		private AsyncLock ClientsLock { get; }

		public TotServer(IPEndPoint bindToEndPoint)
		{
			Guard.NotNull(nameof(bindToEndPoint), bindToEndPoint);

			InitLock = new AsyncLock();

			using (InitLock.Lock())
			{
				TcpListener = new TcpListener(bindToEndPoint);

				AcceptTcpClientsTaskCancel = new CancellationTokenSource();
				ClientsLock = new AsyncLock();

				using (ClientsLock.Lock())
				{
					Clients = new List<TotClient>();
				}
			}			
		}

		public async Task StartAsync()
		{
			using (await InitLock.LockAsync().ConfigureAwait(false))
			{
				TcpListener.Start();

				AcceptTcpClientsTask = AcceptTcpClientsAsync(AcceptTcpClientsTaskCancel.Token);

				Logger.LogInfo<TotServer>("Server started.");
			}
		}
		
		/// <param name="cancel">It usually isn't enough to cancel this task, after cancel TcpListener.Stop() must be called.</param>
		private async Task AcceptTcpClientsAsync(CancellationToken cancel)
		{
			Guard.NotNull(nameof(cancel), cancel);
			if (cancel == CancellationToken.None) throw new ArgumentException($"{nameof(cancel)} cannot be CancellationToken.None");

			while(true)
			{
				try
				{
					cancel.ThrowIfCancellationRequested();

					var tcpClient = await TcpListener.AcceptTcpClientAsync().ConfigureAwait(false); // TcpListener.Stop() will trigger ObjectDisposedException
					var totClient = new TotClient(tcpClient);

					using (await ClientsLock.LockAsync().ConfigureAwait(false))
					{
						Clients.Add(totClient);
					}
				}
				catch(OperationCanceledException ex)
				{
					Logger.LogInfo<TotServer>("Server stopped accepting incoming connections.");
					Logger.LogTrace<TotServer>(ex);
					return;
				}
				catch(ObjectDisposedException ex)
				{
					// If TcpListener.Stop() is called, this exception will be triggered.
					if(cancel.IsCancellationRequested)
					{
						Logger.LogInfo<TotServer>("Server stopped accepting incoming connections.");
						Logger.LogTrace<TotServer>(ex);
					}
					else // exception was triggered by something else
					{
						Logger.LogWarning<TotServer>(ex, LogLevel.Debug);
					}
					
				}
				catch (Exception ex)
				{
					Logger.LogWarning<TotServer>(ex, LogLevel.Debug);
				}
			}
		}

		public async Task StopAsync()
		{
			using (await InitLock.LockAsync().ConfigureAwait(false))
			{
				try
				{
					AcceptTcpClientsTaskCancel.Cancel();
					TcpListener.Stop();

					await AcceptTcpClientsTask.ConfigureAwait(false);
					AcceptTcpClientsTaskCancel.Dispose();

					using (await ClientsLock.LockAsync().ConfigureAwait(false))
					{
						foreach (var client in Clients)
						{
							try
							{
								client.TcpClient.Dispose();
							}
							catch (Exception ex)
							{
								Logger.LogWarning<TotServer>(ex, LogLevel.Debug);
							}
						}
					}
				}
				catch(Exception ex)
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
}
