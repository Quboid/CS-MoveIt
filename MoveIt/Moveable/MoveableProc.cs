using UnityEngine;
using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Threading;

namespace MoveIt
{
    public class ProcState : InstanceState
    {
        public void BeginClone(ref Matrix4x4 matrix4x, float deltaHeight, float deltaAngle, Vector3 center, bool followTerrain, CloneDataPO cloneData)
        {
            Vector3 newPosition = matrix4x.MultiplyPoint(position - center);
            newPosition.y = position.y + deltaHeight;

            if (followTerrain)
            {
                newPosition.y = newPosition.y + TerrainManager.instance.SampleOriginalRawHeightSmooth(newPosition) - terrainHeight;
            }

            cloneData.m_angle = angle + deltaAngle;
            cloneData.m_position = newPosition;
            cloneData.CloneState = this;

            MoveItTool.PO.Clone(cloneData);

            return;
        }
    }

    public class MoveableProc : Instance
    {
        internal PO_Object m_procObj;

        public override HashSet<ushort> segmentList
        {
            get
            {
                return new HashSet<ushort>();
            }
        }

        public int ProcId { get => (int)id.NetLane - 1; }
        
        public MoveableProc(InstanceID instanceID) : base(instanceID)
        {
            m_procObj = MoveItTool.PO.GetProcObj(instanceID.NetLane);
            Info = m_procObj.Info;
        }

        public override InstanceState SaveToState(bool integrate = true)
        {
            ProcState state = new ProcState
            {
                instance = this,
                Info = Info,
                position = m_procObj.Position,
                angle = m_procObj.Angle,
                isCustomContent = Info.Prefab.m_isCustomContent,
            };
            state.terrainHeight = TerrainManager.instance.SampleOriginalRawHeightSmooth(state.position);

            state.SaveIntegrations(integrate);

            return state;
        }

        public override void LoadFromState(InstanceState state)
        {
            m_procObj.Position = state.position;
            m_procObj.Angle = state.angle;
        }

        public override Vector3 position
        {
            get
            {
                if (!isValid) return Vector3.zero;
                return m_procObj.Position;
            }
            set
            {
                if (!isValid) return;
                m_procObj.Position = value;
            }
        }

        public override float angle
        {
            get
            {
                if (!isValid) return 0f;
                return m_procObj.Angle;
            }
            set
            {
                if (!isValid) return;
                m_procObj.Angle = (value + Mathf.PI * 2) % (Mathf.PI * 2);
            }
        }

        public override bool isValid
        {
            get
            {
                if (id.IsEmpty) return false;
                return true;
            }
        }

        // deltaAngleRad is cumulative delta since Transform Action started, CCW
        public override void Transform(InstanceState state, ref Matrix4x4 matrix4x, float deltaHeight, float deltaAngleRad, Vector3 center, bool followTerrain)
        {
            Vector3 newPosition = matrix4x.MultiplyPoint(state.position - center);
            newPosition.y = state.position.y + deltaHeight;

            if (followTerrain)
            {
                newPosition.y = newPosition.y + TerrainManager.instance.SampleOriginalRawHeightSmooth(newPosition) - state.terrainHeight;
            }

            float a = state.angle + deltaAngleRad;

            Move(newPosition, a % (Mathf.PI * 2));
        }

        // angleRad is absolute angle, CCW
        public override void Move(Vector3 location, float angleRad)
        {
            m_procObj.Position = location;
            m_procObj.Angle = angleRad;
        }

        public override void SetHeight(float height)
        {
            if (!isValid) return;
            m_procObj.SetPositionY(height);
        }

        public override void SetHeight()
        {
            SetHeight(TerrainManager.instance.SampleDetailHeight(position));
        }

        public override Instance Clone(InstanceState instanceState, ref Matrix4x4 matrix4x, float deltaHeight, float deltaAngle, Vector3 center, bool followTerrain, Dictionary<ushort, ushort> clonedNodes, Action action)
        {
            throw new Exception($"Called Clone for MoveableProc");

            //ProcState state = instanceState as ProcState;

            //Vector3 newPosition = matrix4x.MultiplyPoint(state.position - center);
            //newPosition.y = state.position.y + deltaHeight;

            //if (followTerrain)
            //{
            //    newPosition.y = newPosition.y + TerrainManager.instance.SampleOriginalRawHeightSmooth(newPosition) - state.terrainHeight;
            //}

            //MoveItTool.PO.Clone(state, newPosition, state.angle + deltaAngle, action);

            //return null;
        }

        public override Instance Clone(InstanceState instanceState, Dictionary<ushort, ushort> clonedNodes)
        {
            return instanceState.instance;
        }

        public override void Delete()
        {
            MoveItTool.PO.visibleObjects.Remove(id.NetLane);
            MoveItTool.PO.Delete(m_procObj);
        }

        public override Bounds GetBounds(bool ignoreSegments = true)
        {
            return new Bounds(m_procObj.Position, new Vector3(8, 0, 8));
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo, Color toolColor, Color despawnColor)
        {
            if (!isValid) return;
            if (MoveItTool.m_isLowSensitivity) return;

            if (m_procObj.Group is null)
            {
                m_procObj.RenderOverlay(cameraInfo, toolColor);
            }
            else
            {
                if (m_procObj.Group.root == m_procObj)
                {
                    foreach (PO_Object po in m_procObj.Group.objects)
                    {
                        if (toolColor == MoveItTool.Colors.POSelected) toolColor = MoveItTool.Colors.POselectedGroup;
                        if (toolColor == MoveItTool.Colors.POHover) toolColor = MoveItTool.Colors.POhoverGroup;
                        po.RenderOverlay(cameraInfo, toolColor.Adjusted());
                    }
                }
            }
        }

        public override void RenderCloneOverlay(InstanceState instanceState, ref Matrix4x4 matrix4x, Vector3 deltaPosition, float deltaAngle, Vector3 center, bool followTerrain, RenderManager.CameraInfo cameraInfo, Color toolColor)
        {
            if (!isValid) return;
            if (MoveItTool.m_isLowSensitivity) return;

            ProcState state = instanceState as ProcState;

            Vector3 newPosition = matrix4x.MultiplyPoint(state.position - center);
            newPosition.y = state.position.y + deltaPosition.y;

            if (followTerrain)
            {
                newPosition.y = newPosition.y - state.terrainHeight + TerrainManager.instance.SampleOriginalRawHeightSmooth(newPosition);
            }

            m_procObj.RenderOverlay(cameraInfo, toolColor, newPosition);
        }

        public override void RenderCloneGeometry(InstanceState instanceState, ref Matrix4x4 matrix4x, Vector3 deltaPosition, float deltaAngle, Vector3 center, bool followTerrain, RenderManager.CameraInfo cameraInfo, Color toolColor)
        {
            if (m_procObj.Info is null || m_procObj.Info.Prefab is null)
                return;

            PrefabInfo pi = m_procObj.Info.Prefab;


            if (pi is BuildingInfo)
            {
                MoveableBuilding.RenderCloneGeometryImplementation(instanceState, ref matrix4x, deltaPosition, deltaAngle, center, followTerrain, cameraInfo);
            }
            else
            {
                MoveableProp.RenderCloneGeometryImplementation(instanceState, ref matrix4x, deltaPosition, deltaAngle, center, followTerrain, cameraInfo);
            }
        }
    }
}
