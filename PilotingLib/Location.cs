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
    /// <summary>
    /// Describes a location in space: a static point or a moving entity.
    /// </summary>
    public struct Location
    {
        /// <summary>Unique ID of the associated entity, or 0 if no entity is associated.</summary>
        public long EntityID;
        /// <summary>Distance to maintain relative to the entity's centerpoint.</summary>
        public double Distance;
        /// <summary>Position of the entity/point in world-space.</summary>
        public Vector3D Translation;
        /// <summary>Velocity of the entity in world-space, or a zero vector for a static point.</summary>
        public Vector3D Velocity;
        /// <summary>Estimated current position - assuming the entity kept its last observed velocity.</summary>
        public Vector3D Position { get { return Translation + Velocity * Elapsed.TotalSeconds; } }
        /// <summary>Orientation of the entity relative to the worldspace.</summary>
        public Quaternion Orientation;
        /// <summary>How much time has passed since last update of this information.</summary>
        public TimeSpan Elapsed;
        /// <summary>Visible name of the location.</summary>
        public string Name;
        /// <summary>Creates an instance for a static location in space.</summary>
        /// <param name="position">Position in world-space.</param>
        /// <param name="name">Name of the location.</param>
        public Location(Vector3D position, string name = "") : this()
        {
            Orientation = Quaternion.Identity;
            Translation = position;
            Name = name;
        }
        /// <summary>Creates an instance for a detected entity.</summary>
        /// <param name="entity">Entity information.</param>
        public Location(MyDetectedEntityInfo entity) : this()
        {
            EntityID = entity.EntityId;
            Update(entity);
        }
        /// <summary>Creates an instance for a waypoint.</summary>
        /// <param name="waypoint">Waypoint to transform into an instance.</param>
        public Location(MyWaypointInfo waypoint) : this(waypoint.Coords, waypoint.Name) { }
        /// <summary>
        /// Creates a location instance from string. Following formats are supported:
        /// <para>- Location.ToString() serialization</para>
        /// <para>- GPS strings</para>
        /// <para>- Vector3D.ToString() serialization</para>
        /// <para>Throws <see cref="ArgumentException"/> if string fails to match any of the above.</para>
        /// </summary>
        /// <param name="source">String to parse.</param>
        /// <returns>Location instance described by the string.</returns>
        public static Location FromString(string source)
        {
            Location result;
            MyWaypointInfo wp;
            Vector3D v;
            if (source.StartsWith("LOCATION/") && TryParse(source, out result))
                return result;
            if (source.StartsWith("GPS:") && MyWaypointInfo.TryParse(source, out wp))
                return new Location(wp);
            if (Vector3D.TryParse(source, out v))
                return new Location(v);
            throw new ArgumentException("Not a valid string format: " + source, "source");
        }

        public static implicit operator Location(Vector3D position) { return new Location(position); }
        public static implicit operator Location(MyDetectedEntityInfo entity) { return new Location(entity); }
        public static implicit operator Location(MyWaypointInfo wp) { return new Location(wp); }

        /// <summary>
        /// Transforms a point in local coordinates of the target into world coordinates.
        /// </summary>
        /// <param name="v">A point to transform.</param>
        /// <returns>Coordinates of the point in world-space.</returns>
        public Vector3D LocalToGlobal(Vector3D v) { return Position + Vector3D.Transform(v, Orientation); }
        /// <summary>
        /// Increments interval elapsed since location data was acquired.
        /// </summary>
        /// <param name="interval">Increment value</param>
        public void Update(TimeSpan interval) { Elapsed += interval; }
        /// <summary>
        /// Scans through a collection of detected entities and checks if we can pull updated information on ours.
        /// </summary>
        /// <param name="entities">Entities to be matched.</param>
        /// <returns>True if information was updated successfully.</returns>
        public bool Update(IEnumerable<MyDetectedEntityInfo> entities)
        {
            foreach (var i in entities)
                if (i.EntityId == EntityID)
                    return Update(i);
            return false;
        }
        /// <summary>
        /// Checks detected entity and sees if it's the same entity as ours. If yes, pulls updated information.
        /// </summary>
        /// <param name="entity">Entity to check.</param>
        /// <returns>True if information was updated successfully.</returns>
        public bool Update(MyDetectedEntityInfo entity)
        {
            if (entity.EntityId != EntityID) return false;
            Name = entity.Name;
            Translation = entity.Position;
            Velocity = entity.Velocity;
            Orientation = Quaternion.CreateFromRotationMatrix(entity.Orientation);
            Elapsed = new TimeSpan();
            return true;
        }
        /// <summary>Serializes Location instance into a string.</summary>
        /// <returns>Serialized string. Single line, uses '/' as separator.</returns>
        public override string ToString()
        {
            StringBuilder buf = new StringBuilder("LOCATION/");
            buf.Append(EntityID).Append('/');
            buf.Append(Distance).Append('/');
            buf.Append(Translation).Append('/');
            buf.Append(Velocity).Append('/');
            buf.Append(Orientation.X).Append('/');
            buf.Append(Orientation.Y).Append('/');
            buf.Append(Orientation.Z).Append('/');
            buf.Append(Orientation.W).Append('/');
            buf.Append(Elapsed.TotalSeconds).Append('/');
            buf.Append(Name);
            return buf.ToString();
        }
        /// <summary>Attempts to de-serialize a string into a Location instance.</summary>
        /// <param name="s">String to parse.</param>
        /// <param name="loc">Location instance.</param>
        /// <returns>True if parsing succeeds.</returns>
        public static bool TryParse(string s, out Location loc)
        {
            loc = new Location();
            string[] parts = s.Split(new char[] { '/' }, 11);
            double seconds = 0;
            bool good = (parts.Length == 11) && (parts[0] == "LOCATION")
                && long.TryParse(parts[1], out loc.EntityID)
                && double.TryParse(parts[2], out loc.Distance)
                && Vector3D.TryParse(parts[3], out loc.Translation)
                && Vector3D.TryParse(parts[4], out loc.Velocity)
                && float.TryParse(parts[5], out loc.Orientation.X)
                && float.TryParse(parts[6], out loc.Orientation.Y)
                && float.TryParse(parts[7], out loc.Orientation.Z)
                && float.TryParse(parts[8], out loc.Orientation.W)
                && double.TryParse(parts[9], out seconds);
            if (good)
            {
                loc.Name = parts[10];
                loc.Elapsed = TimeSpan.FromSeconds(seconds);
            }
            return good;
        }
    }
}
