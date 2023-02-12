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
            foreach (InstanceState state in m_states)
            {
                if (state.instance.isValid)
                {
                    if (state.instance.id.Building > 0)
                    {
                        state.instance.SetHeight();
                    }
                }
            }
            foreach (InstanceState state in m_states)
            {
                if (state.instance.isValid)
                {
                    if (state.instance.id.Building == 0)
                    {
                        state.instance.SetHeight();
                    }
                }
            }

            UpdateArea(GetTotalBounds(false));
        }

        public override void Undo()
        {
            foreach (InstanceState state in m_states)
            {
                state.instance.LoadFromState(state);
            }

            UpdateArea(GetTotalBounds(false));
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
