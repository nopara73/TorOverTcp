using DotNetEssentials;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace TorOverTcp
{
    public class TotClient
    {
		public TcpClient TcpClient { get; }
		
		/// <param name="connectedClient">Must be already connected.</param>
		public TotClient(TcpClient connectedClient)
		{
			Guard.NotNull(nameof(connectedClient), connectedClient);
			if(!connectedClient.Connected)
			{
				throw new ArgumentException($"{nameof(connectedClient)} is not connected.");
			}
			TcpClient = connectedClient;
		}
	}
}
