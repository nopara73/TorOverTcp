using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
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
		public async Task CanInitializeAsync()
		{
			Assert.Throws<ArgumentNullException>(() => new TotServer(null));

			var serverEndPoint = new IPEndPoint(IPAddress.Loopback, new Random().Next(5000, 5500));
			var server = new TotServer(serverEndPoint);

			await server.StopAsync(); // make sure calling stop doesn't throw exception

			var tcpClient = new TcpClient();
			var tcpClient2 = new TcpClient();
			var tcpClient3 = new TcpClient();
			var tcpClient4 = new TcpClient();
			try
			{
				await server.StartAsync();

				Assert.Throws<ConnectionException>(() => new TotClient(tcpClient));

				await tcpClient.ConnectAsync(serverEndPoint.Address, serverEndPoint.Port);
				await tcpClient3.ConnectAsync(serverEndPoint.Address, serverEndPoint.Port);
				await tcpClient4.ConnectAsync(serverEndPoint.Address, serverEndPoint.Port);
				var totClient3 = new TotClient(tcpClient3);
				var totClient4 = new TotClient(tcpClient4);
				await totClient4.StartAsync();
				await totClient4.StopAsync();

				await server.StopAsync();

				// tcpClient doesn't know if the server has stopped, so it will work
				var totClient = new TotClient(tcpClient);
				await totClient3.StopAsync(); // make sure it doesn't throw exception
				Assert.Throws<ConnectionException>(() => totClient3 = new TotClient(tcpClient3));

				await totClient.StartAsync(); // Start will not fail, but rather retry periodically
				totClient.Disconnected += TotClient_Disconnected_CanInitializeAsync;

				while (0 == Interlocked.Read(ref _totClient_Disconnected_CanInitializeAsyncCalled))
				{
					await Task.Delay(10);
				}

				totClient.Disconnected -= TotClient_Disconnected_CanInitializeAsync;
				await totClient.StopAsync();

				var thrownSocketExceptionFactoryExtendedSocketException = false;
				try
				{
					await tcpClient2.ConnectAsync(serverEndPoint.Address, serverEndPoint.Port);
				}
				// this will be the uncatchable SocketExceptionFactory+ExtendedSocketException
				catch (Exception ex) when (ex.Message.StartsWith("No connection could be made because the target machine actively refused it"))
				{
					if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
					{
						thrownSocketExceptionFactoryExtendedSocketException = true;
					}
				}
				catch (Exception ex) when (ex.Message.StartsWith("Connection refused"))
				{
					if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
					{
						thrownSocketExceptionFactoryExtendedSocketException = true;
					}
				}
				Assert.True(thrownSocketExceptionFactoryExtendedSocketException);

				server = new TotServer(serverEndPoint);

				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					await server.StartAsync();

					await tcpClient2.ConnectAsync(serverEndPoint.Address, serverEndPoint.Port);
					var totClient2 = new TotClient(tcpClient2);
					await totClient2.StartAsync();
					await totClient2.StopAsync();
				}
				else // on non-windows platforms this is not possible (address already in use
				{
					await Assert.ThrowsAsync<SocketException>(async () => await server.StartAsync());
				}
			}
			finally
			{
				await server.StopAsync();
			}
		}

		private long _totClient_Disconnected_CanInitializeAsyncCalled = 0;
		private void TotClient_Disconnected_CanInitializeAsync(object sender, Exception e)
		{
			Assert.IsType<TotClient>(sender);
			Assert.IsType<ConnectionException>(e);

			Interlocked.Increment(ref _totClient_Disconnected_CanInitializeAsyncCalled);
		}

		[Fact]
		public async Task PingPongAsync()
		{
			var serverEndPoint = new IPEndPoint(IPAddress.Loopback, new Random().Next(5000, 5500));
			var server = new TotServer(serverEndPoint);
			var tcpClient = new TcpClient();
			TotClient totClient = null;

			try
			{
				await server.StartAsync();
				await tcpClient.ConnectAsync(serverEndPoint.Address, serverEndPoint.Port);

				totClient = new TotClient(tcpClient);

				await totClient.StartAsync();

				await totClient.PingAsync();
			}
			finally
			{
				await totClient?.StopAsync();
				tcpClient?.Dispose(); // this is when tcpClient.ConnectAsync fails
				await server.StopAsync();
			}
		}
	}
}
