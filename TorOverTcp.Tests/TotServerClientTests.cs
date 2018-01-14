﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TorOverTcp.Exceptions;
using Xunit;

namespace TorOverTcp.Tests
{
	public class TotServerClientTests : IClassFixture<SharedFixture>
	{
		private SharedFixture SharedFixture { get; }

		public TotServerClientTests(SharedFixture fixture)
		{
			SharedFixture = fixture;
		}

		[Fact]
		public async Task SendRequesAsync()
		{
			Assert.Throws<ArgumentNullException>(() => new TotServer(null));

			var serverEndPoint = new IPEndPoint(IPAddress.Loopback, 5282);
			var server = new TotServer(serverEndPoint);

			try
			{
				await server.StartAsync();

				using (var tcpClient = new TcpClient())
				{
					Assert.Throws<ConnectionException>(() => new TotClient(tcpClient));

					await tcpClient.ConnectAsync(serverEndPoint.Address, serverEndPoint.Port);
					
					var totClient = new TotClient(tcpClient);
				}
			}
			finally
			{
				await server.StopAsync();
			}
		}
	}
}
