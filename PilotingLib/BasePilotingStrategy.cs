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
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// Provides basic facilities for piloting s strategies.
    /// </summary>
    public abstract class BasePilotingStrategy
    {
        /// <summary>
        /// Reference block. Its position & orientation are considered to be ship's position & orientation.
        /// If null, then ship controller block is used for that purpose.
        /// </summary>
        public IMyCubeBlock Reference;
        public Base6Directions.Direction ReferenceForward;
        public Base6Directions.Direction ReferenceUp;
        /// <summary>
        /// Current goal: point in world-space to reach, or entity to pursue.
        /// </summary>
        public Location Goal { get; set; }
        /// <summary>
        /// Maximum speed (relative to the goal) that ship is allowed to reach.
        /// </summary>
        public double MaxLinearSpeed = 100.0;
        /// <summary>
        /// How close to the maximum safe speed are we allowed to get.
        /// </summary>
        public double VelocityUsage = 0.9;
        public double OrientationEpsilon = 1e-4;
        public double PositionEpsilon = 1e-1;
        /// <summary>
        /// Should inertial dampeners be left on for this task.
        /// </summary>
        public bool DampenersOverride = false;
        /// <summary>
        /// Constructs the strategy with given goal and (optional) reference block.
        /// </summary>
        /// <param name="goal">Goal to pursue.</param>
        /// <param name="reference">Reference block to use.</param>
        public BasePilotingStrategy(Location goal, IMyCubeBlock reference,
            Base6Directions.Direction forward = Base6Directions.Direction.Forward,
            Base6Directions.Direction up = Base6Directions.Direction.Up)
        {
            if (!Base6Directions.IsValidBlockOrientation(forward, up))
                throw new ArgumentException("Invalid set of directions!");
            Goal = goal;
            Reference = reference;
            ReferenceForward = forward;
            ReferenceUp = up;
        }
        /// <summary>
        /// Queries the strategy on which linear and angular velocities the ship should have.
        /// </summary>
        /// <param name="owner">AutoPilot instance that queries the strategy.</param>
        /// <param name="linearV">Initial value - current linear velocity. Set it to desired linear velocity.</param>
        /// <param name="angularV">Initial value - current rotation. Set it to desired rotation.</param>
        /// <returns>True if goal is considered achieved.</returns>
        public abstract bool Update(BasePilot owner, ref Vector3D linearV, ref Vector3D angularV);

        protected double MaxSpeedFor(double accel, double distance)
        {
            double braking_time = Math.Sqrt(2 * distance / accel);
            double acceptable_speed = Math.Min(accel * braking_time * VelocityUsage, MaxLinearSpeed);
            return Math.Min(acceptable_speed, distance);//slow down when close
        }
    }
}
