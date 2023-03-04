using System.Collections.Generic;
using UnityEngine;
using static RenderManager;

namespace MoveIt
{
    public class SelectAction : Action
    {
        private HashSet<Instance> m_oldSelection;
        private HashSet<Instance> m_newSelection;

        public SelectAction(bool append = false)
        {
            m_oldSelection = selection;

            if (append && selection != null)
            {
                m_newSelection = new HashSet<Instance>(selection);
            }
            else
            {
                m_newSelection = new HashSet<Instance>();
            }

            selection = m_newSelection;
        }

        // Used by Prop Painter
        public void Add(Instance instance)
        {
            if (!selection.Contains(instance))
            {
                m_newSelection.AddObject(instance);
            }
        }

        public void Remove(Instance instance)
        {
            m_newSelection.RemoveObject(instance);
        }

        public override void Do()
        {
            selection = m_newSelection;
        }

        public override void Undo()
        {
            selection = m_oldSelection;
        }

        public override void ReplaceInstances(List<CloneData> toReplace)
        {
            // Update this action's state instances with the updated instances
            foreach (CloneData data in toReplace)
            {
                if (m_oldSelection.Remove(data.Original))
                {
                    DebugUtils.Log($"SelectAction Replacing: {data.OriginalIId.Debug()} -> {data.CloneIId.Debug()}", "[M79.3]");
                    m_oldSelection.Add(data.Clone);
                }

                if (m_newSelection.Remove(data.Original))
                {
                    DebugUtils.Log($"SelectAction Replacing: {data.OriginalIId.Debug()} -> {data.CloneIId.Debug()}", "[M79.4]");
                    m_newSelection.Add(data.Clone);
                }
            }
        }
    }

    public static class MyExtensions
    {
        public static void AddObject(this HashSet<Instance> selection, Instance ins)
        {
            selection.Add(ins);

            // Add the rest of the PO group
            if (ins is MoveableProc mpo)
            {
                if (mpo.m_procObj.Group == null)
                    return;

                foreach (PO_Object po in mpo.m_procObj.Group.objects)
                {
                    if (po.Id != mpo.m_procObj.Id)
                    {
                        InstanceID insId = default;
                        insId.NetLane = po.Id;
                        selection.Add(new MoveableProc(insId));
                    }
                }
            }
        }

        public static void RemoveObject(this HashSet<Instance> selection, Instance ins)
        {
            selection.Remove(ins);

            // Add the rest of the PO group
            if (ins is MoveableProc mpo)
            {
                if (mpo.m_procObj.Group == null)
                    return;

                foreach (PO_Object po in mpo.m_procObj.Group.objects)
                {
                    if (po.Id != mpo.m_procObj.Id)
                    {
                        InstanceID insId = default;
                        insId.NetLane = po.Id;
                        selection.Remove(new MoveableProc(insId));
                    }
                }
            }
        }
    }
}
