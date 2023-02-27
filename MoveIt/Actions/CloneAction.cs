using ColossalFramework;
using QCommonLib.QTasks;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoveIt
{
    public class CloneActionImport : CloneActionBase
    {
        // Constructor for imported selections
        public CloneActionImport(InstanceState[] states, Vector3 centerPoint)
        {
            bool includesPO = false;

            m_oldSelection = selection;
            m_states.Clear();

            foreach (InstanceState state in states)
            {
                if (state.instance != null && state.Info.Prefab != null)
                {
                    if (state is ProcState)
                    {
                        continue;
                    }

                    m_states.Add(state);
                }
            }

            if (includesPO && !MoveItTool.PO.Active)
            {
                MoveItTool.PO.InitialiseTool(true);
            }

            center = centerPoint;
        }
    }

    public class CloneActionFindIt : CloneActionBase
    {
        // Constructor for FindIt object
        public CloneActionFindIt(PrefabInfo prefab)
        {
            m_oldSelection = selection;
            m_states.Clear();

            Vector3 position = MoveItTool.RaycastMouseLocation();
            InstanceState state = new InstanceState();

            if (prefab is BuildingInfo)
            {
                state = new BuildingState
                {
                    isSubInstance = false,
                    isHidden = false,
                    flags = Building.Flags.Completed
                };
                state.Info.Prefab = prefab;
                InstanceID id = new InstanceID
                {
                    Building = 1,
                    Type = InstanceType.Building
                };
                state.instance = new MoveableBuilding(id);
            }
            else if (prefab is PropInfo)
            {
                state = new PropState
                {
                    fixedHeight = false,
                    single = false,
                };
                state.Info.Prefab = prefab;
                InstanceID id = new InstanceID
                {
                    Prop = 1,
                    Type = InstanceType.Prop
                };
                state.instance = new MoveableProp(id);
            }
            else if (prefab is TreeInfo)
            {
                state = new TreeState
                {
                    fixedHeight = false,
                    single = false,
                };
                state.Info.Prefab = prefab;
                InstanceID id = new InstanceID
                {
                    Tree = 1,
                    Type = InstanceType.Tree
                };
                state.instance = new MoveableTree(id);
            }

            state.position = position;
            state.terrainHeight = position.y;
            m_states.Add(state);
            center = position;
        }
    }

    public class CloneAction : CloneActionMain
    {
        public CloneAction() : base() { }
    }

    public class DuplicateAction : CloneActionMain
    {
        public DuplicateAction() : base()
        {
            angleDelta = 0f;
            moveDelta = Vector3.zero;
        }
    }

    public class CloneActionMain : CloneActionBase
    {
        public CloneActionMain() : base()
        {
            m_oldSelection = selection;

            HashSet<Instance> newSelection = GetCleanSelection(out center);
            if (newSelection.Count == 0) return;

            // Save states
            foreach (Instance instance in newSelection)
            {
                if (instance.isValid)
                {
                    m_states.Add(instance.SaveToState());
                }
            }
        }
    }

    public class CloneActionBase : Action
    {
        public Vector3 moveDelta;
        public Vector3 center;
        public float angleDelta;
        public bool followTerrain;

        internal NodeMergeClone m_snapNode = null;
        internal List<NodeMergeClone> m_nodeMergeData = new List<NodeMergeClone>();

        public HashSet<InstanceState> m_states = new HashSet<InstanceState>(); // the InstanceStates to be cloned
        internal HashSet<Instance> m_oldSelection; // The selection before cloning

        /// <summary>
        /// Maps of clones
        /// </summary>
        internal List<CloneData> m_cloneData = new List<CloneData>();
        internal Dictionary<InstanceID, InstanceID> m_mapLanes;

        /// <summary>
        /// Map of node clones, Original -> Clone to connect cloned segments
        /// </summary>
        internal Dictionary<ushort, ushort> m_mapNodes;
        internal Dictionary<PO_Group, PO_Group> m_POGroupMap = new Dictionary<PO_Group, PO_Group>();

        protected Matrix4x4 matrix4x = default;

        public static HashSet<Instance> GetCleanSelection(out Vector3 center)
        {
            HashSet<Instance> newSelection = new HashSet<Instance>(selection);

            InstanceID id = new InstanceID();

            // Adding missing nodes
            foreach (Instance instance in selection)
            {
                if (instance is MoveableSegment)
                {
                    ushort segment = instance.id.NetSegment;

                    id.NetNode = segmentBuffer[segment].m_startNode;
                    newSelection.Add(id);

                    id.NetNode = segmentBuffer[segment].m_endNode;
                    newSelection.Add(id);
                }
            }

            // Adding missing segments
            foreach (Instance instance in selection)
            {
                if (instance.id.Type == InstanceType.NetNode)
                {
                    ushort node = instance.id.NetNode;
                    for (int i = 0; i < 8; i++)
                    {
                        ushort segment = nodeBuffer[node].GetSegment(i);
                        id.NetSegment = segment;

                        if (segment != 0 && !newSelection.Contains(id))
                        {
                            ushort startNode = segmentBuffer[segment].m_startNode;
                            ushort endNode = segmentBuffer[segment].m_endNode;

                            if (node == startNode)
                            {
                                id.NetNode = endNode;
                            }
                            else
                            {
                                id.NetNode = startNode;
                            }

                            if (newSelection.Contains(id))
                            {
                                id.NetSegment = segment;
                                newSelection.Add(id);
                            }
                        }
                    }
                }
            }

            // Remove single nodes
            HashSet<Instance> toRemove = new HashSet<Instance>();
            foreach (Instance instance in newSelection)
            {
                if (instance.id.Type == InstanceType.NetNode)
                {
                    bool found = false;
                    ushort node = instance.id.NetNode;

                    for (int i = 0; i < 8; i++)
                    {
                        ushort segment = nodeBuffer[node].GetSegment(i);
                        id.NetSegment = segment;

                        if (segment != 0 && newSelection.Contains(id))
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        toRemove.Add(instance);
                    }
                }
            }
            newSelection.ExceptWith(toRemove);

            if (newSelection.Count > 0)
            {
                // Calculate center
                Bounds totalBounds = default;
                bool init = false;

                foreach (Instance instance in newSelection)
                {
                    if (init)
                    {
                        totalBounds.Encapsulate(instance.GetBounds());
                    }
                    else
                    {
                        totalBounds = instance.GetBounds();
                        init = true;
                    }
                }

                center = totalBounds.center;
            }
            else
            {
                center = Vector3.zero;
            }

            // Sort segments by buildIndex
            HashSet<Instance> sorted = new HashSet<Instance>();
            List<uint> indexes = new List<uint>();
            foreach (Instance instance in newSelection)
            {
                if (instance.id.Type != InstanceType.NetSegment)
                {
                    sorted.Add(instance);
                }
                else
                {
                    uint bi = ((NetSegment)instance.data).m_buildIndex;
                    if (!indexes.Contains(bi))
                       indexes.Add(bi);
                }
            }

            indexes.Sort();

            foreach (uint i in indexes)
            {
                foreach (Instance instance in newSelection)
                {
                    if (instance.id.Type == InstanceType.NetSegment)
                    {
                        if (((NetSegment)instance.data).m_buildIndex == i)
                        {
                            sorted.Add(instance);
                        }
                    }
                }
            }

            return sorted;
        }

        public override void Do()
        {
            MoveItTool.TaskManager.AddSingleTask(MoveItTool.TaskManager.CreateTask(QTask.Threads.Simulation, DoImplementation), "Clone-Do-01");
        }

        internal bool DoImplementation()
        {
            MoveItTool.instance.m_lastInstance = null;

            m_cloneData = new List<CloneData>();
            m_mapLanes = new Dictionary<InstanceID, InstanceID>();
            m_mapNodes = new Dictionary<ushort, ushort>();

            matrix4x.SetTRS(center + moveDelta, Quaternion.AngleAxis(angleDelta * Mathf.Rad2Deg, Vector3.down), Vector3.one);

            // Clone PO
            MoveItTool.PO.MapGroupClones(m_states, this);

            List<QTask> tasks = new List<QTask>();
            foreach (InstanceState state in m_states)
            {
                if (state is ProcState ps)
                {
                    CloneDataPO cloneData = new CloneDataPO()
                    {
                        Original = state.instance,
                        m_action = this
                    };

                    ps.BeginClone(ref matrix4x, moveDelta.y, angleDelta, center, followTerrain, cloneData);
                    tasks.Add(MoveItTool.TaskManager.CreateTask(QTask.Threads.Simulation, () => { return FinalizePO(cloneData); }));
                    m_cloneData.Add(cloneData);
                }
            }
            MoveItTool.TaskManager.AddBatch(tasks, null, null, "Clone-Do-02");

            // Clone nodes before buildings or segments
            foreach (InstanceState state in m_states)
            {
                if (state is NodeState)
                {
                    Instance clone = state.instance.Clone(state, ref matrix4x, moveDelta.y, angleDelta, center, followTerrain, m_mapNodes, this);

                    if (clone == null)
                    {
                        Log.Info($"Failed to clone node {state}", "[M23]");
                        return true;
                    }

                    m_cloneData.Add(new CloneData()
                    {
                        Original = state.instance,
                        Clone = clone,
                        CloneState = state
                    });

                    m_mapNodes.Add(state.instance.id.NetNode, clone.id.NetNode);
                }
            }

            // Clone buildings next (so attached nodes are created before segments)
            List<ushort> attachedNodes = new List<ushort>();
            foreach (InstanceState state in m_states)
            {
                if (state is BuildingState)
                {
                    Instance clone = state.instance.Clone(state, ref matrix4x, moveDelta.y, angleDelta, center, followTerrain, m_mapNodes, this);

                    if (clone == null)
                    {
                        Log.Info($"Failed to clone building {state}", "[M24]");
                        return true;
                    }

                    m_cloneData.Add(new CloneData()
                    {
                        Original = state.instance,
                        Clone = clone,
                        CloneState = state
                    });

                    foreach (Instance inst in clone.subInstances)
                    {
                        if (inst is MoveableNode mn)
                        {
                            attachedNodes.Add(mn.id.NetNode);
                        }
                    }
                }
            }

            // Clone the remaining types
            foreach (InstanceState state in m_states)
            {
                if (!(state is NodeState || state is BuildingState || state is ProcState))
                {
                    Instance clone = state.instance.Clone(state, ref matrix4x, moveDelta.y, angleDelta, center, followTerrain, m_mapNodes, this);

                    if (clone == null)
                    {
                        Log.Info($"Failed to clone {state}", "[M25]");
                        return true;
                    }

                    m_cloneData.Add(new CloneData()
                    {
                        Original = state.instance,
                        Clone = clone,
                        CloneState = state
                    });

                    if (state is SegmentState segmentState)
                    {
                        MoveItTool.NS.SetSegmentModifiers(clone.id.NetSegment, segmentState);
                        if (segmentState.LaneIDs != null)
                        {
                            // old version does not store lane ids
                            var clonedLaneIds = MoveableSegment.GetLaneIds(clone.id.NetSegment);
                            DebugUtils.AssertEq(clonedLaneIds.Count, segmentState.LaneIDs.Count, "clonedLaneIds.Count, segmentState.LaneIDs.Count");
                            for (int i = 0; i < clonedLaneIds.Count; ++i)
                            {
                                var origLaneIId = new InstanceID { NetLane = segmentState.LaneIDs[i] };
                                var cloneLaneIId = new InstanceID { NetLane = clonedLaneIds[i] };
                                m_mapLanes.Add(origLaneIId, cloneLaneIId);
                            }
                        }
                    }
                }
            }

            // Look for overlapping nodes within the clones, to reattach networks to attached networks
            int c = 0;
            HashSet<CloneData> tmpClones = new HashSet<CloneData>(m_cloneData);
            foreach (CloneData cloneData in tmpClones)
            {
                if (cloneData.Clone is MoveableNode mn)
                {
                    NetNode node = (NetNode)mn.data;
                    NetInfo nodeInfo = (NetInfo)mn.Info.Prefab;
                    c++;

                    foreach (ushort attachedId in attachedNodes)
                    {
                        NetNode attached = nodeBuffer[attachedId];

                        if ((node.m_flags & NetNode.Flags.Untouchable) == NetNode.Flags.Untouchable && (attached.m_flags & NetNode.Flags.Untouchable) == NetNode.Flags.Untouchable &&
                            node.Info.m_class.m_service == attached.Info.m_class.m_service && node.Info.m_class.m_subService == attached.Info.m_class.m_subService)
                        {
                            if ((mn.position - attached.m_position).magnitude < 0.01f)
                            {
                                if (NodeMerging.MergeNodes(new NodeMergeExisting()
                                {
                                    ParentId = attachedId,
                                    ChildId = mn.id.NetNode
                                }))
                                {
                                    m_cloneData.Remove(cloneData);
                                }
                            }
                        }
                    }
                }
            }
            //Log.Debug($"Independent nodes: {c}, attached nodes: {attachedNodes.Count}, objects: {m_cloneData.Count} (was: {tmpClones.Count})");

            MoveItTool.TaskManager.AddSingleTask(MoveItTool.TaskManager.CreateTask(QTask.Threads.Simulation, DoFinalize), "Clone-Do-03");

            return true;
        }

        internal bool FinalizePO(CloneData cloneData)
        {
            if (!(cloneData is CloneDataPO clonePO)) throw new Exception($"PO Clone: cloneData is not for PO");
            return MoveItTool.PO.Logic.RetrieveClone(clonePO);
        }

        internal bool DoFinalize()
        {
            // Merge nodes
            if (MoveItTool.instance.MergeNodes)
            {
                foreach (NodeMergeClone mergeClone in m_nodeMergeData)
                {
                    CloneData cloneData = CloneData.GetFromState(m_cloneData, mergeClone.nodeState);
                    if (NodeMerging.MergeNodes(mergeClone.ConvertToExisting(cloneData)))
                    {
                        MoveableNode.UpdateSegments(mergeClone.ParentId, mergeClone.ParentNetNode.m_position);
                        cloneData.Clone = mergeClone.ParentIId;
                        m_mapNodes[mergeClone.nodeState.instance.id.NetNode] = mergeClone.ParentId;
                    }
                    else
                    {
                        if (cloneData == null)
                            Log.Info($"Failed node merge - virtual:{mergeClone.ChildId}, placed:<null> (parent:{mergeClone.ParentId})", "[M26.1]");
                        else
                            Log.Info($"Failed node merge - virtual:{mergeClone.ChildId}, placed:{cloneData.StateIId.NetNode} (parent:{mergeClone.ParentId})", "[M26.2]");
                    }
                }
            }

            if (m_states.Count == 1)
            {
                foreach (InstanceState state in m_states)
                {
                    MoveItTool.CloneSingleObject(state.Info.Prefab);
                    break;
                }
            }

            // Select clones
            selection = CloneData.GetClones(m_cloneData);
            MoveItTool.m_debugPanel.UpdatePanel();

            UpdateArea(GetTotalBounds(false));
            try
            {
                MoveItTool.UpdatePillarMap();
            }
            catch (Exception e)
            {
                DebugUtils.Log("CloneActionBase.Do failed");
                DebugUtils.LogException(e);
            }

            // Clone integrations, including lanes
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
                        //Debug.Log($"Integrated-Paste\n- {item.Value.id} {item.Value.id.Debug()}\n- {data.Value}");
                        data.Key.Paste(cloneData.CloneIId, data.Value, mapOrigToClone);
                    }
                    catch (Exception e)
                    {
                        InstanceID sourceInstanceID = cloneData.StateIId;
                        InstanceID targetInstanceID = cloneData.CloneIId;
                        Log.Error($"integration {data.Key} Failed to paste from {sourceInstanceID.Type}:{sourceInstanceID.Index} to {targetInstanceID.Type}:{targetInstanceID.Index}", "[M21]");
                        DebugUtils.LogException(e);
                    }
                }
            }

            if (m_cloneData != null && m_cloneData.Count > 0)
            {
                Dictionary<Instance, Instance> toReplace = new Dictionary<Instance, Instance>();

                ActionQueue.instance.ReplaceInstancesForward(m_cloneData);
            }

            return true;
        }

        public override void Undo()
        {
            if (m_cloneData == null || m_cloneData.Count == 0) return;

            Bounds bounds = GetTotalBounds(false);

            List<QTask> tasks = new List<QTask>();

            foreach (CloneData data in m_cloneData)
            {
                bool isMerge = false;
                foreach (NodeMergeBase merge in m_nodeMergeData)
                {
                    if (data.CloneIId == merge.ParentIId)
                    {
                        isMerge = true;
                        break;
                    }
                }
                if (!isMerge)
                {
                    tasks.Add(MoveItTool.TaskManager.CreateTask(QTask.Threads.Simulation, () =>
                    {
                        data.Clone.Delete();
                        return true;
                    }));
                }
            }

            QTask postfix = MoveItTool.TaskManager.CreateTask(QTask.Threads.Main, () =>
            {
                m_cloneData.Clear();

                //m_clones = null;

                // Restore selection
                selection = m_oldSelection;
                MoveItTool.m_debugPanel.UpdatePanel();

                UpdateArea(bounds);
                MoveItTool.UpdatePillarMap();
                return true;
            });

            MoveItTool.TaskManager.AddBatch(tasks, null, postfix, "Clone-Undo-01");
        }

        /// <summary>
        /// Update action when past/future actions change instances
        /// </summary>
        /// <param name="toReplace">The list of CloneData objects that need updated</param>
        public override void ReplaceInstances(List<CloneData> toReplace)
        {
            // Update this action's state instances with the updated instances
            foreach (InstanceState state in m_states)
            {
                CloneData data = CloneData.GetFromOriginal(toReplace, state.instance);
                if (data != null)
                {
                    Log.Debug($"CloneAction Replacing: {state.instance.id.Debug()}/{data.OriginalIId.Debug()} -> {data.CloneIId.Debug()}", "[M78.1]");
                    state.ReplaceInstance(data.Clone);
                }
            }

            // Update the selected instances
            if (m_oldSelection != null)
            {
                foreach (CloneData data in toReplace)
                {
                    if (m_oldSelection.Remove(data.Original))
                    {
                        Log.Debug($"CloneAction Replacing: {data.OriginalIId.Debug()} -> {data.CloneIId.Debug()}", "[M79.1]");
                        m_oldSelection.Add(data.Clone);
                    }
                }
            }

            // Update this action's clone data
            if (m_cloneData != null && m_cloneData.Count > 0)
            {
                List<CloneData> clonedOrigin = new List<CloneData>();

                foreach (CloneData data in m_cloneData)
                {
                    CloneData update = CloneData.GetFromOriginal(toReplace, data.Original);
                    if (update != null)
                    {
                        update.Clone = data.Clone;
                        clonedOrigin.Add(update);
                        Log.Debug($"CloneAction Replacing: {data.OriginalIId.Debug()} -> {update.CloneIId.Debug()}", "[M80]");
                    }
                    else
                    {
                        clonedOrigin.Add(data);
                    }
                }

                m_cloneData = clonedOrigin;
            }
        }

        public Dictionary<InstanceState, InstanceState> CalculateStates(Vector3 deltaPosition, float deltaAngle, Vector3 center, bool followTerrain, ref HashSet<InstanceState> newStates)
        {
            Matrix4x4 matrix4x = default;
            matrix4x.SetTRS(center + deltaPosition, Quaternion.AngleAxis(deltaAngle * Mathf.Rad2Deg, Vector3.down), Vector3.one);

            Dictionary<InstanceState, InstanceState> statesMap = new Dictionary<InstanceState, InstanceState>();

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
                        newState.terrainHeight = Singleton<TerrainManager>.instance.SampleOriginalRawHeightSmooth(newState.position);
                        newState.position.y = newState.position.y + newState.terrainHeight - state.terrainHeight;
                    }

                    newState.angle = state.angle + deltaAngle;

                    newStates.Add(newState);
                    statesMap.Add(newState, state);
                }
            }
            return statesMap;
        }

        public int Count
        {
            get { return m_states.Count; }
        }
    }
}
