namespace KartRider.Common.Utilities;

internal sealed class ByteArraySegment
{
	private byte[] mBuffer;

	private int mStart;

	private int mLength;

	private bool mEncrypted = true;

	public byte[] Buffer
	{
		get
		{
			return mBuffer;
		}
		set
		{
			mBuffer = value;
		}
	}

	public bool Encrypted => mEncrypted;

	public int Length => mLength;

	public int Start => mStart;

	public ByteArraySegment(byte[] pBuffer, bool pEncrypted)
	{
		mBuffer = pBuffer;
		mLength = mBuffer.Length;
		mEncrypted = pEncrypted;
	}

	public ByteArraySegment(byte[] pBuffer, int pStart, int pLength)
	{
		mBuffer = pBuffer;
		mStart = pStart;
		mLength = pLength;
	}

	public bool Advance(int pLength)
	{
		mStart += pLength;
		mLength -= pLength;
		return mLength <= 0;
	}
}
