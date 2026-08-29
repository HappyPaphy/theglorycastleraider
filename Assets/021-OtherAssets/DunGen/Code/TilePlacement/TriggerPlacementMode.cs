using UnityEngine;

namespace DunGen.Placement
{
    public enum TriggerPlacementMode
	{
		[InspectorName("None")]
		None,
		[InspectorName("3D")]
		ThreeDimensional,
		[InspectorName("2D")]
		TwoDimensional,
	}
}
