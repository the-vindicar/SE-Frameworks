using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;

namespace IngameScript
{
    static class BlockExtensions
    {
        public static bool IsAlive(this IMyTerminalBlock block) { return block.WorldMatrix != MatrixD.Identity; }
        public static Vector3D RealSize(this IMyCubeBlock block) { return (Vector3D)(block.Max - block.Min + Vector3I.One) * block.CubeGrid.GridSize; }
    }
}
