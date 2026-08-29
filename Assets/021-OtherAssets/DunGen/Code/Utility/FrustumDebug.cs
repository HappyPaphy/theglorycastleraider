using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DunGen
{
	public static class FrustumDebug
	{
		// Reused buffers to avoid GC
		private static readonly List<Vector3> _poly = new List<Vector3>(16);
		private static readonly List<Vector3> _scratch = new List<Vector3>(16);
		private static readonly List<Vector3> _out = new List<Vector3>(16);


		/// <summary>Draw edges (and optionally filled faces) of a convex frustum.</summary>
		/// <param name="planes">Frustum planes, inward-facing normals (inside is distance >= 0).</param>
		/// <param name="edgeColour">Colour for wire edges.</param>
		/// <param name="duration">Debug.DrawLine duration in seconds (0 = one frame).</param>
		/// <param name="faceColour">If a.hasValue, faces will be drawn semi-transparent in Scene view (Editor only).</param>
		/// <param name="extentOnFace">Half-size of the initial face quad (before clipping). Just needs to be "big".</param>
		/// <param name="epsilon">Geometric tolerance.</param>
		public static void Draw(IReadOnlyList<Plane> planes, Color edgeColour, float duration = 0f,
								Color? faceColour = null, float extentOnFace = 5000f, float epsilon = 1e-4f)
		{
			if (planes == null || planes.Count == 0)
				return;

			// For each plane, compute its clipped face polygon
			for (int i = 0; i < planes.Count; i++)
			{
				var face = BuildFacePolygon(i, planes, extentOnFace, epsilon);

				if (face == null || face.Count < 3)
					continue;

				// Wireframe edges
				for (int v = 0; v < face.Count; v++)
				{
					var a = face[v];
					var b = face[(v + 1) % face.Count];

					Debug.DrawLine(a, b, edgeColour, duration, depthTest: false);
				}

#if UNITY_EDITOR
				if (faceColour.HasValue)
				{
					// Filled face in the Scene view (Editor only)
					var col = faceColour.Value;
					Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

					using (new Handles.DrawingScope(col))
					{
						// Draw as a triangle fan from the polygon centroid
						var centroid = Vector3.zero;

						for (int k = 0; k < face.Count; k++)
							centroid += face[k];

						centroid /= face.Count;

						for (int k = 1; k < face.Count; k++)
							Handles.DrawAAConvexPolygon(centroid, face[k - 1], face[k]);

						Handles.DrawAAConvexPolygon(centroid, face[face.Count - 1], face[0]);
					}
				}
#endif
			}
		}

		// Build the polygon for one face by clipping a large quad on that plane against all other planes
		private static List<Vector3> BuildFacePolygon(int planeIndex, IReadOnlyList<Plane> planes, float extent, float eps)
		{
			var p = planes[planeIndex];

			// Big quad on the plane
			Vector3 c = PointOnPlane(p);
			OrthonormalBasis(p.normal, out Vector3 u, out Vector3 v);

			var poly = _poly; poly.Clear();
			poly.Add(c + (-u - v) * extent);
			poly.Add(c + (u - v) * extent);
			poly.Add(c + (u + v) * extent);
			poly.Add(c + (-u + v) * extent);

			// Clip against all other planes
			for (int i = 0; i < planes.Count; i++)
			{
				if (i == planeIndex)
					continue;

				ClipPolygonAgainstPlane(poly, planes[i], eps, _scratch);

				if (poly.Count < 3)
					break; // fully clipped away
			}

			// Return a copy (so subsequent clips don't mutate the caller's list)
			if (poly.Count >= 3)
			{
				_out.Clear();
				_out.AddRange(poly);

				return _out;
			}

			return null;
		}

		// Sutherland–Hodgman half-space clipping (in 3D)
		private static void ClipPolygonAgainstPlane(List<Vector3> inPoly, Plane clip, float eps, List<Vector3> scratch)
		{
			scratch.Clear();

			if (inPoly.Count == 0)
			{
				inPoly.Clear();
				return;
			}

			Vector3 prev = inPoly[inPoly.Count - 1];
			float dPrev = clip.GetDistanceToPoint(prev);
			bool prevInside = dPrev >= -eps;

			for (int i = 0; i < inPoly.Count; i++)
			{
				Vector3 cur = inPoly[i];
				float dCur = clip.GetDistanceToPoint(cur);
				bool curInside = dCur >= -eps;

				if (curInside)
				{
					if (!prevInside)
						scratch.Add(Intersect(prev, cur, dPrev, dCur));

					scratch.Add(cur);
				}
				else if (prevInside)
					scratch.Add(Intersect(prev, cur, dPrev, dCur));

				prev = cur;
				dPrev = dCur;
				prevInside = curInside;
			}

			// swap scratch -> inPoly
			inPoly.Clear();
			inPoly.AddRange(scratch);
		}

		private static Vector3 Intersect(in Vector3 a, in Vector3 b, float da, float db)
		{
			// da and db are signed distances to the clipping plane
			// t in [0,1] where segment crosses plane: a + t*(b-a)
			float t = da / (da - db);
			return a + (b - a) * t;
		}

		private static Vector3 PointOnPlane(Plane p) => -p.normal * p.distance;

		private static void OrthonormalBasis(in Vector3 n, out Vector3 u, out Vector3 v)
		{
			// Choose a helper vector least parallel to n
			Vector3 t = Mathf.Abs(n.y) < 0.99f ? Vector3.up : Vector3.right;
			u = Vector3.Normalize(Vector3.Cross(t, n));
			v = Vector3.Normalize(Vector3.Cross(n, u));
		}
	}
}