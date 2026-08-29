using DunGen.Common;
using System;
using UnityEngine;

namespace DunGen
{
    public static class ExtensionHelpers
    {
        public static Vector3 ToVector3(this AxisDirection direction)
        {
            return direction switch
            {
                AxisDirection.PosX => new Vector3(+1, 0, 0),
                AxisDirection.NegX => new Vector3(-1, 0, 0),
                AxisDirection.PosY => new Vector3(0, +1, 0),
                AxisDirection.NegY => new Vector3(0, -1, 0),
                AxisDirection.PosZ => new Vector3(0, 0, +1),
                AxisDirection.NegZ => new Vector3(0, 0, -1),
                _ => throw new NotImplementedException($"AxisDirection '{direction}' not implemented"),
            };
        }
    }
}