using System;
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
				using(var tcpClient2 = new TcpClient())
				{
					Assert.Throws<ConnectionException>(() => new TotClient(tcpClient));

					await tcpClient.ConnectAsync(serverEndPoint.Address, serverEndPoint.Port);

					await server.StopAsync();

					// tcpClient doesn't know if the server has stopped, so it will work
					var totClient = new TotClient(tcpClient);
					var thrownSocketExceptionFactoryExtendedSocketException = false;
					try
					{
						await tcpClient2.ConnectAsync(serverEndPoint.Address, serverEndPoint.Port);
					}
					// this will be the uncatchable SocketExceptionFactory+ExtendedSocketException
					catch (Exception ex) when (ex.Message.StartsWith("No connection could be made because the target machine actively refused it"))
					{
						thrownSocketExceptionFactoryExtendedSocketException = true;
					}
					Assert.True(thrownSocketExceptionFactoryExtendedSocketException);
					
					server = new TotServer(serverEndPoint);
					await server.StartAsync();

					await tcpClient2.ConnectAsync(serverEndPoint.Address, serverEndPoint.Port);
					var totClient2 = new TotClient(tcpClient2);
				}
			}
			finally
			{
				await server.StopAsync();
			}
		}
	}
}
