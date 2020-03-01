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
    public enum Gravity { None, Natural, Artificial, Total }

    public interface IRotationAid
    {
        double Rotate(TimeSpan interval, Vector3D desired_forward, Vector3D desired_up, Vector3D forward, Vector3D up, ref Vector3D velocity);
    }

    public abstract class BasePilot
    {
        /// <summary>
        /// Current task, or null if there is none.
        /// </summary>
        public BasePilotingStrategy CurrentTask { get; set; }
        /// <summary>
        /// Rotation aid.
        /// </summary>
        public IRotationAid RotationAid { get; set; }
        /// <summary>
        /// Ship controller (cockpit, flight station or remote controller) used by the autopilot. 
        /// The ship MUST have at least one installed.
        /// </summary>
        public IMyShipController Controller { get; protected set; }
        /// <summary>
        /// Time elapsed since last update.
        /// </summary>
        public TimeSpan elapsedTime { get; protected set; }
        /// <summary>
        /// Ship velocities just before last task update.
        /// </summary>
        public MyShipVelocities Velocities { get; protected set; }
        /// <summary>
        /// Ship mass before last task update;
        /// </summary>
        public MyShipMass Mass { get; protected set; }
        /// <summary>
        /// This function can be used for debugging, but it's up to user to provide actual log output, like Echo() or a screen.
        /// </summary>
        public Action<string> Log = null;

        public abstract double GetMaxAccelerationFor(Vector3D direction);
        public abstract double GetThrustChangeDelay(Vector3D from, Vector3D to);
        public abstract bool Update(TimeSpan elapsed);
    }
}
