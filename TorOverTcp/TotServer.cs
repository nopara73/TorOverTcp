using DotNetEssentials;
using DotNetEssentials.Logging;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TorOverTcp
{
	public class TotServer
    {
		public TcpListener TcpListener { get; }

		public AsyncLock InitLock { get; }

		public TotServer(IPEndPoint bindToEndPoint)
		{
			Guard.NotNull(nameof(bindToEndPoint), bindToEndPoint);

			InitLock = new AsyncLock();

			using (InitLock.Lock())
			{
				TcpListener = new TcpListener(bindToEndPoint);
			}			
		}

		public async Task StartAsync()
		{
			using (await InitLock.LockAsync().ConfigureAwait(false))
			{
				TcpListener.Start();
			}
		}

		public async Task StopAsync()
		{
			using (await InitLock.LockAsync().ConfigureAwait(false))
			{
				try
				{
					TcpListener.Stop();
				}
				catch(Exception ex)
				{
					Logger.LogWarning<TotServer>(ex, LogLevel.Debug);
				}
			}
		}
	}
}
