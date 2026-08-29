using UnityEngine;

namespace DunGen.Common
{
    public enum AxisDirection
    {
        [InspectorName("+X")]
        PosX,
        [InspectorName("-X")]
        NegX,
        [InspectorName("+Y")]
        PosY,
        [InspectorName("-Y")]
        NegY,
        [InspectorName("+Z")]
        PosZ,
        [InspectorName("-Z")]
        NegZ,
    }
}
