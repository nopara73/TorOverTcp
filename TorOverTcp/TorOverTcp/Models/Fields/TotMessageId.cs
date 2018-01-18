using DotNetEssentials;
using System;
using System.Collections.Generic;
using System.Text;
using TorOverTcp.Bases;

namespace TorOverTcp.TorOverTcp.Models.Fields
{
	public class TotMessageId : ByteArraySerializableBase
	{
		#region Statics

		public static TotMessageId Random => new TotMessageId((ushort)(new Random()).Next(ushort.MinValue, ushort.MaxValue));

		#endregion

		#region PropertiesAndMembers

		public ushort Id { get; private set; }

		#endregion

		#region Constructors

		public TotMessageId()
		{

		}

		public TotMessageId(ushort id)
		{
			Id = id;
		}

		public TotMessageId(byte[] bytes)
		{
			Guard.NotNull(nameof(bytes), bytes);
			Guard.InRangeAndNotNull($"{nameof(bytes)}.{nameof(bytes.Length)}", bytes.Length, 2, 2);
			Id = BitConverter.ToUInt16(bytes, 0);
		}

		#endregion

		#region Serialization

		public override void FromBytes(byte[] bytes)
		{
			Guard.NotNull(nameof(bytes), bytes);
			Guard.InRangeAndNotNull($"{nameof(bytes)}.{nameof(bytes.Length)}", bytes.Length, 2, 2);
			Id = BitConverter.ToUInt16(bytes, 0);
		}

		public override byte[] ToBytes() => BitConverter.GetBytes(Id);

		public override string ToString() => Id.ToString();

		#endregion
	}
}
