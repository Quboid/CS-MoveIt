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
            foreach (InstanceState state in m_states)
            {
                if (state.instance.isValid)
                {
                    state.instance.SetHeight(height);
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
                    Log.Debug($"AlignHeightAction Replacing: {state.instance.id.Debug()}/{data.OriginalIId.Debug()} -> {data.CloneIId.Debug()}", "[M78.2]");
                    state.ReplaceInstance(data.Clone);
                }
            }
        }
    }
}
