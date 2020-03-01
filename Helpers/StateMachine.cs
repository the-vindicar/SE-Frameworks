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
    class StateMachine
    {
        public delegate IEnumerable<string> State();
        public event Action<string> StateChanged;
        public IDictionary<string, State> States = new Dictionary<string, State>();
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
