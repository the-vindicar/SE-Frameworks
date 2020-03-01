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
        /// Points the ship in the direction of the goal and flies straight there.
        /// It won't slow down before the target, and it will ignore potential collisions.
        /// </summary>
        public class RammingStrategy : BasePilotingStrategy
        {
            public RammingStrategy(Location goal, IMyCubeBlock reference,
                Base6Directions.Direction forward = Base6Directions.Direction.Forward,
                Base6Directions.Direction up = Base6Directions.Direction.Up) : base(goal, reference, forward, up) { }
            public override bool Update(BasePilot owner, ref Vector3D linearV, ref Vector3D angularV)
            {
                IMyCubeBlock reference = Reference ?? owner.Controller;
                MatrixD wm = reference.WorldMatrix;
                Goal.Update(owner.elapsedTime);
                Vector3D direction = Goal.Position - wm.Translation;
                double distance = direction.Normalize();
                //linear velocity
                linearV = direction * MaxLinearSpeed + Goal.Velocity;
                //angular velocity
                double diff = owner.RotationAid.Rotate(owner.elapsedTime,
                    direction, Vector3D.Zero,
                    wm.GetDirectionVector(ReferenceForward),
                    wm.GetDirectionVector(ReferenceUp),
                    ref angularV);
                if (diff < OrientationEpsilon)
                    angularV = Vector3D.Zero;
                return (diff < OrientationEpsilon) && (distance < PositionEpsilon);
            }
        }
}
