using DotNetEssentials;
using TorOverTcp.TorOverTcp.Models.Fields;
using TorOverTcp.TorOverTcp.Models.Messages.Bases;
using System;
using System.Collections.Generic;
using System.Text;

namespace TorOverTcp.TorOverTcp.Models.Messages
{
	/// <summary>
	/// A Pong MUST follow it.
	/// </summary>
	public class TotPing : TotMessageBase
	{
		#region Statics

		public static TotPing Instance => new TotPing(TotContent.Empty);

		#endregion

		#region ConstructorsAndInitializers

		public TotPing() : base()
		{

		}

		public TotPing(TotContent content) : base(TotMessageType.Ping, TotMessageId.Random, TotPurpose.Ping, content)
		{

		}

		#endregion

		#region Serialization

		public override void FromBytes(byte[] bytes)
		{
			Guard.NotNullOrEmpty(nameof(bytes), bytes);

			base.FromBytes(bytes);

			var expectedMessageType = TotMessageType.Ping;
			if (MessageType != expectedMessageType)
			{
				throw new FormatException($"Wrong {nameof(MessageType)}. Expected: {expectedMessageType}. Actual: {MessageType}.");
			}

			var expectedPurpose = TotPurpose.Ping;
			if (Purpose != expectedPurpose)
			{
				throw new FormatException($"Wrong {nameof(Purpose)}. Expected: {expectedPurpose}. Actual: {Purpose}.");
			}
		}

		#endregion
	}
}
