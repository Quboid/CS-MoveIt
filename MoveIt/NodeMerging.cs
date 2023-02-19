using ColossalFramework;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.RectTransform;

namespace MoveIt
{
    internal class NodeMerging
    {
        internal const float MAX_MERGE_DISTANCE = 3f;
        internal const float MAX_SNAP_DISTANCE = 8f;

        /// <summary>
        /// Check if two nodes can be merged and if possible, merge and delete child
        /// </summary>
        /// <param name="data">The NodeMergeBase</param>
        /// <param name="tolerance">The max positional distance to be considered overlapping</param>
        /// <returns></returns>
        internal static bool MergeNodes(NodeMergeExisting data)
        {
            if (!data.CanMerge()) return false;
            return DoMerge(data);
        }

        /// <summary>
        /// Get the distance between a node and a vector
        /// </summary>
        /// <param name="a">Node A</param>
        /// <param name="b">Position</param>
        /// <returns>Distance in metres</returns>
        internal static float GetNodeDistance(NetNode a, Vector3 b)
        {
            return (a.m_position - b).magnitude;
        }

        /// <summary>
        /// Combine two nodes into one
        /// </summary>
        /// <param name="data">The NodeMergeData</param>
        /// <returns>True if successful (child is deleted), false if failed and child remains</returns>
        private static bool DoMerge(NodeMergeExisting data)
        {
            ref NetNode parent = ref data.ParentNetNode;
            ref NetNode child = ref data.ChildNetNode;

            List<ushort> segments = new List<ushort>();
            for (int i = 0; i < 8; i++)
            {
                if (parent.GetSegment(i) > 0) segments.Add(parent.GetSegment(i));
                if (child.GetSegment(i) > 0) segments.Add(child.GetSegment(i));
            }

            if (segments.Count < 2 || segments.Count > 8) return false;

            parent.m_segment0 = segments[0];
            parent.m_segment1 = segments[1];
            if (segments.Count > 2) parent.m_segment2 = segments[2]; else parent.m_segment2 = 0;
            if (segments.Count > 3) parent.m_segment3 = segments[3]; else parent.m_segment3 = 0;
            if (segments.Count > 4) parent.m_segment4 = segments[4]; else parent.m_segment4 = 0;
            if (segments.Count > 5) parent.m_segment5 = segments[5]; else parent.m_segment5 = 0;
            if (segments.Count > 6) parent.m_segment6 = segments[6]; else parent.m_segment6 = 0;
            if (segments.Count > 7) parent.m_segment7 = segments[7]; else parent.m_segment7 = 0;

            for (int i = 0; i < 8; i++)
            {
                if (child.GetSegment(i) > 0) SwitchSegmentNode(child.GetSegment(i), data.ChildInstanceId, data.ParentInstanceId);
            }
            parent.CalculateNode(data.ParentId);

            if (child.m_segment0 > 0) { child.m_segment0 = 0; }
            if (child.m_segment1 > 0) { child.m_segment1 = 0; }
            if (child.m_segment2 > 0) { child.m_segment2 = 0; }
            if (child.m_segment3 > 0) { child.m_segment3 = 0; }
            if (child.m_segment4 > 0) { child.m_segment4 = 0; }
            if (child.m_segment5 > 0) { child.m_segment5 = 0; }
            if (child.m_segment6 > 0) { child.m_segment6 = 0; }
            if (child.m_segment7 > 0) { child.m_segment7 = 0; }

            MoveableNode childNode = new MoveableNode(data.ChildInstanceId);
            childNode.Delete();

            Log.Info($"Merged node {data.ChildId} into {data.ParentId} ({segments.Count} segments)", "[M61]");

            return true;
        }

        private static void SwitchSegmentNode(ushort segmentId, InstanceID fromId, InstanceID toId)
        {
            NetSegment[] segmentBuffer = Singleton<NetManager>.instance.m_segments.m_buffer;

            ref NetSegment segment = ref segmentBuffer[segmentId];
            if (segment.m_startNode == fromId.NetNode)
                segment.m_startNode = toId.NetNode;
            else if (segment.m_endNode == fromId.NetNode)
                segment.m_endNode = toId.NetNode;
            else
                Log.Info($"Node not found for segment #{segmentId} (switching {fromId} to {toId})", "[M62]");
            //Log.Debug($"BBB06 Node switch segment #{segmentId} (switching {fromId.NetNode} to {toId.NetNode}) - {segmentBuffer[segmentId].m_startNode},{segmentBuffer[segmentId].m_endNode}", "[M63]");
        }
    }


    internal abstract class NodeMergeBase
    {
        internal NodeMergeStatuses status;

        internal abstract float Distance { get; }
        internal abstract bool CanMerge();

        /// <summary>
        /// The new node that is merging, may or may not exist (ushort)
        /// </summary>
        internal abstract ushort ChildId { get; set; }
        /// <summary>
        /// The new node that is merging, may or may not exist (InstanceID)
        /// </summary>
        internal abstract InstanceID ChildInstanceId { get; }

        private ushort _parentId;
        /// <summary>
        /// The existing node that is being merged into (ushort)
        /// </summary>
        internal ushort ParentId { get => _parentId; set => _parentId = value; }
        /// <summary>
        /// The existing node that is being merged into (InstanceID)
        /// </summary>
        internal InstanceID ParentInstanceId
        {
            get
            {
                InstanceID id = default;
                id.NetNode = ParentId;
                return id;
            }
        }

        /// <summary>
        /// The new node that is merging, may or may not exist (NetNode)
        /// </summary>
        internal ref NetNode ChildNetNode => ref Singleton<NetManager>.instance.m_nodes.m_buffer[ChildId];
        /// <summary>
        /// The existing node that is being merged into (NetNode)
        /// </summary>
        internal ref NetNode ParentNetNode => ref Singleton<NetManager>.instance.m_nodes.m_buffer[ParentId];

        public override string ToString()
        {
            return $"{ChildId}:{ParentId}={Distance}";
        }


        internal static NodeMergeClone Get(List<NodeMergeClone> list, NodeState state)
        {
            foreach (NodeMergeClone data in list)
            {
                if (data.nodeState == state && (data.status == NodeMergeStatuses.Snap || data.status == NodeMergeStatuses.Merge)) return data;
            }
            return null;
        }

        internal static bool CanMerge(List<NodeMergeClone> list, NodeState state)
        {
            return Get(list, state) != null;
        }

        internal static NodeMergeClone GetSnap(List<NodeMergeClone> nodes)
        {
            foreach (NodeMergeClone data in nodes)
            {
                if (data.status == NodeMergeStatuses.Snap) return data;
            }
            return null;
        }

        internal static bool CanMergeNodes(ushort parentId, NodeState state)
        {
            return CanMergeNodes(Singleton<NetManager>.instance.m_nodes.m_buffer[parentId], (NetInfo)state.Info.Prefab);
        }

        internal static bool CanMergeNodes(NetNode parent, NetInfo childInfo)
        {
            if (!((parent.m_flags & NetNode.Flags.Created) == NetNode.Flags.Created)) return false;
            if (!(parent.Info.m_class.m_service == childInfo.m_class.m_service && parent.Info.m_class.m_subService == childInfo.m_class.m_subService)) return false;

            return true;
        }
    }

    internal class NodeMergeClone : NodeMergeBase
    {
        internal NodeState nodeState;
        internal NodeState adjustedState;

        internal override ushort ChildId { get => ChildInstanceId.NetNode; set { ; } }
        internal override InstanceID ChildInstanceId => nodeState.instance.id;

        private float _distance = -1;
        internal override float Distance
        {
            get
            {
                if (_distance == -1)
                {
                    _distance = NodeMerging.GetNodeDistance(ParentNetNode, adjustedState.position);
                }
                return _distance;
            }
        }

        internal override bool CanMerge()
        {
            if (!CanMergeNodes(ParentId, adjustedState)) return false;

            int segCount = 0;
            for (int i = 0; i < 8; i++)
            {
                if (ParentNetNode.GetSegment(i) > 0) segCount++;
                if (ChildNetNode.GetSegment(i) > 0) segCount++;
            }
            if (segCount > 8) return false;

            return NodeMerging.GetNodeDistance(ParentNetNode, adjustedState.position) < NodeMerging.MAX_MERGE_DISTANCE;
        }

        internal NodeMergeExisting ConvertToExisting(ushort newId)
        {
            return new NodeMergeExisting()
            {
                ChildId = newId,
                ParentId = ParentId
            };
        }

        internal NodeMergeExisting ConvertToExisting(CloneData cloneData)
        {
            return new NodeMergeExisting()
            {
                ChildId = cloneData.CloneIId.NetNode,
                ParentId = ParentId
            };
        }
    }

    internal class NodeMergeExisting : NodeMergeBase
    {
        private ushort _childId;
        internal override ushort ChildId { get => _childId; set => _childId = value; }
        internal override InstanceID ChildInstanceId
        {
            get
            {
                InstanceID id = default;
                id.NetNode = ChildId;
                return id;
            }
        }

        private float _distance = -1;
        internal override float Distance
        {
            get
            {
                if (_distance == -1)
                {
                    _distance = NodeMerging.GetNodeDistance(ParentNetNode, ChildNetNode.m_position);
                }
                return _distance;
            }
        }

        internal override bool CanMerge()
        {
            if (!CanMergeNodes(ParentNetNode, ChildNetNode.Info)) return false;

            int segCount = 0;
            for (int i = 0; i < 8; i++)
            {
                if (ParentNetNode.GetSegment(i) > 0) segCount++;
                if (ChildNetNode.GetSegment(i) > 0) segCount++;
            }
            if (segCount > 8) return false;

            return true;
        }
    }

    internal enum NodeMergeStatuses
    {
        None,
        Merge,
        Snap,
        Invalid
    }
}
