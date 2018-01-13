using DotNetEssentials.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace TorOverTcp.Tests
{
	public class SharedFixture : IDisposable
	{
		public SharedFixture()
		{
			// Initialize tests...

			Logger.SetMinimumLevel(LogLevel.Trace);
			Logger.SetModes(LogMode.Debug, LogMode.File);
			Logger.SetFilePath("TestLogs.txt");
		}

		public void Dispose()
		{
			// Cleanup tests...
		}
	}
}
