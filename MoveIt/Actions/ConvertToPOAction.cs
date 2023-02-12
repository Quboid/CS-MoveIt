using ColossalFramework.UI;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace MoveIt
{
    class ConvertToPOAction : Action
    {
        private HashSet<InstanceState> m_states = new HashSet<InstanceState>();
        private HashSet<Instance> m_clones = new HashSet<Instance>();
        private HashSet<Instance> m_oldSelection;
        private bool firstRun = true; // TODO this disables Redo

        public ConvertToPOAction() : base()
        {
            foreach (Instance instance in selection)
            {
                if ((instance is MoveableBuilding || instance is MoveableProp) && instance.isValid)
                {
                    m_states.Add(instance.SaveToState(false));
                }
            }
        }

        public override void Do()
        {
            if (!firstRun) return; // Disables Redo
            m_clones.Clear();
            m_oldSelection = new HashSet<Instance>();

            foreach (InstanceState instanceState in m_states)
            {
                Instance instance = instanceState.instance;

                lock (instance.data)
                {
                    if (!instance.isValid)
                    {
                        continue;
                    }

                    if (!(instance is MoveableBuilding || instance is MoveableProp))
                    {
                        m_oldSelection.Add(instance);
                        continue;
                    }

                    PO_Object obj = null;
                    try
                    {
                        obj = MoveItTool.PO.ConvertToPO(instance);
                        if (obj == null)
                        {
                            continue;
                        }

                        MoveItTool.PO.visibleObjects.Add(obj.Id, obj);
                    }
                    catch (ArgumentException e)
                    {
                        string s = "";
                        foreach (var kv in MoveItTool.PO.visibleObjects)
                        {
                            s += $"{kv.Key}, ";
                        }
                        Log.Debug($"ArgumentException:\n{e}\n\n{(obj == null ? "<null>" : obj.Id.ToString())}\n\n{MoveItTool.PO.visibleObjects.Count}: {s}", "[M27]");
                    }

                    InstanceID instanceID = default;
                    instanceID.NetLane = obj.Id;
                    MoveableProc mpo = new MoveableProc(instanceID);
                    m_clones.Add(mpo);

                    mpo.angle = instance.angle;
                    mpo.position = instance.position;

                    selection.Add(mpo);
                    selection.Remove(instance);
                    instance.Delete();
                }
            }

            MoveItTool.m_debugPanel.UpdatePanel();
        }

        public override void Undo()
        {
            firstRun = false; // TODO this disables Redo
            if (m_states == null) return;

            List<CloneData> toReplace = new List<CloneData>();
            foreach (Instance clone in m_clones)
            {
                MoveItTool.PO.visibleObjects.Remove(clone.id.NetLane);
                clone.Delete();
            }

            foreach (InstanceState state in m_states)
            {
                if (state.instance is MoveableBuilding || state.instance is MoveableProp)
                {
                    Instance clone = state.instance.Clone(state, null);
                    toReplace.Add(new CloneData() { Original = state.instance, Clone = clone });
                    m_oldSelection.Add(clone);
                }
            }

            ReplaceInstances(toReplace);
            ActionQueue.instance.ReplaceInstancesBackward(toReplace);

            selection = m_oldSelection;
            MoveItTool.m_debugPanel.UpdatePanel();
        }

        public override void ReplaceInstances(List<CloneData> toReplace)
        {
            // Update this action's state instances with the updated instances
            foreach (InstanceState state in m_states)
            {
                CloneData data = CloneData.GetFromOriginal(toReplace, state.instance);
                if (data != null)
                {
                    Log.Debug($"ConvertToPO Replacing: {state.instance.id.Debug()}/{data.OriginalIId.Debug()} -> {data.CloneIId.Debug()}", "[M78.9]");
                    state.ReplaceInstance(data.Clone);
                }
            }

            // Update the selected instances
            if (m_oldSelection != null)
            {
                foreach (CloneData data in toReplace)
                {
                    if (m_oldSelection.Remove(data.Original))
                    {
                        Log.Debug($"ConvertToPO Replacing: {data.OriginalIId.Debug()} -> {data.CloneIId.Debug()}", "[M79.5]");
                        m_oldSelection.Add(data.Clone);
                    }
                }
            }
        }
    }
}
