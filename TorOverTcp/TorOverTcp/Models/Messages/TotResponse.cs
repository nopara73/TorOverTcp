using DotNetEssentials;
using TorOverTcp.TorOverTcp.Models.Fields;
using TorOverTcp.TorOverTcp.Models.Messages.Bases;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TorOverTcp.TorOverTcp.Models.Messages
{
	/// <summary>
	/// Issued by the server. A Request MUST precede it.
	/// </summary>
	public class TotResponse : TotMessageBase
	{
		#region Statics

		public static TotResponse Success(TotMessageId messageId) => new TotResponse(TotPurpose.Success, messageId);

		/// <summary>
		/// The request was malformed. BadRequest SHOULD be issued in case of client side errors.
		/// </summary>
		public static TotResponse BadRequest(TotMessageId messageId) => new TotResponse(TotPurpose.BadRequest, messageId);

		public static TotResponse VersionMismatch(TotMessageId messageId) => new TotResponse(TotPurpose.VersionMismatch, messageId);

		/// <summary>
		/// The server was not able to execute the Request properly. UnsuccessfulReqeust SHOULD be issued in case of server side errors.
		/// </summary>
		public static TotResponse UnsuccessfulRequest(TotMessageId messageId) => new TotResponse(TotPurpose.UnsuccessfulRequest, messageId);

		#endregion

		#region ConstructorsAndInitializers

		public TotResponse() : base()
		{

		}

		/// <param name="purpose">Success, BadRequest, VersionMismatch, UnsuccessfulRequest</param>
		public TotResponse(TotPurpose purpose, TotMessageId messageId) : this(purpose, TotContent.Empty, messageId)
		{

		}

		/// <param name="purpose">Success, BadRequest, VersionMismatch, UnsuccessfulRequest</param>
		public TotResponse(TotPurpose purpose, TotContent content, TotMessageId messageId) : base(TotMessageType.Response, messageId,  purpose, content)
		{
			Guard.NotNull(nameof(purpose), purpose);
			if(purpose != TotPurpose.Success 
				&& purpose != TotPurpose.BadRequest 
				&& purpose != TotPurpose.VersionMismatch 
				&& purpose != TotPurpose.UnsuccessfulRequest)
			{
				throw new ArgumentException($"{nameof(purpose)} of {nameof(TotResponse)} can only be {TotPurpose.Success}, {TotPurpose.BadRequest}, {TotPurpose.VersionMismatch} or {TotPurpose.UnsuccessfulRequest}. Actual: {purpose}.");
			}
		}

		#endregion

		#region Serialization

		public override void FromBytes(byte[] bytes)
		{
			Guard.NotNullOrEmpty(nameof(bytes), bytes);

			base.FromBytes(bytes);

			var expectedMessageType = TotMessageType.Response;
			if (MessageType != expectedMessageType)
			{
				throw new FormatException($"Wrong {nameof(MessageType)}. Expected: {expectedMessageType}. Actual: {MessageType}.");
			}

			var validPurposes = new TotPurpose[] { TotPurpose.Success, TotPurpose.BadRequest, TotPurpose.VersionMismatch, TotPurpose.UnsuccessfulRequest };
			
			if (!validPurposes.Contains(Purpose))
			{
				throw new FormatException($"Wrong {nameof(Purpose)}. Value: {Purpose}.");
			}
		}

		#endregion
	}
}
