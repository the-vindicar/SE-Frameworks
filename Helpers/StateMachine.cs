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
    /// Coroutine-based state machine. 
    /// Each state must be a method with the following signature:
    /// <para>IEnumerable&lt;string&gt; State()</para>
    /// <para>On each iteration, state method should do one of the following:</para>
    /// <para>a) yield return null in order to pause execution while keeping the state,</para>
    /// <para>b) yield return the name of the next state to change the state,</para>
    /// <para>c) yield break to stop the state machine (final state).</para>
    /// </summary>
    class StateMachine
    {
        public delegate IEnumerable<string> State();
        /// <summary>Event is triggered every time the state changes.</summary>
        public event Action<string> StateChanged;
        /// <summary>
        /// Collection of available states. 
        /// Keys are state names that should be yield'ed by the state coroutines.
        /// </summary>
        public IDictionary<string, State> States = new Dictionary<string, State>();
        /// <summary>
        /// Controls current state of the state machine. On assignment, state changes immediately.
        /// <para>Warning: assigning the same value will cause current state coroutine to restart.</para>
        /// </summary>
        public string CurrentState
        {
            get { return stateName; }
            set
            {
                stateRunner?.Dispose();
                stateName = string.IsNullOrEmpty(value) ? null : value;
                stateRunner = (stateName == null) ? null : States[stateName]().GetEnumerator();
                StateChanged?.Invoke(stateName);
            }
        }
        /// <summary>
        /// Allows current state coroutine to do a portion of work and switch over if needed.
        /// <para>Returns true if machine continues operation, false if it has reached the final state.</para>
        /// </summary>
        /// <returns></returns>
        public bool Update()
        {
            if (stateRunner == null)
                return false;
            else if (!stateRunner.MoveNext())
            {
                CurrentState = null;
                return false;
            }
            else if (!string.IsNullOrEmpty(stateRunner.Current))
                CurrentState = stateRunner.Current;
            return true;
        }
        string stateName = string.Empty;
        IEnumerator<string> stateRunner = null;
    }
}
