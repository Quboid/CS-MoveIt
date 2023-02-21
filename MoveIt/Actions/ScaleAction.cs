using QCommonLib.QTasks;
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
                    MoveItTool.TaskManager.CreateTask(QTask.Threads.Simulation, () =>
                    {
                        Vector3 offset2 = (state.position - center) * magnitude;

                        matrix4x.SetTRS(state.position + offset2, Quaternion.AngleAxis(0f, Vector3.down), Vector3.one);
                        state.instance.Transform(state, ref matrix4x, moveDelta.y, 0f, state.position, followTerrain);

                        if (autoCurve && state.instance is MoveableNode n)
                        {
                            n.AutoCurve(segmentCurve);
                        }
                    });
                }
            }

            bool full = (!Settings.fastMove);
            if (!full)
            {
                full = selection.Count > MoveItTool.Fastmove_Max;
            }
            UpdateArea(originalBounds, full);
            Bounds fullbounds = GetTotalBounds(false);
            UpdateArea(fullbounds, full);
        }
    }
}
