using System.Threading;

namespace KartRider.Common.Utilities;

internal sealed class LockFreeQueue<T> where T : class
{
	private class SingleLinkNode
	{
		public SingleLinkNode Next;

		public T Item;
	}

	private SingleLinkNode mHead;

	private SingleLinkNode mTail;

	public T Next
	{
		get
		{
			if (mHead.Next != null)
			{
				return mHead.Next.Item;
			}
			return null;
		}
	}

	public LockFreeQueue()
	{
		mHead = new SingleLinkNode();
		mTail = mHead;
	}

	private static bool CompareAndExchange(ref SingleLinkNode pLocation, SingleLinkNode pComparand, SingleLinkNode pNewValue)
	{
		return pComparand == Interlocked.CompareExchange(ref pLocation, pNewValue, pComparand);
	}

	public bool Dequeue(out T pItem)
	{
		pItem = null;
		SingleLinkNode singleLinkNode = null;
		bool flag = false;
		while (!flag)
		{
			singleLinkNode = mHead;
			SingleLinkNode singleLinkNode2 = mTail;
			SingleLinkNode next = singleLinkNode.Next;
			if (singleLinkNode != mHead)
			{
				continue;
			}
			if (singleLinkNode != singleLinkNode2)
			{
				pItem = next.Item;
				flag = CompareAndExchange(ref mHead, singleLinkNode, next);
				continue;
			}
			if (next != null)
			{
				CompareAndExchange(ref mTail, singleLinkNode2, next);
				continue;
			}
			return false;
		}
		return true;
	}

	public T Dequeue()
	{
		Dequeue(out var pItem);
		return pItem;
	}

	public void Enqueue(T pItem)
	{
		SingleLinkNode singleLinkNode = null;
		SingleLinkNode pNewValue = new SingleLinkNode
		{
			Item = pItem
		};
		bool flag = false;
		while (!flag)
		{
			singleLinkNode = mTail;
			SingleLinkNode next = singleLinkNode.Next;
			if (mTail == singleLinkNode)
			{
				if (next != null)
				{
					CompareAndExchange(ref mTail, singleLinkNode, next);
				}
				else
				{
					flag = CompareAndExchange(ref mTail.Next, null, pNewValue);
				}
			}
		}
		CompareAndExchange(ref mTail, singleLinkNode, pNewValue);
	}
}
