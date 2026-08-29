using UnityEngine;

namespace DunGen.Culling.Shared
{
	public class Portal
	{
		/// <summary>
		/// The room this portal belongs to
		/// </summary>
		public Room From;

		/// <summary>
		/// The adjacent room this portal leads to
		/// </summary>
		public Room To;

		/// <summary>
		/// The door component that this portal is based on
		/// </summary>
		public Door DoorComponent;

		/// <summary>
		/// Transform at the base of the portal
		/// </summary>
		public Transform Transform;

		/// <summary>
		/// The size of the portal opening
		/// </summary>
		public Vector2 Size;

		/// <summary>
		/// Door renderers that are used to render the portal
		/// </summary>
		public Renderer[] Renderers { get; private set; }

		private Vector3[] quadCache;


		public Vector3[] GetWorldQuad()
		{
			if (quadCache == null || quadCache.Length != 4)
				quadCache = new Vector3[4];

			var right = Transform.right;
			var up = Transform.up;
			var basePosition = Transform.position;

			var halfWidth = Size.x * 0.5f;
			var height = Size.y;

			// TL, TR, BR, BL (clockwise as viewed from the "from" side)
			quadCache[0] = basePosition + (-right * halfWidth + up * height);
			quadCache[1] = basePosition + ( right * halfWidth + up * height);
			quadCache[2] = basePosition + ( right * halfWidth);
			quadCache[3] = basePosition + (-right * halfWidth);

			return quadCache;
		}

		public void RefreshRenderers()
		{
			if (DoorComponent == null)
				Renderers = new Renderer[0];
			else
				Renderers = DoorComponent.GetComponentsInChildren<Renderer>(true);
		}

		public Plane GetPortalPlane() => new Plane(Transform.forward, Transform.position); // Normal from 'from' to 'to'
	}
}
