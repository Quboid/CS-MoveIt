using MoveIt.Tasks;
using UnityEngine;

namespace MoveIt
{
    public class ScaleAction : BaseTransformAction
    {
        public float magnitude;

        public override void Do()
        {
            Bounds originalBounds = GetTotalBounds(false);

            Matrix4x4 matrix4x = default;

            foreach (InstanceState state in m_states)
            {
                if (state.instance.isValid)
                {
                    Vector3 offset = (state.position - center) * magnitude;

                    matrix4x.SetTRS(state.position + offset, Quaternion.AngleAxis(0f, Vector3.down), Vector3.one);
                    state.instance.Transform(state, ref matrix4x, moveDelta.y, 0f, state.position, followTerrain);

                    if (autoCurve && state.instance is MoveableNode node)
                    {
                        node.AutoCurve(segmentCurve);
                    }
                    //new Task(this, Threads.Simulation, () => {
                    //    Vector3 offset = (state.position - center) * magnitude;

                    //    matrix4x.SetTRS(state.position + offset, Quaternion.AngleAxis(0f, Vector3.down), Vector3.one);
                    //    state.instance.Transform(state, ref matrix4x, moveDelta.y, 0f, state.position, followTerrain);

                    //    if (autoCurve && state.instance is MoveableNode node)
                    //    {
                    //        node.AutoCurve(segmentCurve);
                    //    }
                    //});
                }
            }

            bool full = (!Settings.fastMove);
            if (!full)
            {
                full = selection.Count > MoveItTool.Fastmove_Max ? true : false;
            }
            UpdateArea(originalBounds, full);
            Bounds fullbounds = GetTotalBounds(false);
            UpdateArea(fullbounds, full);
        }
    }
}
