using System;
using System.Collections.Concurrent;

namespace DunGen.Pooling
{
	public static class CollectionPool
	{
		#region List

		public static class List<T>
		{
			private static readonly ConcurrentBag<System.Collections.Generic.List<T>> pool = new ConcurrentBag<System.Collections.Generic.List<T>>();

			public static System.Collections.Generic.List<T> Get()
			{
				return pool.TryTake(out var list) ? list : new System.Collections.Generic.List<T>();
			}

			public static void Return(System.Collections.Generic.List<T> list)
			{
				if (list == null)
					return;

				list.Clear();
				pool.Add(list);
			}

			public static PooledObject<System.Collections.Generic.List<T>> Get(out System.Collections.Generic.List<T> tempList)
			{
				tempList = Get();
				return new PooledObject<System.Collections.Generic.List<T>>(tempList, Return);
			}
		}

		#endregion

		#region HashSet

		public static class HashSet<T>
		{
			private static readonly ConcurrentBag<System.Collections.Generic.HashSet<T>> pool = new ConcurrentBag<System.Collections.Generic.HashSet<T>>();

			public static System.Collections.Generic.HashSet<T> Get()
			{
				return pool.TryTake(out var set) ? set : new System.Collections.Generic.HashSet<T>();
			}

			public static void Return(System.Collections.Generic.HashSet<T> hashSet)
			{
				if (hashSet == null)
					return;

				hashSet.Clear();
				pool.Add(hashSet);
			}

			public static PooledObject<System.Collections.Generic.HashSet<T>> Get(out System.Collections.Generic.HashSet<T> tempHashSet)
			{
				tempHashSet = Get();
				return new PooledObject<System.Collections.Generic.HashSet<T>>(tempHashSet, Return);
			}
		}

		#endregion

		#region Queue

		public static class Queue<T>
		{
			private static readonly ConcurrentBag<System.Collections.Generic.Queue<T>> pool = new ConcurrentBag<System.Collections.Generic.Queue<T>>();

			public static System.Collections.Generic.Queue<T> Get() => pool.TryTake(out var q) ? q : new System.Collections.Generic.Queue<T>();

			public static void Return(System.Collections.Generic.Queue<T> queue)
			{
				if (queue == null)
					return;

				queue.Clear();
				pool.Add(queue);
			}

			public static PooledObject<System.Collections.Generic.Queue<T>> Get(out System.Collections.Generic.Queue<T> tempQueue)
			{
				tempQueue = Get();
				return new PooledObject<System.Collections.Generic.Queue<T>>(tempQueue, Return);
			}
		}

		#endregion

		public readonly struct PooledObject<TCollection> : IDisposable
		{
			public TCollection Collection { get; }
			private readonly Action<TCollection> returnAction;

			public PooledObject(TCollection item, Action<TCollection> returnAction)
			{
				Collection = item;
				this.returnAction = returnAction;
			}

			public void Dispose()
			{
				returnAction?.Invoke(Collection);
			}
		}
	}
}