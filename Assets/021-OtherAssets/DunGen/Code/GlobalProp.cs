using UnityEngine;

namespace DunGen
{
	[AddComponentMenu("DunGen/Random Props/Global Prop")]
	[HelpURL("https://dungen-docs.aegongames.com/advanced-features/props-variety/#global-props")]
	public class GlobalProp : MonoBehaviour
	{
		public int PropGroupID = 0;
		public float MainPathWeight = 1;
		public float BranchPathWeight = 1;
		public AnimationCurve DepthWeightScale = AnimationCurve.Linear(0, 1, 1, 1);
	}
}
