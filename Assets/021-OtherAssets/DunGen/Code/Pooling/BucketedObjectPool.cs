using System;
using System.Collections.Generic;
using UnityEngine;

namespace DunGen.Pooling
{
	/// <summary>
	/// A simple object pool that groups objects into buckets based on a key
	/// </summary>
	/// <typeparam name="TKey">The key type that determines how objects are bucketed</typeparam>
	/// <typeparam name="TObject">The type of pooled object</typeparam>
	public sealed class BucketedObjectPool<TKey, TObject> where TObject : class
	{
		private readonly Dictionary<TKey, List<TObject>> buckets = new Dictionary<TKey, List<TObject>>();
		private readonly Func<TKey, TObject> objectFactory;
		private readonly Action<TObject> takeAction;
		private readonly Action<TObject> returnAction;
		private readonly int initialCapacity;
		private readonly Dictionary<TObject, TKey> owningBuckets = new Dictionary<TObject, TKey>();


		public BucketedObjectPool(Func<TKey, TObject> objectFactory,
			Action<TObject> takeAction = null,
			Action<TObject> returnAction = null,
			int initialCapacity = 0)
		{
			this.objectFactory = objectFactory;
			this.takeAction = takeAction;
			this.returnAction = returnAction;
			this.initialCapacity = initialCapacity;
		}

		/// <summary>
		/// Clears out all the existing objects in the pool
		/// </summary>
		public void Clear()
		{
			buckets.Clear();
			owningBuckets.Clear();
		}

		/// <summary>
		/// Takes an object from the pool, creating a new one if the pool is empty
		/// </summary>
		/// <param name="key">The bucket to take from</param>
		/// <returns>The object returned from the pool</returns>
		public TObject TakeObject(TKey key)
		{
			TryTakeObject(key, out var obj);
			return obj;
		}

		/// <summary>
		/// Takes an object from the pool, creating a new one if the pool is empty
		/// </summary>
		/// <param name="key">The bucket to take from</param>
		/// <param name="obj">Object returned from the pool</param>
		/// <returns>Did we take an existing object from the pool? False if a new instance had to be created</returns>
		public bool TryTakeObject(TKey key, out TObject obj)
		{
			if(key == null)
				throw new ArgumentNullException(nameof(key));

			// Create the bucket if it doesn't exist yet
			if (!buckets.TryGetValue(key, out var bucket))
				bucket = InitialiseBucket(key);

			// Take an object from the bucket if there is one..
			if (bucket.Count > 0)
			{
				obj = bucket[bucket.Count - 1];
				bucket.RemoveAt(bucket.Count - 1);

				takeAction?.Invoke(obj);
				return true;
			}
			// ..otherwise create a new object
			else
			{
				var newObject = objectFactory(key);
				owningBuckets[newObject] = key;

				obj = newObject;
				return false;
			}
		}

		/// <summary>
		/// Returns an object to the pool
		/// </summary>
		/// <param name="obj">The object to return</param>
		/// <returns>True if successful</returns>
		public bool ReturnObject(TObject obj)
		{
			if (obj == null)
				return false;

			// Find which bucket owns this object
			if (!owningBuckets.TryGetValue(obj, out var key))
				return false;

			returnAction?.Invoke(obj);
			buckets[key].Add(obj);

			return true;
		}

		/// <summary>
		/// Inserts an object into the pool. This is useful for pre-warming the pool with existing objects
		/// </summary>
		/// <param name="key">The bucket to insert into</param>
		/// <param name="obj">The object to insert</param>
		/// <returns>True if successful</returns>
		public bool InsertObject(TKey key, TObject obj)
		{
			if(key == null || obj == null)
				return false;

			if (owningBuckets.TryGetValue(obj, out var existingKey))
			{
				Debug.LogError("Tried to 'Insert' an object into the pool that already belongs to it, use ReturnObject() instead");
				return false;
			}

			// Create the bucket if it doesn't exist yet
			if (!buckets.TryGetValue(key, out var bucket))
				bucket = InitialiseBucket(key);

			returnAction?.Invoke(obj);
			buckets[key].Add(obj);
			owningBuckets[obj] = key;

			return true;
		}

		private List<TObject> InitialiseBucket(TKey key)
		{
			var bucket = new List<TObject>(initialCapacity);
			buckets[key] = bucket;

			for (int i = 0; i < initialCapacity; i++)
			{
				var newObject = objectFactory(key);
				owningBuckets[newObject] = key;
			}

			return bucket;
		}

		public void DumpPoolInfo(Func<TKey, string> getBucketName = null)
		{
			foreach (var pair in buckets)
			{
				string bucketName = getBucketName?.Invoke(pair.Key) ?? pair.Key.ToString();
				Debug.Log($"Bucket: {bucketName}, Count: {pair.Value.Count}");
			}
		}
	}
}
