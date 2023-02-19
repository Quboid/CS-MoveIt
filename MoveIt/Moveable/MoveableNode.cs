using ColossalFramework;
using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;


namespace MoveIt
{
    public class NodeState : InstanceState
    {
        public NetNode.Flags flags;
        public BuildingState pillarState;

        [XmlElement("segmentsSave")]
        public SegmentSave[] segmentsSave = new SegmentSave[8];

        [XmlElement("segmentsList")]
        public List<ushort> segmentsList = new List<ushort>();

        public struct SegmentSave
        {
            public Vector3 startDirection;
            public Vector3 endDirection;
        }

        public override void ReplaceInstance(Instance instance)
        {
            base.ReplaceInstance(instance);

            MoveableNode node = instance as MoveableNode;

            if (pillarState != null)
            {
                pillarState.instance = node.Pillar;
                if(pillarState.instance == null)
                {
                    pillarState = null;
                }
            }
        }

        /// <summary>
        /// Find the nearest existing node that can be merged into
        /// </summary>
        /// <param name="original">The original state, rather than this temporary one</param>
        /// <returns>The NodeMergeClone, or null if none found</returns>
        internal NodeMergeClone FindNearestNode(NodeState original)
        {
            NetNode[] nodeBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;
            int c = 0;
            float dist = NodeMerging.MAX_MERGE_DISTANCE;

            int num = Mathf.Clamp((int)(position.x / 64f + 135f), 0, 269);
            int num2 = Mathf.Clamp((int)(position.z / 64f + 135f), 0, 269);
            int num3 = num2 * 270 + num;
            ushort node = NetManager.instance.m_nodeGrid[num3];
            NodeMergeClone result = null;

            do
            {
                NodeMergeClone cloneData = new NodeMergeClone()
                {
                    nodeState = original,
                    adjustedState = this,
                    ParentId = node,
                    status = NodeMergeStatuses.Merge
                };

                if (cloneData.CanMerge())
                {
                    if (cloneData.Distance < dist)
                    {
                        result = cloneData;
                        dist = cloneData.Distance;
                    }
                }

                node = nodeBuffer[node].m_nextGridNode;
            }
            while (node != 0 && c++ < 99);

            return result;
        }
    }

    public class MoveableNode : Instance
    {
        public MoveableBuilding Pillar // Pillar, pylon, water junction
        {
            get
            {
                foreach (Instance sub in subInstances)
                {
                    if (sub is MoveableBuilding mb)
                    {
                        return mb;
                    }
                }
                return null;
            }
        }
        public ushort m_snapNode = 0;

        public override HashSet<ushort> segmentList
        {
            get
            {
                HashSet<ushort> segments = new HashSet<ushort>();

                for (int i = 0; i < 8; i++)
                {
                    ushort segment = nodeBuffer[id.NetNode].GetSegment(i);
                    if (segment != 0)
                    {
                        segments.Add(segment);
                    }
                }

                return segments;
            }
        }

        public MoveableNode(InstanceID instanceID) : base(instanceID)
        {
            Info = new Info_Prefab(NetManager.instance.m_nodes.m_buffer[instanceID.NetNode].Info);

            subInstances = GetSubInstances();
        }

        public override InstanceState SaveToState(bool integrate = true)
        {
            ushort node = id.NetNode;

            NodeState state = new NodeState
            {
                instance = this,
                Info = Info,
                position = nodeBuffer[node].m_position,
                flags = nodeBuffer[node].m_flags,
                isCustomContent = Info.Prefab.m_isCustomContent
            };
            state.terrainHeight = TerrainManager.instance.SampleOriginalRawHeightSmooth(state.position);

            if (Pillar != null)
            {
                state.pillarState = Pillar.SaveToState() as BuildingState;
            }

            for (int i = 0; i < 8; i++)
            {
                ushort segment = nodeBuffer[node].GetSegment(i);
                if (segment != 0)
                {
                    state.segmentsList.Add(segment);
                    state.segmentsSave[i].startDirection = segmentBuffer[segment].m_startDirection;
                    state.segmentsSave[i].endDirection = segmentBuffer[segment].m_endDirection;
                }
            }

            state.SaveIntegrations(integrate);

            return state;
        }

        public override void LoadFromState(InstanceState state)
        {
            if (!(state is NodeState nodeState)) return;

            ushort node = id.NetNode;

            netManager.MoveNode(node, nodeState.position);

            for (int i = 0; i < 8; i++)
            {
                ushort segment = nodeBuffer[node].GetSegment(i);
                if (segment != 0 && nodeState.segmentsList.Contains(segment))
                {
                    segmentBuffer[segment].m_startDirection = nodeState.segmentsSave[i].startDirection;
                    segmentBuffer[segment].m_endDirection = nodeState.segmentsSave[i].endDirection;

                    UpdateSegmentBlocks(segment, ref segmentBuffer[segment]);

                    netManager.UpdateNode(segmentBuffer[segment].m_startNode);
                    netManager.UpdateNode(segmentBuffer[segment].m_endNode);
                }
            }

            if (nodeState.pillarState != null)
            {
                nodeState.pillarState.instance.LoadFromState(nodeState.pillarState);
            }
        }

        public override Vector3 position
        {
            get
            {
                if (id.IsEmpty) return Vector3.zero;
                return nodeBuffer[id.NetNode].m_position;
            }
            set
            {
                if (id.IsEmpty) nodeBuffer[id.NetNode].m_position = Vector3.zero;
                else nodeBuffer[id.NetNode].m_position = value;
            }
        }

        public override float angle
        {
            get { return 0f; }
            set { }
        }

        public override bool isValid
        {
            get
            {
                if (id.IsEmpty) return false;
                return (nodeBuffer[id.NetNode].m_flags & NetNode.Flags.Created) != NetNode.Flags.None;
            }
        }

        public override void Transform(InstanceState instanceState, ref Matrix4x4 matrix4x, float deltaHeight, float deltaAngle, Vector3 center, bool followTerrain)
        {
            NodeState state = instanceState as NodeState;

            Vector3 newPosition = matrix4x.MultiplyPoint(state.position - center);
            newPosition.y = state.position.y + deltaHeight;

            if (followTerrain)
            {
                newPosition.y = newPosition.y + TerrainManager.instance.SampleOriginalRawHeightSmooth(newPosition) - state.terrainHeight;
            }

            Move(newPosition, 0);

            if (state.pillarState != null)
            {
                //ushort pillarId = state.pillarState.instance.id.Building;
                //ref Building buildingData = ref BuildingManager.instance.m_buildings.m_buffer[pillarId];
                //if ((buildingData.m_flags & Building.Flags.Hidden) != Building.Flags.Hidden)
                //{
                //    buildingData.m_flags |= Building.Flags.Hidden;
                //    state.pillarState.instance.isHidden = true;
                //}

                Vector3 subPosition = state.pillarState.position - center;
                subPosition = matrix4x.MultiplyPoint(subPosition);
                subPosition.y = state.pillarState.position.y - state.position.y + newPosition.y;

                state.pillarState.instance.Move(subPosition, state.pillarState.angle + deltaAngle);
            }
        }

        public List<Instance> GetSubInstances()
        {
            List<Instance> instances = new List<Instance>();
            ushort building = nodeBuffer[id.NetNode].m_building;
            int count = 0;
            while (building != 0)
            {
                InstanceID buildingID = default;
                buildingID.Building = building;

                instances.Add(new MoveableBuilding(buildingID));
                building = buildingBuffer[building].m_subBuilding;

                if (++count > 49152)
                {
                    CODebugBase<LogChannel>.Error(LogChannel.Core, "Buildings: Invalid list detected!\n" + Environment.StackTrace);
                    break;
                }
            }

            ushort node = buildingBuffer[id.Building].m_netNode;
            count = 0;
            while (node != 0)
            {
                ItemClass.Layer layer = nodeBuffer[node].Info.m_class.m_layer;
                if (layer != ItemClass.Layer.PublicTransport)
                {
                    InstanceID nodeID = default;
                    nodeID.NetNode = node;
                    instances.Add(new MoveableNode(nodeID));
                }

                node = nodeBuffer[node].m_nextBuildingNode;
                if ((nodeBuffer[node].m_flags & NetNode.Flags.Created) != NetNode.Flags.Created)
                {
                    node = 0;
                }

                if (++count > 32768)
                {
                    CODebugBase<LogChannel>.Error(LogChannel.Core, "Nodes: Invalid list detected!\n" + Environment.StackTrace);
                    break;
                }
            }

            return instances;
        }

        public override void Move(Vector3 location, float angle)
        {
            //Singleton<SimulationManager>.instance.AddAction(() => MoveProcess(location, angle));
            MoveProcess(location, angle);
        }

        internal static void UpdateSegments(ushort node, Vector3 oldPosition)
        {
            for (int i = 0; i < 8; i++)
            {
                ushort segment = nodeBuffer[node].GetSegment(i);
                if (segment != 0 && !Action.IsSegmentSelected(segment))
                {
                    ushort startNode = segmentBuffer[segment].m_startNode;
                    ushort endNode = segmentBuffer[segment].m_endNode;

                    Vector3 oldVector;
                    if (node == endNode)
                    {
                        oldVector = oldPosition - nodeBuffer[startNode].m_position;
                    }
                    else
                    {
                        oldVector = nodeBuffer[endNode].m_position - oldPosition;
                    }
                    oldVector.Normalize();

                    Vector3 startDirection = new Vector3(segmentBuffer[segment].m_startDirection.x, 0, segmentBuffer[segment].m_startDirection.z);
                    Vector3 endDirection = new Vector3(segmentBuffer[segment].m_endDirection.x, 0, segmentBuffer[segment].m_endDirection.z);

                    Quaternion startRotation = Quaternion.FromToRotation(oldVector, startDirection.normalized);
                    Quaternion endRotation = Quaternion.FromToRotation(-oldVector, endDirection.normalized);

                    Vector3 newVector = nodeBuffer[endNode].m_position - nodeBuffer[startNode].m_position;
                    newVector.Normalize();

                    segmentBuffer[segment].m_startDirection = startRotation * newVector;
                    segmentBuffer[segment].m_endDirection = endRotation * -newVector;

                    CalculateSegmentDirections(ref segmentBuffer[segment], segment);

                    netManager.UpdateSegmentRenderer(segment, true);
                    UpdateSegmentBlocks(segment, ref segmentBuffer[segment]);

                    if (node != startNode)
                    {
                        netManager.UpdateNode(startNode);
                    }
                    else
                    {
                        netManager.UpdateNode(endNode);
                    }
                }
            }

            netManager.UpdateNode(node);
        }

        internal void MoveProcess(Vector3 location, float angle)
        { 
            if (!isValid) return;

            TransformAngle = angle;
            TransformPosition = location;

            if (!isVirtual())
            {
                ushort node = id.NetNode;
                Vector3 oldPosition = nodeBuffer[node].m_position;

                netManager.MoveNode(node, location);

                UpdateSegments(node, oldPosition);

                //for (int i = 0; i < 8; i++)
                //{
                //    ushort segment = nodeBuffer[node].GetSegment(i);
                //    if (segment != 0 && !Action.IsSegmentSelected(segment))
                //    {
                //        ushort startNode = segmentBuffer[segment].m_startNode;
                //        ushort endNode = segmentBuffer[segment].m_endNode;

                //        Vector3 oldVector;
                //        if (node == endNode)
                //        {
                //            oldVector = oldPosition - nodeBuffer[startNode].m_position;
                //        }
                //        else
                //        {
                //            oldVector = nodeBuffer[endNode].m_position - oldPosition;
                //        }
                //        oldVector.Normalize();

                //        Vector3 startDirection = new Vector3(segmentBuffer[segment].m_startDirection.x, 0, segmentBuffer[segment].m_startDirection.z);
                //        Vector3 endDirection = new Vector3(segmentBuffer[segment].m_endDirection.x, 0, segmentBuffer[segment].m_endDirection.z);

                //        Quaternion startRotation = Quaternion.FromToRotation(oldVector, startDirection.normalized);
                //        Quaternion endRotation = Quaternion.FromToRotation(-oldVector, endDirection.normalized);

                //        Vector3 newVector = nodeBuffer[endNode].m_position - nodeBuffer[startNode].m_position;
                //        newVector.Normalize();

                //        segmentBuffer[segment].m_startDirection = startRotation * newVector;
                //        segmentBuffer[segment].m_endDirection = endRotation * -newVector;

                //        CalculateSegmentDirections(ref segmentBuffer[segment], segment);

                //        netManager.UpdateSegmentRenderer(segment, true);
                //        UpdateSegmentBlocks(segment, ref segmentBuffer[segment]);

                //        if (node != startNode)
                //        {
                //            netManager.UpdateNode(startNode);
                //        }
                //        else
                //        {
                //            netManager.UpdateNode(endNode);
                //        }
                //    }
                //}

                //netManager.UpdateNode(node);
            }
        }

        public void AutoCurve(NetSegment segmentCurve)
        {
            ushort node = id.NetNode;

            if (segmentCurve.m_startNode != 0 && segmentCurve.m_endNode != 0)
            {
                segmentCurve.GetClosestPositionAndDirection(position, out _, out Vector3 tangent);

                for (int i = 0; i < 8; i++)
                {
                    // Start node
                    ushort segment = nodeBuffer[segmentCurve.m_startNode].GetSegment(i);

                    if (segment != 0)
                    {
                        ushort startNode = segmentBuffer[segment].m_startNode;
                        ushort endNode = segmentBuffer[segment].m_endNode;

                        if (startNode == node)
                        {
                            segmentBuffer[segment].m_startDirection = -tangent;
                            segmentBuffer[segment].m_endDirection = segmentCurve.m_startDirection;

                            CalculateSegmentDirections(ref segmentBuffer[segment], segment);
                            netManager.UpdateSegmentRenderer(segment, true);
                            UpdateSegmentBlocks(segment, ref segmentBuffer[segment]);

                            netManager.UpdateNode(endNode);
                        }
                        else if (endNode == node)
                        {
                            segmentBuffer[segment].m_startDirection = segmentCurve.m_startDirection;
                            segmentBuffer[segment].m_endDirection = -tangent;

                            CalculateSegmentDirections(ref segmentBuffer[segment], segment);
                            netManager.UpdateSegmentRenderer(segment, true);
                            UpdateSegmentBlocks(segment, ref segmentBuffer[segment]);

                            netManager.UpdateNode(startNode);
                        }
                    }

                    // End node
                    segment = nodeBuffer[segmentCurve.m_endNode].GetSegment(i);

                    if (segment != 0)
                    {
                        ushort startNode = segmentBuffer[segment].m_startNode;
                        ushort endNode = segmentBuffer[segment].m_endNode;

                        if (startNode == node)
                        {
                            segmentBuffer[segment].m_startDirection = tangent;
                            segmentBuffer[segment].m_endDirection = segmentCurve.m_endDirection;

                            CalculateSegmentDirections(ref segmentBuffer[segment], segment);
                            netManager.UpdateSegmentRenderer(segment, true);
                            UpdateSegmentBlocks(segment, ref segmentBuffer[segment]);

                            netManager.UpdateNode(endNode);
                        }
                        else if (endNode == node)
                        {
                            segmentBuffer[segment].m_startDirection = segmentCurve.m_endDirection;
                            segmentBuffer[segment].m_endDirection = tangent;

                            CalculateSegmentDirections(ref segmentBuffer[segment], segment);
                            netManager.UpdateSegmentRenderer(segment, true);
                            UpdateSegmentBlocks(segment, ref segmentBuffer[segment]);

                            netManager.UpdateNode(startNode);
                        }
                    }
                }
            }

            netManager.UpdateNode(node);
        }

        public override void SetHeight(float height)
        {
            Vector3 newPosition = position;

            MoveableBuilding nodePillar = Pillar;
            if (nodePillar != null)
            {
                Vector3 subPosition = nodePillar.position;
                subPosition.y = subPosition.y - newPosition.y + height;

                nodePillar.Move(subPosition, nodePillar.angle);
            }

            newPosition.y = height;
            Move(newPosition, angle);
        }
        
        public override void SetHeight()
        {
            SetHeight(TerrainManager.instance.SampleRawHeightSmooth(position));
        }

        public override Instance Clone(InstanceState instanceState, ref Matrix4x4 matrix4x, float deltaHeight, float deltaAngle, Vector3 center, bool followTerrain, Dictionary<ushort, ushort> clonedNodes, Action action)
        {
            NodeState state = instanceState as NodeState;

            Vector3 newPosition = matrix4x.MultiplyPoint(state.position - center);
            newPosition.y = state.position.y + deltaHeight;

            if (followTerrain)
            {
                newPosition.y = newPosition.y + TerrainManager.instance.SampleOriginalRawHeightSmooth(newPosition) - state.terrainHeight;
            }

            Instance cloneInstance = null;

            if (NetManager.instance.CreateNode(out ushort clone, ref SimulationManager.instance.m_randomizer, state.Info.Prefab as NetInfo,
                newPosition, SimulationManager.instance.m_currentBuildIndex))
            {
                SimulationManager.instance.m_currentBuildIndex++;

                InstanceID cloneID = default;
                cloneID.NetNode = clone;

                nodeBuffer[clone].m_flags = state.flags;

                nodeBuffer[clone].CalculateNode(clone);
                nodeBuffer[clone].Info.m_netAI.GetNodeBuilding(clone, ref nodeBuffer[clone], out BuildingInfo newBuilding, out float heightOffset);
                nodeBuffer[clone].UpdateBuilding(clone, newBuilding, heightOffset);

                cloneInstance = new MoveableNode(cloneID);

                if (((NetNode)data).m_building > 0)
                {
                    buildingBuffer[((NetNode)(cloneInstance.data)).m_building].m_flags = buildingBuffer[((NetNode)data).m_building].m_flags;
                }
            }

            return cloneInstance;
        }

        public override Instance Clone(InstanceState instanceState, Dictionary<ushort, ushort> clonedNodes)
        {
            NodeState state = instanceState as NodeState;

            MoveableNode cloneInstance = null;

            if (NetManager.instance.CreateNode(out ushort clone, ref SimulationManager.instance.m_randomizer, state.Info.Prefab as NetInfo,
                state.position, SimulationManager.instance.m_currentBuildIndex))
            {
                SimulationManager.instance.m_currentBuildIndex++;

                InstanceID cloneID = default;
                cloneID.NetNode = clone;
                cloneInstance = new MoveableNode(cloneID);

                nodeBuffer[clone].m_flags = state.flags;

                // TODO: Clone pillar instead?
                nodeBuffer[clone].Info.m_netAI.GetNodeBuilding(clone, ref nodeBuffer[clone], out BuildingInfo newBuilding, out float heightOffset);
                nodeBuffer[clone].UpdateBuilding(clone, newBuilding, heightOffset);
            }

            return cloneInstance;
        }

        public override void Delete()
        {
            if (isValid) NetManager.instance.ReleaseNode(id.NetNode);
        }

        public override Bounds GetBounds(bool ignoreSegments = true)
        {
            ushort node = id.NetNode;

            // In asset editor, second loads can cause null error
            if (nodeBuffer[node].Info == null)
            {
                return new Bounds();
            }

            if (Virtual)
            {
                return new Bounds(OverlayPosition, new Vector3(nodeBuffer[node].Info.m_halfWidth, 0, nodeBuffer[node].Info.m_halfWidth));
            }
            Bounds bounds = SanitizeBounds(node);

            if (nodeBuffer[node].Info.m_netAI is WaterPipeAI)
            {
                ignoreSegments = true;
            }

            if (!ignoreSegments)
            {
                for (int i = 0; i < 8; i++)
                {
                    ushort segment = nodeBuffer[node].GetSegment(i);
                    if (segment != 0)
                    {
                        ushort startNode = segmentBuffer[segment].m_startNode;
                        ushort endNode = segmentBuffer[segment].m_endNode;

                        if (node != startNode)
                        {
                            bounds.Encapsulate(SanitizeBounds(startNode));
                        }
                        else
                        {
                            bounds.Encapsulate(SanitizeBounds(endNode));
                        }
                    }
                }
            }

            return bounds;
        }

        private Bounds SanitizeBounds(ushort id)
        {
            NetNode node = nodeBuffer[id];
            Bounds bounds = node.m_bounds;
            Vector3 AbsCenter = new Vector3(Math.Abs(bounds.center.x), Math.Abs(bounds.center.y), Math.Abs(bounds.center.z));

            if (AbsCenter == bounds.extents || bounds.center == Vector3.zero)
            {
                node.m_bounds = new Bounds(node.m_position, new Vector3(16f, 0f, 16f));
            }
            node.m_bounds.center = node.m_position;

            return node.m_bounds;
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo, Color toolColor, Color despawnColor)
        {
            if (!isValid) return;
            if (!isVirtual() && MoveItTool.m_isLowSensitivity) return;
            toolColor = toolColor.Adjusted();

            ushort node = id.NetNode;
            NetInfo netInfo = nodeBuffer[node].Info;
            float alpha = 1f;
            NetTool.CheckOverlayAlpha(netInfo, ref alpha);
            toolColor.a *= alpha;
            RenderManager.instance.OverlayEffect.DrawCircle(cameraInfo, toolColor, OverlayPosition, Mathf.Max(6f, netInfo.m_halfWidth * 2f), -1f, 1280f, false, true);
        }

        public override void RenderCloneOverlay(InstanceState state, ref Matrix4x4 matrix4x, Vector3 deltaPosition, float deltaAngle, Vector3 center, bool followTerrain, RenderManager.CameraInfo cameraInfo, Color toolColor)
        {
            if (ActionQueue.instance.current is CloneActionBase action)
            {
                NodeMergeClone data = NodeMergeBase.Get(action.m_nodeMergeData, (NodeState)state);
                if (data != null)
                {
                    Vector3 newPosition = matrix4x.MultiplyPoint(state.position - center);
                    newPosition.y = state.position.y + deltaPosition.y;

                    if (followTerrain)
                    {
                        newPosition.y = newPosition.y - state.terrainHeight + TerrainManager.instance.SampleOriginalRawHeightSmooth(newPosition);
                    }

                    NetNode parent = data.ParentNetNode;

                    if (!MoveItTool.m_isLowSensitivity)
                    {
                        if (data == action.m_snapNode)
                        {
                            NetInfo netInfo = (NetInfo)state.Info.Prefab;
                            RenderManager.instance.OverlayEffect.DrawCircle(cameraInfo, toolColor.Adjusted(), newPosition, Mathf.Max(6f, netInfo.m_halfWidth * 2f), -1f, 1280f, false, true);
                        }

                        RenderManager.instance.OverlayEffect.DrawCircle(cameraInfo, toolColor.Adjusted(), parent.m_position, Mathf.Max(6f, parent.Info.m_halfWidth * 2f), -1f, 1280f, false, true);
                    }

                    Bezier3 bezier;
                    bezier.a = newPosition;
                    bezier.d = parent.m_position;

                    NetSegment.CalculateMiddlePoints(bezier.a, (newPosition - parent.m_position).normalized, bezier.d, (parent.m_position - newPosition).normalized, true, true, out bezier.b, out bezier.c);
                    RenderManager.instance.OverlayEffect.DrawBezier(cameraInfo, toolColor, bezier, 1f, 100000f, -100000f, -1f, 1280f, false, true);
                }
            }
        }

        public override void RenderCloneGeometry(InstanceState state, ref Matrix4x4 matrix4x, Vector3 deltaPosition, float deltaAngle, Vector3 center, bool followTerrain, RenderManager.CameraInfo cameraInfo, Color toolColor) { }
        
        public override void RenderGeometry(RenderManager.CameraInfo cameraInfo, Color toolColor)
        {
            foreach (Instance subInstance in subInstances)
            {
                if (subInstance is MoveableBuilding msb)
                {
                    msb.RenderGeometry(cameraInfo, toolColor);
                }
            }
        }
    }
}
