using System;
using UnityEngine;

namespace DunGen.Weighting
{
	public enum DepthScalingMode
	{
		None,
		Auto,
		MainPathDepth,
		BranchDepth
	}

	/// <summary>
	/// A complex weighted entry that can be used to provide more control over how weights are applied
	/// based on the tile's position within the generated layout.
	/// </summary>
	[Serializable]
	public class WeightedEntry<T>
	{
		/// <summary>
		/// The value of this entry
		/// </summary>
		public T Value;

		/// <summary>
		/// Base weight applied when this entry is on the main path
		/// </summary>
		public float MainPathWeight = 1.0f;

		/// <summary>
		/// Base weight applied when this entry is on a branch path
		/// </summary>
		public float BranchPathWeight = 1.0f;

		/// <summary>
		/// The scaling curve used to modify the base weight based on the tile's normalized depth (0-1)
		/// </summary>
		public AnimationCurve DepthWeightScale = AnimationCurve.Linear(0, 1, 1, 1);

		/// <summary>
		/// Determines how the depth scaling curve is applied
		/// </summary>
		public DepthScalingMode DepthScalingMode = DepthScalingMode.None;


		public WeightedEntry() { }
		public WeightedEntry(T value)
		{
			Value = value;
		}

		public float GetEffectiveWeight(bool isOnMainPath, float normalisedPathDepth, float normalisedBranchDepth)
		{
			float baseWeight = isOnMainPath ? MainPathWeight : BranchPathWeight;

			if(DepthScalingMode == DepthScalingMode.None)
				return baseWeight;

			float depth = 0f;

			switch (DepthScalingMode)
			{
				case DepthScalingMode.None:
					break;
				case DepthScalingMode.Auto:
					depth = isOnMainPath ? normalisedPathDepth : normalisedBranchDepth;
					break;
				case DepthScalingMode.MainPathDepth:
					depth = normalisedPathDepth;
					break;
				case DepthScalingMode.BranchDepth:
					depth = isOnMainPath ? 0f : normalisedBranchDepth;
					break;
				default:
					throw new ArgumentOutOfRangeException($"{typeof(DepthScalingMode).Name} {DepthScalingMode} is not yet implemented");
			}

			return baseWeight * DepthWeightScale.Evaluate(depth);
		}
	}
}