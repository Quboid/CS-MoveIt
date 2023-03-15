using QCommonLib.QTasks;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoveIt
{
    public class TransformAction : BaseTransformAction
    {
    }

    public class MoveToAction : BaseTransformAction
    {
        internal Vector3 Original, Position;
        internal float AngleOriginal, Angle;
        internal bool AngleActive, HeightActive;

        public override void Undo()
        {
            MoveItTool.instance.DeactivateTool();
            
            base.Undo();
        }
    }

    public abstract class BaseTransformAction : Action
    {
        public Vector3 moveDelta;
        public Vector3 center;
        public float angleDelta;
        public float snapAngle;
        public bool followTerrain;

        public bool autoCurve;
        public NetSegment segmentCurve;

        public HashSet<InstanceState> m_states = new HashSet<InstanceState>();

        internal bool _virtual = false;
        public bool Virtual
        {
            get => _virtual;
            set
            {
                if (value == true)
                {
                    if (_virtual == false && selection.Count < MoveItTool.Fastmove_Max)
                    {
                        _virtual = true;
                        foreach (Instance i in selection)
                        {
                            i.Virtual = true;
                        }
                    }
                }
                else
                {
                    if (_virtual == true)
                    {
                        _virtual = false;
                        foreach (Instance i in selection)
                        {
                            i.Virtual = false;
                        }
                        Do();
                        UpdateArea(GetTotalBounds(), true);
                    }
                }
            }
        }

        public BaseTransformAction() : base()
        {
            foreach (Instance instance in selection)
            {
                if (instance.isValid)
                {
                    m_states.Add(instance.SaveToState(false));
                }
            }

            m_states = ProcessPillars(m_states, true);
            center = GetCenter();
        }

        public override void Do()
        {
            QTaskManager.QueueOnSimulation(() => DoImplemenation());
        }

        private bool DoImplemenation()
        {
            if (!PillarsProcessed) ProcessPillars(m_states, true);

            Bounds originalBounds = GetTotalBounds(false);

            Matrix4x4 matrix4x = default;
            matrix4x.SetTRS(center + moveDelta, Quaternion.AngleAxis((angleDelta + snapAngle) * Mathf.Rad2Deg, Vector3.down), Vector3.one);

            foreach (InstanceState state in m_states)
            {
                if (state.instance.isValid && !(state is SegmentState))
                {
                    state.instance.Transform(state, ref matrix4x, moveDelta.y, angleDelta + snapAngle, center, followTerrain);

                    if (autoCurve && state.instance is MoveableNode node)
                    {
                        node.AutoCurve(segmentCurve);
                    } 
                    else if (state.instance is MoveableBuilding building)
                    {
                        BuildingManager.instance.RoadCheckNeeded(building.id.Building);
                    }
                }
            }

            // Move segments after the nodes have moved
            foreach (InstanceState state in m_states)
            {
                if (state.instance.isValid && state is SegmentState)
                {
                    state.instance.Transform(state, ref matrix4x, moveDelta.y, angleDelta + snapAngle, center, followTerrain);
                }
            }

            bool full = !(Settings.fastMove != Event.current.shift);
            if (!full)
            {
                full = selection.Count > MoveItTool.Fastmove_Max;
            }

            Bounds fullbounds = GetTotalBounds(false);
            UpdateArea(originalBounds, full);
            UpdateArea(fullbounds, full);
            return true;
        }

        public override void Undo()
        {
            MoveItTool.TaskManager.AddSingleTask(QTask.Threads.Simulation, UndoImplemenation, "Transform-Undo");
        }

        private bool UndoImplemenation()
        { 
            PillarsProcessed = false;

            Bounds bounds = GetTotalBounds(false);

            foreach (InstanceState state in m_states)
            {
                if (!(state is SegmentState))
                {
                    state.instance.LoadFromState(state);
                }
            }

            foreach (InstanceState state in m_states)
            {
                if (state is SegmentState)
                {
                    state.instance.LoadFromState(state);
                }
            }

            // Does not check MoveItTool.advancedPillarControl, because even if disabled now advancedPillarControl may have been active earlier in action queue
            foreach (KeyValuePair<BuildingState, BuildingState> pillarClone in pillarsOriginalToClone)
            {
                BuildingState originalState = pillarClone.Key;
                BuildingState cloneState = pillarClone.Value;
                cloneState.instance.Delete();
                originalState.instance.isHidden = false;
                buildingBuffer[originalState.instance.id.Building].m_flags &= ~Building.Flags.Hidden;
                selection.Remove(cloneState.instance);
                selection.Add(originalState.instance);
                m_states.Remove(cloneState);
                m_states.Add(originalState);
            }
            if (pillarsOriginalToClone.Count > 0)
            {
                MoveItTool.UpdatePillarMap();
            }

            UpdateArea(bounds, true);
            UpdateArea(GetTotalBounds(false), true);

            return true;
        }

        public void InitialiseDrag()
        {
            MoveItTool.dragging = true;
            Virtual = false;

            foreach (InstanceState instanceState in m_states)
            {
                if (instanceState.instance is MoveableBuilding mb)
                {
                    mb.InitialiseDrag();
                }
            }
        }

        public void FinaliseDrag()
        {
            MoveItTool.dragging = false;
            Virtual = false;

            foreach (InstanceState instanceState in m_states)
            {
                if (instanceState.instance is MoveableBuilding mb)
                {
                    mb.FinaliseDrag();
                }
            }
        }

        public override void ReplaceInstances(List<CloneData> toReplace)
        {
            // Update this action's state instances with the updated instances
            foreach (InstanceState state in m_states)
            {
                CloneData data = CloneData.GetFromOriginal(toReplace, state.instance);
                if (data != null)
                {
                    Log.Debug($"TransformAction Replacing: {state.instance.id.Debug()}/{data.OriginalIId.Debug()} -> {data.CloneIId.Debug()}", "[M78.8]");
                    state.ReplaceInstance(data.Clone);
                }
            }
        }

        public HashSet<InstanceState> CalculateStates(Vector3 deltaPosition, float deltaAngle, Vector3 center, bool followTerrain)
        {
            Matrix4x4 matrix4x = default;
            matrix4x.SetTRS(center + deltaPosition, Quaternion.AngleAxis(deltaAngle * Mathf.Rad2Deg, Vector3.down), Vector3.one);

            HashSet<InstanceState> newStates = new HashSet<InstanceState>();

            foreach (InstanceState state in m_states)
            {
                if (state.instance.isValid)
                {
                    InstanceState newState = (InstanceState)Activator.CreateInstance(state.GetType()); // Maintain exact class type
                    newState.instance = state.instance;
                    newState.Info = state.Info;

                    newState.position = matrix4x.MultiplyPoint(state.position - center);
                    newState.position.y = state.position.y + deltaPosition.y;

                    if (followTerrain)
                    {
                        newState.terrainHeight = TerrainManager.instance.SampleOriginalRawHeightSmooth(newState.position);
                        newState.position.y = newState.position.y + newState.terrainHeight - state.terrainHeight;
                    }

                    newState.angle = state.angle + deltaAngle;

                    newStates.Add(newState);
                }
            }
            return newStates;
        }
    }
}
