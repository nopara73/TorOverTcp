using System;
using System.Collections.Generic;
using System.Text;

namespace TorOverTcp.Exceptions
{
	public class TotRequestException : Exception
	{
		public TotRequestException(string message) : base(message)
		{

		}

		public TotRequestException(string message, Exception innerException) : base(message, innerException)
		{

		}
	}
}
