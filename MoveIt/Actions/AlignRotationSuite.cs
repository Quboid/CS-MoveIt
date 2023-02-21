using System;
using ColossalFramework;
using System.Collections.Generic;
using UnityEngine;

namespace MoveIt
{
    public class AlignIndividualAction : AlignRotationAction
    {
    }

    public class AlignGroupAction : AlignRotationAction
    {
    }

    public class AlignRandomAction : AlignRotationAction
    {
    }

    public class AlignRotationAction : Action
    {
        public float newAngle;
        public bool followTerrain;
        public HashSet<InstanceState> m_states = new HashSet<InstanceState>();

        public AlignRotationAction() : base()
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
            MoveItTool.TaskManager.AddTask(new QTasks.QTask(QTasks.QTask.Threads.Simulation, () => { DoImplementation(); }), "AlignRotation-01");
        }

        public void DoImplementation()
        { 
            Vector3 PoR;
            Matrix4x4 matrix = default;
            Bounds bounds = GetTotalBounds();
            float angleDelta;
            System.Random random = new System.Random();
            BuildingManager buildingManager = Singleton<BuildingManager>.instance;

            angleDelta = 0 - GetAngle() + newAngle;
            PoR = bounds.center;

            foreach (InstanceState state in m_states)
            {
                if (state.instance.isValid)
                {
                    if (state.instance is MoveableBuilding mb)
                    {
                        if (Mathf.Abs(Singleton<TerrainManager>.instance.SampleOriginalRawHeightSmooth(mb.position) - mb.position.y) > 0.01f)
                        {
                            mb.AddFixedHeightFlag(mb.id.Building);
                        }
                        else
                        {
                            mb.RemoveFixedHeightFlag(mb.id.Building);
                        }

                        if (this is AlignIndividualAction)
                        {
                            angleDelta = 0 - mb.angle + newAngle;
                            PoR = state.position;
                        }
                        else if (this is AlignRandomAction)
                        {
                            angleDelta = 0 - mb.angle + (float)(random.NextDouble() * Math.PI * 2);
                            PoR = state.position;
                        }

                        matrix.SetTRS(PoR, Quaternion.AngleAxis(angleDelta * Mathf.Rad2Deg, Vector3.down), Vector3.one);
                        mb.Transform(state, ref matrix, 0f, angleDelta, PoR, followTerrain);

                        BuildingInfo prefab = (BuildingInfo)state.Info.Prefab;
                        ushort id = mb.id.Building;
                        Building building = buildingManager.m_buildings.m_buffer[id];

                        if (prefab.m_hasParkingSpaces != VehicleInfo.VehicleType.None)
                        {
                            buildingManager.UpdateParkingSpaces(id, ref building);
                        }

                        buildingManager.UpdateBuildingRenderer(id, true);
                    }
                    else if (state.instance is MoveableProp mp)
                    {
                        if (this is AlignIndividualAction)
                        {
                            angleDelta = 0 - mp.angle + newAngle;
                            PoR = state.position;
                        }
                        else if (this is AlignRandomAction)
                        {
                            angleDelta = 0 - mp.angle + (float)(random.NextDouble() * Math.PI * 2);
                            PoR = state.position;
                        }
                        matrix.SetTRS(PoR, Quaternion.AngleAxis(angleDelta * Mathf.Rad2Deg, Vector3.down), Vector3.one);
                        mp.Transform(state, ref matrix, 0f, angleDelta, PoR, followTerrain);
                    }
                    else if (state.instance is MoveableNode mn)
                    {
                        if (this is AlignIndividualAction)
                        {
                            angleDelta = 0 - mn.angle + newAngle;
                            PoR = state.position;
                        }
                        else if (this is AlignRandomAction)
                        {
                            angleDelta = 0 - mn.angle + (float)(random.NextDouble() * Math.PI * 2);
                            PoR = state.position;
                        }

                        matrix.SetTRS(PoR, Quaternion.AngleAxis(angleDelta * Mathf.Rad2Deg, Vector3.down), Vector3.one);
                        mn.Transform(state, ref matrix, 0f, angleDelta, PoR, followTerrain);
                    }
                    else if (state.instance is MoveableProc mpo)
                    {
                        if (this is AlignIndividualAction)
                        {
                            angleDelta = 0 - mpo.angle + newAngle;
                            PoR = state.position;
                        }
                        else if (this is AlignRandomAction)
                        {
                            angleDelta = 0 - mpo.angle + (float)(random.NextDouble() * Math.PI * 2);
                            PoR = state.position;
                        }
                        matrix.SetTRS(PoR, Quaternion.AngleAxis(angleDelta * Mathf.Rad2Deg, Vector3.down), Vector3.one);
                        mpo.Transform(state, ref matrix, 0f, angleDelta, PoR, followTerrain);
                    }
                    else if (state.instance is MoveableTree mt)
                    {
                        if (this is AlignIndividualAction || this is AlignRandomAction)
                        {
                            continue;
                        }
                        matrix.SetTRS(PoR, Quaternion.AngleAxis(angleDelta * Mathf.Rad2Deg, Vector3.down), Vector3.one);
                        mt.Transform(state, ref matrix, 0f, angleDelta, PoR, followTerrain);
                    }
                }
            }

            // Move segments after nodes, for updated positions
            foreach (InstanceState state in m_states)
            {
                if (state.instance.isValid)
                {
                    if (state.instance is MoveableSegment ms)
                    {
                        if (this is AlignIndividualAction)
                        {
                            angleDelta = 0 - ms.angle + newAngle;
                            PoR = state.position;
                        }
                        else if (this is AlignRandomAction)
                        {
                            angleDelta = 0 - ms.angle + (float)(random.NextDouble() * Math.PI * 2);
                            PoR = state.position;
                        }

                        matrix.SetTRS(PoR, Quaternion.AngleAxis(angleDelta * Mathf.Rad2Deg, Vector3.down), Vector3.one);
                        ms.Transform(state, ref matrix, 0f, angleDelta, PoR, followTerrain);
                    }
                }
            }

            MoveItTool.SetToolState();
            UpdateArea(bounds);
            UpdateArea(GetTotalBounds(false));
        }


        public override void Undo()
        {
            Bounds bounds = GetTotalBounds(false);

            foreach (InstanceState state in m_states)
            {
                state.instance.LoadFromState(state);
            }

            UpdateArea(bounds);
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
                    Log.Debug($"AlignRotationAction Replacing: {state.instance.id.Debug()}/{data.OriginalIId.Debug()} -> {data.CloneIId.Debug()}", "[M78.4]");
                    state.ReplaceInstance(data.Clone);
                }
            }
        }
    }
}
