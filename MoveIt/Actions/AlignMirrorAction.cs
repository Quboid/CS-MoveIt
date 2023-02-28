using System;
using System.Collections.Generic;
using UnityEngine;
using QCommonLib.QTasks;

namespace MoveIt
{
    class AlignMirrorAction : CloneActionMain
    {
        public Vector3 mirrorPivot;
        public float mirrorAngle;
        private Bounds originalBounds;

        public AlignMirrorAction() : base() {}

        public override void Do()
        {
            originalBounds = GetTotalBounds(false);

            MoveItTool.TaskManager.AddSingleTask(MoveItTool.TaskManager.CreateTask(QTask.Threads.Simulation, DoImplementation), "Mirror-Do-01");

            // Queue batch to queue additional batch, so final batch is run after those added by the above batch
            MoveItTool.TaskManager.AddSingleTask(MoveItTool.TaskManager.CreateTask(QTask.Threads.Simulation, () =>
            {
                MoveItTool.TaskManager.AddSingleTask(MoveItTool.TaskManager.CreateTask(QTask.Threads.Simulation, DoMirrorProcess), "Mirror-Do-03");
                return true;
            }), "Mirror-Do-02");
        }

        public bool DoMirrorProcess()
        {
            Dictionary<Instance, float> instanceRotations = new Dictionary<Instance, float>();

            Matrix4x4 matrix4x = default;
            foreach (CloneData cloneData in m_cloneData)
            {
                if (cloneData.Clone.isValid)
                {
                    InstanceState state = null;

                    foreach (CloneData candidate in m_cloneData)
                    {
                        if (candidate.CloneIId.RawData == cloneData.CloneIId.RawData)
                        {
                            if (candidate.Clone is MoveableSegment)
                            { // Segments need original state because nodes move before clone's position is saved
                                state = candidate.Original.SaveToState();
                            }
                            else
                            { // Buildings need clone state to access correct subInstances. Others don't matter, but clone makes most sense
                                state = candidate.Clone.SaveToState();
                            }
                            break;
                        }
                    }

                    if (state == null)
                    {
                        throw new NullReferenceException($"Original for cloned object not found.");
                    }

                    float faceDelta = getMirrorFacingDelta(state.angle, mirrorAngle);
                    float posDelta = getMirrorPositionDelta(state.position, mirrorPivot, mirrorAngle);
                    instanceRotations[cloneData.Clone] = faceDelta;

                    matrix4x.SetTRS(mirrorPivot, Quaternion.AngleAxis(posDelta * Mathf.Rad2Deg, Vector3.down), Vector3.one);

                    cloneData.Clone.Transform(state, ref matrix4x, 0f, faceDelta, mirrorPivot, followTerrain);
                }
            }

            ReattachNodes();

            // Mirror integrations, including lanes
            Dictionary<InstanceID, InstanceID> mapOrigToClone = new Dictionary<InstanceID, InstanceID>(m_mapLanes);
            foreach (CloneData cloneData in m_cloneData)
            {
                mapOrigToClone.Add(cloneData.OriginalIId, cloneData.CloneIId);
            }

            foreach (CloneData cloneData in m_cloneData)
            {
                foreach (var data in cloneData.CloneState.IntegrationData)
                {
                    try
                    {
                        CallIntegration(data.Key, cloneData.CloneIId, data.Value, mapOrigToClone, instanceRotations[cloneData.Clone], mirrorAngle);
                    }
                    catch (Exception e)
                    {
                        InstanceID sourceInstanceID = cloneData.StateIId;
                        InstanceID targetInstanceID = cloneData.CloneIId;
                        Log.Error($"integration {data.Key} Failed to paste from " +
                            $"{sourceInstanceID.Type}:{sourceInstanceID.Index} to {targetInstanceID.Type}:{targetInstanceID.Index}", "[M21]");
                        DebugUtils.LogException(e);
                    }
                }
            }

            //string msg = $"Cloned Objects: {m_cloneData.Count}";
            //foreach (CloneData cloneData in m_cloneData)
            //{
            //    msg += $"\n  {cloneData.OriginalIId.Debug()}->{cloneData.CloneIId.Debug()}";
            //}
            //Log.Debug(msg);

            bool fast = Settings.fastMove != Event.current.shift;
            UpdateArea(originalBounds, !fast || ((TypeMask & TypeMasks.Network) != TypeMasks.None));
            UpdateArea(GetTotalBounds(false), !fast);
            return true;
        }

        private void CallIntegration(MoveItIntegration.MoveItIntegrationBase method, InstanceID id, object data, Dictionary<InstanceID, InstanceID> map, float instanceRotation, float mirrorRotation)
        {
            method.Mirror(id, data, map, instanceRotation, mirrorRotation);
        }

        public static float getMirrorFacingDelta(float startAngle, float mirrorAngle)
        {
            return (startAngle - ((startAngle - mirrorAngle) * 2) - startAngle) % ((float)Math.PI * 2);
        }

        public static float getMirrorPositionDelta(Vector3 start, Vector3 mirrorOrigin, float angle)
        {
            Vector3 offset = start - mirrorOrigin;
            return (angle + Mathf.Atan2(offset.x, offset.z)) * 2;
        }
    }
}
