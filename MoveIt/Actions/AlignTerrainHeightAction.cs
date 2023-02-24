﻿using QCommonLib.QTasks;
using System.Collections.Generic;

namespace MoveIt
{
    public class AlignTerrainHeightAction : Action
    {
        private HashSet<InstanceState> m_states = new HashSet<InstanceState>();

        public AlignTerrainHeightAction() : base()
        {
            foreach (Instance instance in selection)
            {
                if (instance.isValid)
                {
                    m_states.Add(instance.SaveToState(false));
                }
            }
        }

        public override void Do()
        {
            List<QTask> tasks = new List<QTask>();

            foreach (InstanceState state in m_states)
            {
                if (state.instance.isValid)
                {
                    if (state.instance.id.Type == InstanceType.Building)
                    {
                        tasks.Add(MoveItTool.TaskManager.CreateTask(QTask.Threads.Simulation, () => {
                            state.instance.SetHeight();
                            return true;
                        }));
                    }
                }
            }

            MoveItTool.TaskManager.AddBatch(tasks, null, null, "AlignTerrainHeight-Do-01");

            tasks = new List<QTask>();

            foreach (InstanceState state in m_states)
            {
                if (state.instance.isValid)
                {
                    if (state.instance.id.Type != InstanceType.Building)
                    {
                        tasks.Add(MoveItTool.TaskManager.CreateTask(QTask.Threads.Simulation, () => {
                            state.instance.SetHeight();
                            return true;
                        }));
                    }
                }
            }

            MoveItTool.TaskManager.AddBatch(tasks, null, MoveItTool.TaskManager.CreateTask(QTask.Threads.Simulation, () => { UpdateArea(GetTotalBounds(false)); return true; }), "AlignTerrainHeight-Do-02");
        }

        public override void Undo()
        {
            List<QTask> tasks = new List<QTask>();

            foreach (InstanceState state in m_states)
            {
                tasks.Add(MoveItTool.TaskManager.CreateTask(QTask.Threads.Simulation, () => {
                    state.instance.LoadFromState(state);
                    return true;
                }));
            }

            MoveItTool.TaskManager.AddBatch(tasks, null, MoveItTool.TaskManager.CreateTask(QTask.Threads.Simulation, () => { UpdateArea(GetTotalBounds(false)); return true; }), "AlignTerrainHeight-Undo-01");
        }

        public override void ReplaceInstances(List<CloneData> toReplace)
        {
            // Update this action's state instances with the updated instances
            foreach (InstanceState state in m_states)
            {
                CloneData data = CloneData.GetFromOriginal(toReplace, state.instance);
                if (data != null)
                {
                    Log.Debug($"AlignTerrainHeightAction Replacing: {state.instance.id.Debug()}/{data.OriginalIId.Debug()} -> {data.CloneIId.Debug()}", "[M78.5]");
                    state.ReplaceInstance(data.Clone);
                }
            }
        }
    }
}
