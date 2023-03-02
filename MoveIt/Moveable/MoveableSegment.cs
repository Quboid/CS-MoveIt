using ColossalFramework;
using ColossalFramework.Math;
using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using UnityEngine;

namespace MoveIt
{
    public class SegmentState : InstanceState
    {
        public ushort startNodeId;
        public ushort endNodeId;

        public Vector3 startPosition;
        public Vector3 endPosition;

        public Vector3 startDirection;
        public Vector3 endDirection;

        public bool smoothStart;
        public bool smoothEnd;

        public bool invert;

        public string treeInfoName
        {
            get
            {
                if (treeInfo is null)
                {
                    return "";
                }
                return treeInfo.name;
            }
            set => treeInfo = PrefabCollection<TreeInfo>.FindLoaded(value);
        }

        [XmlIgnore]
        public TreeInfo treeInfo;

        [XmlIgnore]
        public object NS_Modifiers;

        [UsedImplicitly]
        public string NS_ModifiersBase64
        {
            get
            {
                return MoveItTool.NS.EncodeModifiers(NS_Modifiers);
            }
            set
            {
                NS_Modifiers = MoveItTool.NS.DecodeModifiers(value);
            }
        }

        [XmlIgnore]
        public List<uint> LaneIDs;

        [UsedImplicitly]
        public string LaneIDsBase64
        {
            get => EncodeUtil.Encode64(LaneIDs);
            set => LaneIDs = EncodeUtil.Decode64(value) as List<uint>;
        }

        public override void ReplaceInstance(Instance instance)
        {
            base.ReplaceInstance(instance);

            NetSegment[] segmentBuffer = NetManager.instance.m_segments.m_buffer;

            startNodeId = segmentBuffer[instance.id.NetSegment].m_startNode;
            endNodeId = segmentBuffer[instance.id.NetSegment].m_endNode;
        }

        // Move It <= 2.6 compatiblity
#pragma warning disable IDE1006 // Naming Styles
        public ushort startNode
        {
            get => startNodeId;
            set => startNodeId = value;
        }
#pragma warning disable IDE1006 // Naming Styles
        public ushort endNode
        {
            get => endNodeId;
            set => endNodeId = value;
        }
    }

    public class MoveableSegment : Instance
    {
        public override HashSet<ushort> segmentList
        {
            get
            {
                return new HashSet<ushort>{id.NetSegment};
            }
        }

        public MoveableSegment(InstanceID instanceID) : base(instanceID)
        {
            Info = new Info_Prefab(NetManager.instance.m_segments.m_buffer[instanceID.NetSegment].Info);
        }

        public override InstanceState SaveToState(bool integrate = true)
        {
            ushort segment = id.NetSegment;

            SegmentState state = new SegmentState
            {
                instance = this,
                Info = Info,

                position = GetControlPoint(),

                startNodeId = segmentBuffer[segment].m_startNode,
                endNodeId = segmentBuffer[segment].m_endNode,

                startDirection = segmentBuffer[segment].m_startDirection,
                endDirection = segmentBuffer[segment].m_endDirection,

                NS_Modifiers = MoveItTool.NS.GetSegmentModifiers(segment),
               
                LaneIDs = GetLaneIds(segment),

                isCustomContent = Info.Prefab.m_isCustomContent
            };

            state.startPosition = nodeBuffer[state.startNodeId].m_position;
            state.endPosition = nodeBuffer[state.endNodeId].m_position;

            state.smoothStart = ((nodeBuffer[state.startNodeId].m_flags & NetNode.Flags.Middle) != NetNode.Flags.None);
            state.smoothEnd = ((nodeBuffer[state.endNodeId].m_flags & NetNode.Flags.Middle) != NetNode.Flags.None);

            state.invert = ((segmentBuffer[segment].m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.Invert);

            state.treeInfo = treeInfo;

            state.SaveIntegrations(integrate);

            return state;
        }

        public static List<uint> GetLaneIds(ushort segmentId)
        {
            if (segmentBuffer[segmentId].Info == null)
            {
                Log.Error("null info: potentially cuased by missing assets", "[M51]");
                return null;
            }

            var lanes = segmentBuffer[segmentId].Info.m_lanes;
            List<uint> ret = new List<uint>(lanes.Length);
            uint laneId = segmentBuffer[segmentId].m_lanes;
            for (int laneIndex = 0; laneIndex < lanes.Length && laneId !=0; ++laneIndex)
            {
                ret.Add(laneId);
                laneId = laneBuffer[laneId].m_nextLane;
            }
            return ret; 
        }

        public override void LoadFromState(InstanceState state)
        {
            if (!(state is SegmentState segmentState)) return;

            ushort segment = id.NetSegment;

            segmentBuffer[segment].m_startDirection = segmentState.startDirection;
            segmentBuffer[segment].m_endDirection = segmentState.endDirection;

            UpdateSegmentBlocks(segment, ref segmentBuffer[segment]);

            netManager.UpdateNode(segmentBuffer[segment].m_startNode);
            netManager.UpdateNode(segmentBuffer[segment].m_endNode);

            treeInfo = segmentState.treeInfo;

            MoveItTool.NS.SetSegmentModifiers(segment, segmentState);
        }

        public override Vector3 position
        {
            get
            {
                if (id.IsEmpty) return Vector3.zero;
                return GetControlPoint();
            }
            set { }
        }

        public override float angle
        {
            get { return 0f; }
            set { }
        }

        private MoveableNode _startNode = null;
        public MoveableNode StartNode
        {
            get
            {
                InstanceID instanceID = default;
                instanceID.NetNode = segmentBuffer[id.NetSegment].m_startNode;
                _startNode = GetNodeFromSelection(instanceID);
                if (_startNode == null)
                {
                    _startNode = new MoveableNode(instanceID);
                }

                return _startNode;
            }
        }

        private MoveableNode _endNode = null;
        public MoveableNode EndNode
        {
            get
            {
                InstanceID instanceID = default;
                instanceID.NetNode = segmentBuffer[id.NetSegment].m_endNode;
                _endNode = GetNodeFromSelection(instanceID);
                if (_endNode == null)
                {
                    _endNode = new MoveableNode(instanceID);
                }

                return _endNode;
            }
        }

        public TreeInfo treeInfo
        {
            get => ((NetSegment)data).TreeInfo;
            set
            {
                NetSegment seg = (NetSegment)data;
                seg.TreeInfo = value;
            }
        }

        public override bool isValid
        {
            get
            {
                if (id.IsEmpty) return false;
                return (segmentBuffer[id.NetSegment].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None;
            }
        }

        internal override void InitialiseTransform()
        {
            TransformPosition = GetControlPointReal();
        }

        public static bool isSegmentValid(ushort segmentId)
        {
            return (segmentBuffer[segmentId].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None;
        }

        public override void Transform(InstanceState state, ref Matrix4x4 matrix4x, float deltaHeight, float deltaAngle, Vector3 center, bool followTerrain)
        {
            Vector3 newPosition = matrix4x.MultiplyPoint(state.position - center);

            Move(newPosition, 0);
        }

        public override void Move(Vector3 location, float angle)
        {
            //Singleton<SimulationManager>.instance.AddAction(() => MoveProcess(location));
            MoveProcess(location);
        }

        internal void MoveProcess(Vector3 location)
        {
            if (!isValid) return;

            TransformPosition = location;

            if (!isVirtual())
            {
                ushort segment = id.NetSegment;
                ushort startNode = segmentBuffer[segment].m_startNode;
                ushort endNode = segmentBuffer[segment].m_endNode;

                segmentBuffer[segment].m_startDirection = location - nodeBuffer[startNode].m_position;
                segmentBuffer[segment].m_endDirection = location - nodeBuffer[endNode].m_position;

                CalculateSegmentDirections(ref segmentBuffer[segment], segment);

                netManager.UpdateSegmentRenderer(segment, true);
                UpdateSegmentBlocks(segment, ref segmentBuffer[segment]);

                netManager.UpdateNode(startNode);
                netManager.UpdateNode(endNode);
            }
        }

        public MoveableNode GetNodeByDistance(bool furthest = false)
        {
            Ray mouseRay = Camera.main.ScreenPointToRay(Input.mousePosition);
            Vector3 clickPos = MoveItTool.RaycastMouseLocation(mouseRay);
            if (Vector3.Distance(StartNode.position, clickPos) < Vector3.Distance(EndNode.position, clickPos))
            {
                if (furthest)
                {
                    return EndNode;
                }
                else
                {
                    return StartNode;
                }
            }
            else
            {
                if (furthest)
                {
                    return StartNode;
                }
                else
                {
                    return EndNode;
                }
            }
        }

        public override void SetHeight(float height) { }
        public override void SetHeight() { }

        public override Instance Clone(InstanceState instanceState, ref Matrix4x4 matrix4x, float deltaHeight, float deltaAngle, Vector3 center, bool followTerrain, Dictionary<ushort, ushort> clonedNodes, Action action)
        {
            SegmentState state = instanceState as SegmentState;

            Vector3 newPosition = matrix4x.MultiplyPoint(state.position - center);

            Instance cloneInstance = null;

            ushort startNodeId = state.startNodeId;
            ushort endNodeId = state.endNodeId;

            // Nodes should exist
            try
            {
                startNodeId = clonedNodes[startNodeId];
                endNodeId = clonedNodes[endNodeId];
            }
            catch (KeyNotFoundException)
            {
                Log.Debug($"{startNodeId}->{state.startNodeId}, {endNodeId}->{state.endNodeId}", "[M52]");
            }

            Vector3 startDirection = newPosition - nodeBuffer[startNodeId].m_position;
            Vector3 endDirection = newPosition - nodeBuffer[endNodeId].m_position;

            startDirection.y = 0;
            endDirection.y = 0;

            startDirection.Normalize();
            endDirection.Normalize();

            if (netManager.CreateSegment(out ushort clone, ref SimulationManager.instance.m_randomizer, state.Info.Prefab as NetInfo, treeInfo,
                startNodeId, endNodeId, startDirection, endDirection,
                SimulationManager.instance.m_currentBuildIndex, SimulationManager.instance.m_currentBuildIndex,
                state.invert))
            {
                SimulationManager.instance.m_currentBuildIndex++;

                cloneInstance = new MoveableSegment(new InstanceID() { NetSegment = clone });
            }

            return cloneInstance;
        }

        public override Instance Clone(InstanceState instanceState, Dictionary<ushort, ushort> clonedNodes)
        {
            SegmentState state = instanceState as SegmentState;

            Instance cloneInstance = null;

            ushort startNodeId = state.startNodeId;
            ushort endNodeId = state.endNodeId;

            // Nodes should exist
            startNodeId = clonedNodes[startNodeId];
            endNodeId = clonedNodes[endNodeId];

            if (netManager.CreateSegment(out ushort clone, ref SimulationManager.instance.m_randomizer, state.Info.Prefab as NetInfo,
                startNodeId, endNodeId, state.startDirection, state.endDirection,
                SimulationManager.instance.m_currentBuildIndex, SimulationManager.instance.m_currentBuildIndex,
                state.invert))
            {
                SimulationManager.instance.m_currentBuildIndex++;

                cloneInstance = new MoveableSegment(new InstanceID() { NetSegment = clone });
            }

            return cloneInstance;
        }

        public override void Delete()
        {
            if (isValid) NetManager.instance.ReleaseSegment(id.NetSegment, true);
        }

        public override Bounds GetBounds(bool ignoreSegments = true)
        {
            if (Virtual)
            {
                Bounds b = new Bounds(OverlayPosition, Vector3.one);
                b.Encapsulate(StartNode.GetBounds());
                b.Encapsulate(EndNode.GetBounds());
                return b;
            }

            return segmentBuffer[id.NetSegment].m_bounds;
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo, Color toolColor, Color despawnColor)
        {
            if (!isValid) return;
            if (MoveItTool.m_isLowSensitivity) return;
            toolColor = toolColor.Adjusted();

            ushort segment = id.NetSegment;
            NetInfo netInfo = segmentBuffer[segment].Info;

            ushort startNode = segmentBuffer[segment].m_startNode;
            ushort endNode = segmentBuffer[segment].m_endNode;

            bool smoothStart = (nodeBuffer[startNode].m_flags & NetNode.Flags.Middle) != NetNode.Flags.None;
            bool smoothEnd = (nodeBuffer[endNode].m_flags & NetNode.Flags.Middle) != NetNode.Flags.None;

            Bezier3 bezier;
            bezier.a = StartNode.OverlayPosition;
            bezier.d = EndNode.OverlayPosition;

            Vector3 startDirection = OverlayPosition - bezier.a;
            Vector3 endDirection = OverlayPosition - bezier.d;

            startDirection.y = 0;
            endDirection.y = 0;

            startDirection.Normalize();
            endDirection.Normalize();

            NetSegment.CalculateMiddlePoints(
                bezier.a, startDirection,
                bezier.d, endDirection,
                smoothStart, smoothEnd, out bezier.b, out bezier.c);

            RenderManager.instance.OverlayEffect.DrawBezier(cameraInfo, toolColor, bezier, netInfo.m_halfWidth * 4f / 3f, 100000f, -100000f, -1f, 1280f, false, true);

            Segment3 segment1, segment2;

            segment1.a = StartNode.OverlayPosition;
            segment2.a = EndNode.OverlayPosition;

            segment1.b = GetControlPoint(startDirection, endDirection);
            segment2.b = segment1.b;

            toolColor.a /= 2;

            RenderManager.instance.OverlayEffect.DrawSegment(cameraInfo, toolColor, segment1, segment2, 0, 10f, -1f, 1280f, false, true);
            RenderManager.instance.OverlayEffect.DrawCircle(cameraInfo, toolColor, segment1.b, netInfo.m_halfWidth / 2f, -1f, 1280f, false, true);
        }

        public override void RenderCloneOverlay(InstanceState instanceState, ref Matrix4x4 matrix4x, Vector3 deltaPosition, float deltaAngle, Vector3 center, bool followTerrain, RenderManager.CameraInfo cameraInfo, Color toolColor)
        {
            if (MoveItTool.m_isLowSensitivity) return;

            toolColor = toolColor.Adjusted();

            SegmentState state = instanceState as SegmentState;

            NetInfo netInfo = state.Info.Prefab as NetInfo;

            Vector3 newPosition = matrix4x.MultiplyPoint(state.position - center);
            newPosition.y = state.position.y + deltaPosition.y;

            if (followTerrain)
            {
                newPosition.y = newPosition.y - state.terrainHeight + TerrainManager.instance.SampleOriginalRawHeightSmooth(newPosition);
            }

            Bezier3 bezier;
            bezier.a = matrix4x.MultiplyPoint(state.startPosition - center);
            bezier.d = matrix4x.MultiplyPoint(state.endPosition - center);

            if (followTerrain)
            {
                bezier.a.y = bezier.a.y + TerrainManager.instance.SampleOriginalRawHeightSmooth(bezier.a) - TerrainManager.instance.SampleOriginalRawHeightSmooth(state.startPosition);
                bezier.d.y = bezier.d.y + TerrainManager.instance.SampleOriginalRawHeightSmooth(bezier.d) - TerrainManager.instance.SampleOriginalRawHeightSmooth(state.endPosition);
            }
            else
            {
                bezier.a.y = state.startPosition.y + deltaPosition.y;
                bezier.d.y = state.endPosition.y + deltaPosition.y;
            }

            Vector3 startDirection = newPosition - bezier.a;
            Vector3 endDirection = newPosition - bezier.d;

            startDirection.y = 0;
            endDirection.y = 0;

            startDirection.Normalize();
            endDirection.Normalize();

            NetSegment.CalculateMiddlePoints(
                bezier.a, startDirection,
                bezier.d, endDirection,
                state.smoothStart, state.smoothEnd, out bezier.b, out bezier.c);

            RenderManager.instance.OverlayEffect.DrawBezier(cameraInfo, toolColor, bezier, netInfo.m_halfWidth * 4f / 3f, 100000f, -100000f, -1f, 1280f, false, true);
        }

        public override void RenderCloneGeometry(InstanceState instanceState, ref Matrix4x4 matrix4x, Vector3 deltaPosition, float deltaAngle, Vector3 center, bool followTerrain, RenderManager.CameraInfo cameraInfo, Color toolColor)
        {
            SegmentState state = instanceState as SegmentState;

            NetInfo netInfo = state.Info.Prefab as NetInfo;

            Vector3 newPosition = matrix4x.MultiplyPoint(state.position - center);
            newPosition.y = state.position.y + deltaPosition.y;

            if (followTerrain)
            {
                newPosition.y = newPosition.y - state.terrainHeight + TerrainManager.instance.SampleOriginalRawHeightSmooth(newPosition);
            }

            Bezier3 bezier;
            bezier.a = matrix4x.MultiplyPoint(state.startPosition - center);
            bezier.d = matrix4x.MultiplyPoint(state.endPosition - center);

            if (followTerrain)
            {
                bezier.a.y = bezier.a.y + TerrainManager.instance.SampleOriginalRawHeightSmooth(bezier.a) - TerrainManager.instance.SampleOriginalRawHeightSmooth(state.startPosition);
                bezier.d.y = bezier.d.y + TerrainManager.instance.SampleOriginalRawHeightSmooth(bezier.d) - TerrainManager.instance.SampleOriginalRawHeightSmooth(state.endPosition);
            }
            else
            {
                bezier.a.y = state.startPosition.y + deltaPosition.y;
                bezier.d.y = state.endPosition.y + deltaPosition.y;
            }

            Vector3 startDirection = newPosition - bezier.a;
            Vector3 endDirection = newPosition - bezier.d;

            startDirection.y = 0;
            endDirection.y = 0;

            startDirection.Normalize();
            endDirection.Normalize();
            RenderSegment.Invoke(null, new object[] { netInfo, NetSegment.Flags.All, bezier.a, bezier.d, startDirection, -endDirection, state.smoothStart, state.smoothEnd });
        }

        public override void RenderGeometry(RenderManager.CameraInfo cameraInfo, Color toolColor)
        {
            if (!Virtual) return;

            ushort segment = id.NetSegment;
            ushort startNode = segmentBuffer[segment].m_startNode;
            ushort endNode = segmentBuffer[segment].m_endNode;

            bool smoothStart = (nodeBuffer[startNode].m_flags & NetNode.Flags.Middle) != NetNode.Flags.None;
            bool smoothEnd = (nodeBuffer[endNode].m_flags & NetNode.Flags.Middle) != NetNode.Flags.None;

            Bezier3 bezier;
            bezier.a = StartNode.OverlayPosition;
            bezier.d = EndNode.OverlayPosition;

            Vector3 startDirection = OverlayPosition - bezier.a;
            Vector3 endDirection = OverlayPosition - bezier.d;

            startDirection.y = 0;
            endDirection.y = 0;

            startDirection.Normalize();
            endDirection.Normalize();

            NetSegment.CalculateMiddlePoints(
                bezier.a, startDirection,
                bezier.d, endDirection,
                smoothStart, smoothEnd, out bezier.b, out bezier.c);

            // Flip the segment geometry unless the segment is one of inverted
            if ((segmentBuffer[segment].m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.Invert)
            {
                RenderSegment.Invoke(null, new object[] { Info.Prefab as NetInfo, NetSegment.Flags.All, bezier.a, bezier.d, startDirection, -endDirection, smoothStart, smoothEnd });
            }
            else
            {
                RenderSegment.Invoke(null, new object[] { Info.Prefab as NetInfo, NetSegment.Flags.All, bezier.d, bezier.a, endDirection, -startDirection, smoothStart, smoothEnd });
            }
        }

        private Vector3 GetControlPoint()
        {
            return GetControlPoint(segmentBuffer[id.NetSegment].m_startDirection, segmentBuffer[id.NetSegment].m_endDirection);
        }

        private Vector3 GetControlPoint(Vector3 startDir, Vector3 endDir)
        {
            if (Virtual)
            {
                return TransformPosition;
            }

            return GetControlPointReal(startDir, endDir);
        }

        private Vector3 GetControlPointReal()
        {
            return GetControlPointReal(segmentBuffer[id.NetSegment].m_startDirection, segmentBuffer[id.NetSegment].m_endDirection);
        }

        private Vector3 GetControlPointReal(Vector3 startDir, Vector3 endDir)
        { 
            Vector3 startPos = StartNode.OverlayPosition;
            Vector3 endPos = EndNode.OverlayPosition;

            if (!NetSegment.IsStraight(startPos, startDir, endPos, endDir, out float _))
            {
                float dot = startDir.x * endDir.x + startDir.z * endDir.z;
                if (dot >= -0.999f && Line2.Intersect(VectorUtils.XZ(startPos), VectorUtils.XZ(startPos + startDir), VectorUtils.XZ(endPos), VectorUtils.XZ(endPos + endDir), out float u, out float _))
                {
                    return startPos + startDir * u;
                }
                else
                {
                    Log.Info($"Invalid segment directions for {id.Debug()}!");
                    
                    startDir = EndNode.position - StartNode.position;
                    endDir = StartNode.position - EndNode.position;

                    startDir.y = 0;
                    endDir.y = 0;

                    startDir.Normalize();
                    endDir.Normalize();

                    segmentBuffer[id.NetSegment].m_startDirection = startDir;
                    segmentBuffer[id.NetSegment].m_endDirection = endDir;
                }
            }

            return (startPos + endPos) / 2f;
        }

        private MoveableNode GetNodeFromSelection(InstanceID instanceID)
        {
            foreach (Instance i in Action.selection)
            {
                if (i is MoveableNode mn)
                {
                    if (mn.id.NetNode == instanceID.NetNode)
                    {
                        return mn;
                    }
                }
            }

            return null;
        }

        private static readonly MethodInfo RenderSegment = typeof(NetTool).GetMethod("RenderSegment", BindingFlags.NonPublic | BindingFlags.Static);
    }
}
