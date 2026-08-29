using System;
using UnityEngine;

namespace DunGen.Pooling
{
	/// <summary>
	/// Default implementation of <see cref="ITileProxyPool"/>
	/// </summary>
	[Serializable, SubclassDisplay(displayName: "Default")]
	public class TileProxyPool : ITileProxyPool
	{
		protected readonly BucketedObjectPool<TileProxy, TileProxy> pool;


		public TileProxyPool()
		{
			pool = new BucketedObjectPool<TileProxy, TileProxy>(
				objectFactory: template => new TileProxy(template),
				takeAction: x =>
				{
					x.IsRequired = false;
					x.Placement.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
				}
				);
		}

		public virtual TileProxy GetTileProxy(TileProxy tileTemplate) => pool.TakeObject(tileTemplate);

		public virtual void ReturnTileProxy(TileProxy tileProxy) => pool.ReturnObject(tileProxy);
	}
}