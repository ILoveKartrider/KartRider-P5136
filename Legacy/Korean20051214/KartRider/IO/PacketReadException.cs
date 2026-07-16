using System;

namespace KartRider.IO;

internal sealed class PacketReadException : Exception
{
	public PacketReadException(string message)
		: base(message)
	{
	}
}
