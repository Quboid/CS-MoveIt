using ColossalFramework;
using ColossalFramework.UI;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace MoveIt
{
    public class ResetAction : BulldozeAction
    {
        public override void Do()
        {
            DoImplementation(true);
            UndoImplementation(true);
        }

        public override void Undo()
        {
            ;
        }
    }

    public class BulldozeAction : Action
    {
        private HashSet<InstanceState> m_states = new HashSet<InstanceState>();

        private HashSet<Instance> m_oldSelection;

        public bool replaceInstances = true;

        public BulldozeAction() : base()
        {
            HashSet<Instance> newSelection = new HashSet<Instance>(selection);
            HashSet<Instance> extraNodes = new HashSet<Instance>();
            HashSet<ushort> segments = new HashSet<ushort>(); // Segments to be removed

            NetManager netManager = Singleton<NetManager>.instance;

            // Add any segments whose node is selected
            foreach (Instance instance in selection)
            {
                if (instance.isValid)
                {
                    if (instance.id.Type == InstanceType.NetSegment)
                    {
                        segments.Add(instance.id.NetSegment);
                    }
                    else if (instance.id.Type == InstanceType.NetNode)
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            ushort segment = netManager.m_nodes.m_buffer[instance.id.NetNode].GetSegment(i);
                            if (segment != 0)
                            {
                                InstanceID instanceID = default;
                                instanceID.NetSegment = segment;

                                if (selection.Contains(instanceID)) continue;

                                newSelection.Add(new MoveableSegment(instanceID));
                                segments.Add(segment);
                            }
                        }
                    }
                    else if (instance.id.Type == InstanceType.Building)
                    {
                        Building building = (Building)((MoveableBuilding)instance).data;
                        ushort nodeId = building.m_netNode;

                        int c = 0;
                        while (nodeId != 0)
                        {
                            NetNode node = netManager.m_nodes.m_buffer[nodeId];

                            if (node.m_building == 0 || node.Info.m_class.m_layer == ItemClass.Layer.WaterPipes)
                            { // Exclude attached nodes with attached buildings (e.g. water buildings)
                                for (int i = 0; i < 8; i++)
                                {
                                    ushort segmentId = node.GetSegment(i);
                                    if (segmentId != 0 && MoveableSegment.isSegmentValid(segmentId) && 
                                            ((netManager.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Untouchable) == NetSegment.Flags.None))
                                    {
                                        InstanceID instanceID = default;
                                        instanceID.NetSegment = segmentId;

                                        if (selection.Contains(instanceID)) continue;

                                        newSelection.Add(new MoveableSegment(instanceID));
                                        segments.Add(segmentId);
                                    }
                                }
                            }

                            nodeId = node.m_nextBuildingNode;

                            if (++c > 32768)
                            {
                                CODebugBase<LogChannel>.Error(LogChannel.Core, "Nodes: Invalid list detected!\n" + Environment.StackTrace);
                                break;
                            }
                        }
                    }
                }
            }

            // Add any nodes whose segments are all selected
            foreach (Instance instance in newSelection)
            {
                if (instance.isValid)
                {
                    if (instance.id.Type == InstanceType.NetSegment)
                    {
                        ushort segId = instance.id.NetSegment;
                        ushort[] nodeIds = { netManager.m_segments.m_buffer[segId].m_startNode, netManager.m_segments.m_buffer[segId].m_endNode };
                        foreach (ushort id in nodeIds)
                        {
                            bool toDelete = true;
                            NetNode node = netManager.m_nodes.m_buffer[id];
                            for (int i = 0; i < 8; i++)
                            {
                                if (node.GetSegment(i) != 0 && !segments.Contains(node.GetSegment(i)))
                                {
                                    toDelete = false;
                                    break;
                                }
                            }
                            if (toDelete)
                            {
                                if (node.Info.m_class.m_layer == ItemClass.Layer.WaterPipes)
                                {
                                    foreach (Building b in BuildingManager.instance.m_buildings.m_buffer)
                                    {
                                        if (b.m_netNode == id)
                                        {
                                            toDelete = false;
                                            break;
                                        }
                                    }
                                }
                            }
                            if (toDelete)
                            {
                                InstanceID instanceId = default;
                                instanceId.NetNode = id;
                                MoveableNode mn = new MoveableNode(instanceId);
                                if (newSelection.Contains(mn)) continue;

                                extraNodes.Add(mn);
                            }
                        }
                    }
                }
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

            foreach (Instance instance in sorted)
            {
                m_states.Add(instance.SaveToState());
            }
            foreach (Instance instance in extraNodes)
            {
                m_states.Add(instance.SaveToState());
            }
            m_states = ProcessPillars(m_states, false);
        }

        public override void Do()
        {
            bool po = false;
            foreach (InstanceState state in m_states)
            {
                if (state is ProcState)
                {
                    po = true;
                    break;
                }
            }
            if (!Settings.POShowDeleteWarning || !po)
            {
                DoImplementation(false);
                return;
            }

            ConfirmPanel panel = UIView.library.ShowModal<ConfirmPanel>("ConfirmPanel", delegate(UIComponent comp, int value)
            {
                if (value == 1)
                    DoImplementation(false);
            });
            panel.SetMessage("Deleting PO", "Procedural Objects can not be undeleted. Are you sure?");
        }

        public void DoImplementation(bool skipPO = false)
        {
            m_oldSelection = selection;

            Bounds bounds = GetTotalBounds(false);

            foreach (InstanceState state in m_states)
            {
                if (skipPO && state is ProcState) continue;
                if (state is BuildingState) continue;

                if (state.instance.isValid)
                {
                    state.instance.Delete();
                }
            }

            // Remove buildings last so attached nodes are cleaned up
            foreach (InstanceState state in m_states)
            {
                if (skipPO && state is ProcState) continue;
                if (!(state is BuildingState)) continue;

                if (state.instance.isValid)
                {
                    ((MoveableBuilding)state.instance).m_assetEditorSubBuilding.Destroy(state.instance.id.Building);
                    state.instance.Delete();
                }
            }

            UpdateArea(bounds);

            selection = new HashSet<Instance>();
            MoveItTool.m_debugPanel.UpdatePanel();
            MoveItTool.UpdatePillarMap();
        }

        public override void Undo()
        {
            UndoImplementation(false);
        }

        public void UndoImplementation(bool reset = false)
        {
            if (m_states == null) return;

            List<CloneData> toReplace = new List<CloneData>();
            Dictionary<ushort, ushort> clonedNodes = new Dictionary<ushort, ushort>();

            var stateToClone = new Dictionary<InstanceState, Instance>();
            var InstanceID_origToClone = new Dictionary<InstanceID, InstanceID>();

            Building[] buildingBuffer = BuildingManager.instance.m_buildings.m_buffer;

            // Recreate nodes
            foreach (InstanceState state in m_states)
            {
                try
                {
                    if (state.instance.id.Type == InstanceType.NetNode)
                    {
                        Instance clone = state.instance.Clone(state, null);
                        toReplace.Add(new CloneData() { Original = state.instance, Clone = clone });
                        stateToClone.Add(state, clone);
                        InstanceID_origToClone.Add(state.instance.id, clone.id);
                        clonedNodes.Add(state.instance.id.NetNode, clone.id.NetNode);
                        ActionQueue.instance.UpdateNodeIdInStateHistory(state.instance.id.NetNode, clone.id.NetNode);
                    }
                }
                catch (Exception e)
                {
                    Log.Info($"Undo Bulldoze failed on {(state is InstanceState ? state.prefabName : "unknown")}\n{e}", "[M16]");
                }
            }

            // Recreate everything except nodes and segments
            foreach (InstanceState state in m_states)
            {
                try
                {
                    if (state.instance.id.Type == InstanceType.NetNode) continue;
                    if (state.instance.id.Type == InstanceType.NetSegment) continue;
                    if (state is ProcState) continue;

                    Instance clone = state.instance.Clone(state, clonedNodes);
                    toReplace.Add(new CloneData() { Original = state.instance, Clone = clone });
                    stateToClone.Add(state, clone);
                    InstanceID_origToClone.Add(state.instance.id, clone.id);

                    if (state.instance.id.Type == InstanceType.Prop)
                    {
                        PropLayer.Manager.SetFixedHeight(clone.id, ((PropState)state).fixedHeight);
                    }
                    else if (state.instance.id.Type == InstanceType.Building)
                    {
                        // Add attached nodes to the clonedNode list so other segments reconnect
                        BuildingState buildingState = state as BuildingState;
                        List<ushort> origNodeIds = new List<ushort>();

                        MoveableBuilding cb = clone as MoveableBuilding;
                        ushort cloneNodeId = ((Building)cb.data).m_netNode;

                        if (reset)
                        {
                            ushort cloneId = cb.id.Building;

                            buildingBuffer[cloneId].m_flags = buildingBuffer[cloneId].m_flags & ~Building.Flags.BurnedDown;
                            buildingBuffer[cloneId].m_flags = buildingBuffer[cloneId].m_flags & ~Building.Flags.Collapsed;
                            buildingBuffer[cloneId].m_flags = buildingBuffer[cloneId].m_flags & ~Building.Flags.Abandoned;
                            buildingBuffer[cloneId].m_flags = buildingBuffer[cloneId].m_flags | Building.Flags.Active;
                            Thread.Sleep(50);
                        }

                        if (cloneNodeId != 0)
                        {
                            int c = 0;
                            foreach (InstanceState i in buildingState.subStates)
                            {
                                if (i is NodeState ns)
                                {
                                    InstanceID instanceID = default;
                                    instanceID.RawData = ns.id;
                                    origNodeIds.Insert(c++, instanceID.NetNode);
                                }
                            }

                            c = 0;
                            while (cloneNodeId != 0)
                            {
                                ushort origNodeId = origNodeIds[c];

                                NetNode clonedAttachedNode = Singleton<NetManager>.instance.m_nodes.m_buffer[cloneNodeId];
                                if (clonedAttachedNode.Info.GetAI() is TransportLineAI)
                                {
                                    cloneNodeId = clonedAttachedNode.m_nextBuildingNode;
                                    continue;
                                }

                                if (clonedNodes.ContainsKey(origNodeId))
                                {
                                    Log.Debug($"Node #{origNodeId} is already in clone list!", "[M17]");
                                }

                                clonedNodes.Add(origNodeId, cloneNodeId);

                                cloneNodeId = clonedAttachedNode.m_nextBuildingNode;

                                if (++c > 32768)
                                {
                                    CODebugBase<LogChannel>.Error(LogChannel.Core, "Nodes: Invalid list detected!\n" + Environment.StackTrace);
                                    break;
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Warning($"Undo Bulldoze failed on {(state is InstanceState ? state.prefabName : "unknown")}\n{e}", "[M18]");
                }
            }

            // Recreate segments
            foreach (InstanceState state in m_states)
            {
                try
                {
                    if (state is SegmentState segmentState)
                    {
                        if (!clonedNodes.ContainsKey(segmentState.startNodeId))
                        {
                            InstanceID instanceID = InstanceID.Empty;
                            instanceID.NetNode = segmentState.startNodeId;

                            // Don't clone if node is missing
                            if (!((Instance)instanceID).isValid) continue;

                            clonedNodes.Add(segmentState.startNodeId, segmentState.startNodeId);
                        }

                        if (!clonedNodes.ContainsKey(segmentState.endNodeId))
                        {
                            InstanceID instanceID = InstanceID.Empty;
                            instanceID.NetNode = segmentState.endNodeId;

                            // Don't clone if node is missing
                            if (!((Instance)instanceID).isValid) continue;

                            clonedNodes.Add(segmentState.endNodeId, segmentState.endNodeId);
                        }

                        Instance clone = state.instance.Clone(state, clonedNodes);
                        toReplace.Add(new CloneData() { Original = state.instance, Clone = clone });
                        stateToClone.Add(state, clone);
                        InstanceID_origToClone.Add(state.instance.id, clone.id);
                        MoveItTool.NS.SetSegmentModifiers(clone.id.NetSegment, segmentState);
                    }
                }
                catch (Exception e)
                {
                    Log.Warning($"Undo Bulldoze failed on {(state is InstanceState ? state.prefabName : "unknown")}\n{e}", "[M19]");
                }
            }

            // clone integrations.
            foreach (var item in stateToClone)
            {
                foreach (var data in item.Key.IntegrationData)
                {
                    try
                    {
                        data.Key.Paste(item.Value.id, data.Value, InstanceID_origToClone);
                    }
                    catch (Exception e)
                    {
                        InstanceID sourceInstanceID = item.Key.instance.id;
                        InstanceID targetInstanceID = item.Value.id;
                        Log.Error($"integration {data.Key} Failed to paste from " +
                            $"{sourceInstanceID.Type}:{sourceInstanceID.Index} to {targetInstanceID.Type}:{targetInstanceID.Index}", "[M20]");
                        DebugUtils.LogException(e);
                    }
                }
            }

            if (replaceInstances)
            {
                ReplaceInstances(toReplace);
                ActionQueue.instance.ReplaceInstancesBackward(toReplace);

                selection = new HashSet<Instance>();
                foreach (Instance i in m_oldSelection)
                {
                    if (i is MoveableProc) continue;
                    selection.Add(i);
                }
                MoveItTool.m_debugPanel.UpdatePanel();
            }

            // Does not check MoveItTool.advancedPillarControl, because even if disabled now advancedPillarControl may have been active earlier in action queue
            foreach (KeyValuePair<BuildingState, BuildingState> pillarClone in pillarsOriginalToClone)
            {
                BuildingState originalState = pillarClone.Key;
                originalState.instance.isHidden = false;
                buildingBuffer[originalState.instance.id.Building].m_flags &= ~Building.Flags.Hidden;
                selection.Add(originalState.instance);
                m_states.Add(originalState);
            }
            if (pillarsOriginalToClone.Count > 0)
            {
                MoveItTool.UpdatePillarMap();
            }
        }

        internal override void UpdateNodeIdInSegmentState(ushort oldId, ushort newId)
        {
            foreach (InstanceState state in m_states)
            {
                if (state is SegmentState segState)
                {
                    if (segState.startNodeId == oldId)
                    {
                        segState.startNodeId = newId;
                    }
                    if (segState.endNodeId == oldId)
                    {
                        segState.endNodeId = newId;
                    }
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
                    Log.Debug($"BulldozeAction Replacing: {state.instance.id.Debug()}/{data.OriginalIId.Debug()} -> {data.CloneIId.Debug()}", "[M78.6]");
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
                        Log.Debug($"BulldozeAction Replacing: {data.OriginalIId.Debug()} -> {data.CloneIId.Debug()}", "[M79.2]");
                        m_oldSelection.Add(data.Clone);
                    }
                }
            }
        }
    }
}
