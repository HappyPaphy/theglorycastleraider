using System;
using System.Collections.Generic;
using UnityEngine;

namespace DunGen.TilePlacement
{
	/// <summary>
	/// Provides tile template proxies for specified tile prefabs, ensuring that each prefab is associated with a single
	/// proxy instance.
	/// </summary>
	[Serializable, SubclassDisplay(displayName: "Default")]
	public class TileTemplateProvider : ITileTemplateProvider
	{
		protected readonly Dictionary<GameObject, TileProxy> templateCache = new Dictionary<GameObject, TileProxy>();


		public virtual TileProxy GetTileTemplate(GameObject tilePrefab)
		{
			// No proxy has been loaded yet, we should create one
			if (!templateCache.TryGetValue(tilePrefab, out var template))
			{
				template = new TileProxy(tilePrefab);
				templateCache.Add(tilePrefab, template);
			}

			return template;
		}
	}
}