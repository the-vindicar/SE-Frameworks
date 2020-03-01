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
        /// Moves the ship in a straight line to the specified goal.
        /// Ship orientation is ignored.
        /// No collision avoidance is done.
        /// </summary>
        public class UnaimedFlightStrategy : BasePilotingStrategy
        {
            /// <summary>
            /// How close to the maximum safe speed are we allowed to get.
            /// </summary>
            public double VelocityUsage = 0.9;
            /// <summary>
            /// Constructs the strategy with given goal and (optional) reference block.
            /// </summary>
            /// <param name="goal">Goal to pursue.</param>
            /// <param name="reference">Reference block to use, or null to use ship controller.</param>
            public UnaimedFlightStrategy(Location goal, IMyCubeBlock reference) : base(goal, reference) { }
            /// <summary>
            /// Queries the strategy on which linear and angular velocities the ship should have.
            /// </summary>
            /// <param name="owner">AutoPilot instance that queries the strategy.</param>
            /// <param name="linearV">Initial value - current linear velocity. Is set to desired linear velocity.</param>
            /// <param name="angularV">Initial value - current rotation. Is set to desired rotation.</param>
            /// <returns>True if goal is considered achieved.</returns>
            public override bool Update(BasePilot owner, ref Vector3D linearV, ref Vector3D angularV)
            {
                IMyCubeBlock reference = Reference ?? owner.Controller;
                MatrixD wm = reference.WorldMatrix;
                Goal.Update(owner.elapsedTime);
                Vector3D direction = Goal.Position - wm.Translation;
                double distance = direction.Normalize();
                if (distance < Goal.Distance)
                {
                    direction *= -1;
                    distance = Goal.Distance - distance;
                }
                if (distance > PositionEpsilon)
                {
                    //linear velocity
                    double accel = owner.GetMaxAccelerationFor(-direction);
                    Vector3D targetv = direction * MaxSpeedFor(accel, distance);
                    linearV = targetv + Goal.Velocity;
                }
                else
                    linearV = Vector3D.Zero;
                angularV = Vector3D.Zero;
                return Vector3D.IsZero(linearV);
            }
        }
}
