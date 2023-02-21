using QCommonLib.QTasks;
using System.Collections.Generic;

namespace MoveIt
{
    public class AlignHeightAction : Action
    {
        public float height;

        private HashSet<InstanceState> m_states = new HashSet<InstanceState>();

        public AlignHeightAction() : base()
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
                    tasks.Add(MoveItTool.TaskManager.CreateTask(QTask.Threads.Simulation, () => {
                        state.instance.SetHeight(height);
                    }));
                }
            }

            MoveItTool.TaskManager.AddBatch(tasks, null, MoveItTool.TaskManager.CreateTask(QTask.Threads.Simulation, () => { UpdateArea(GetTotalBounds(false)); }), "AlignHeight-Do-01");
        }

        public override void Undo()
        {
            List<QTask> tasks = new List<QTask>();

            foreach (InstanceState state in m_states)
            {
                tasks.Add(MoveItTool.TaskManager.CreateTask(QTask.Threads.Simulation, () => {
                    state.instance.LoadFromState(state);
                }));
            }

            MoveItTool.TaskManager.AddBatch(tasks, null, MoveItTool.TaskManager.CreateTask(QTask.Threads.Simulation, () => { UpdateArea(GetTotalBounds(false)); }), "AlignHeight-Undo-01");
        }

        public override void ReplaceInstances(List<CloneData> toReplace)
        {
            // Update this action's state instances with the updated instances
            foreach (InstanceState state in m_states)
            {
                CloneData data = CloneData.GetFromOriginal(toReplace, state.instance);
                if (data != null)
                {
                    Log.Debug($"AlignHeightAction Replacing: {state.instance.id.Debug()}/{data.OriginalIId.Debug()} -> {data.CloneIId.Debug()}", "[M78.2]");
                    state.ReplaceInstance(data.Clone);
                }
            }
        }
    }
}
