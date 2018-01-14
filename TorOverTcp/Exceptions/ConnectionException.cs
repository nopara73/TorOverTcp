using System;
using System.Collections.Generic;
using System.Text;

namespace TorOverTcp.Exceptions
{
	public class ConnectionException : Exception
	{
		public ConnectionException(string message) : base(message)
		{

		}

		public ConnectionException(string message, Exception innerException) : base(message, innerException)
		{

		}
	}
}
