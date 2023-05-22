using ColossalFramework;
using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoveIt
{
    public partial class MoveItTool : ToolBase
    {
        static bool _stepProcessed = false;

        private void RaycastHoverInstance(Ray mouseRay)
        {
            Vector3 origin = mouseRay.origin;
            Vector3 normalized = mouseRay.direction.normalized;
            Vector3 vector = mouseRay.origin + normalized * Camera.main.farClipPlane;
            Segment3 ray = new Segment3(origin, vector);

            Building[] buildingBuffer = Singleton<BuildingManager>.instance.m_buildings.m_buffer;
            NetNode[] nodeBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;
            NetSegment[] segmentBuffer = Singleton<NetManager>.instance.m_segments.m_buffer;
            TreeInstance[] treeBuffer = Singleton<TreeManager>.instance.m_trees.m_buffer;

            Vector3 location = RaycastMouseLocation(mouseRay);

            InstanceID id = InstanceID.Empty;

            ItemClass.Layer itemLayers = GetItemLayers();

            bool selectPicker = false;
            bool selectBuilding = true;
            bool selectProps = true;
            bool selectDecals = true;
            bool selectSurfaces = true;
            bool selectNodes = true;
            bool selectSegments = true;
            bool selectTrees = true;
            bool selectProc = PO.Active;

            if (marqueeSelection)
            {
                selectPicker = filterPicker;
                selectBuilding = filterBuildings;
                selectProps = filterProps;
                selectDecals = filterDecals;
                selectSurfaces = filterSurfaces;
                selectNodes = filterNodes;
                selectSegments = filterSegments;
                selectTrees = filterTrees;
                selectProc = PO.Active && filterProcs;
            }

            if (MT_Tool == MT_Tools.Group || MT_Tool == MT_Tools.Inplace)
            {
                selectNodes = false;
                selectTrees = false;
            }
            else if (MT_Tool == MT_Tools.Mirror)
            {
                selectBuilding = false;
                selectProps = false;
                selectDecals = false;
                selectSurfaces = false;
                selectProc = false;
                selectTrees = false;
                selectNodes = false;
            }

            float smallestDist = 640000f;

            bool repeatSearch;
            do
            {
                if (PO.Active && (selectProc || selectPicker))
                {
                    foreach (PO_Object obj in PO.Objects)
                    {
                        if (!obj.isHidden() && stepOver.isValidPO(obj.Id))
                        {
                            if (!selectProc)
                            { // Implies selectPicker is true
                                if (obj.GetPrefab() != Filters.Picker.Info)
                                {
                                    continue;
                                }
                            }
                            float radius = obj.Size / 2;
                            bool inXBounds = obj.Position.x > (location.x - radius) && obj.Position.x < (location.x + radius);
                            bool inZBounds = obj.Position.z > (location.z - radius) && obj.Position.z < (location.z + radius);
                            if (inXBounds && inZBounds)
                            {
                                float t = obj.GetDistance(location);
                                if (t < smallestDist)
                                {
                                    id.NetLane = obj.Id;
                                    smallestDist = t;
                                }
                            }
                        }
                    }
                }

                int gridMinX = Mathf.Max((int)((location.x - 16f) / 64f + 135f) - 1, 0);
                int gridMinZ = Mathf.Max((int)((location.z - 16f) / 64f + 135f) - 1, 0);
                int gridMaxX = Mathf.Min((int)((location.x + 16f) / 64f + 135f) + 1, 269);
                int gridMaxZ = Mathf.Min((int)((location.z + 16f) / 64f + 135f) + 1, 269);

                for (int i = gridMinZ; i <= gridMaxZ; i++)
                {
                    for (int j = gridMinX; j <= gridMaxX; j++)
                    {
                        if (selectBuilding || selectSurfaces || (selectPicker && Filters.Picker.IsBuilding))
                        {
                            ushort building = BuildingManager.instance.m_buildingGrid[i * 270 + j];
                            int count = 0;
                            while (building != 0u)
                            {
                                if (stepOver.isValidB(building) && IsBuildingValid(ref buildingBuffer[building], itemLayers) && buildingBuffer[building].RayCast(building, ray, out float t) && t < smallestDist)
                                {
                                    if (Filters.Filter(buildingBuffer[building].Info, ref buildingBuffer[building], true))
                                    {
                                        id.Building = Building.FindParentBuilding(building);
                                        if (id.Building == 0) id.Building = building;
                                        smallestDist = t;
                                    }
                                }
                                building = buildingBuffer[building].m_nextGridBuilding;

                                if (++count > 49152)
                                {
                                    CODebugBase<LogChannel>.Error(LogChannel.Core, "Buildings: Invalid list detected!\n" + Environment.StackTrace);
                                    break;
                                }
                            }
                        }

                        if (selectProps || selectDecals || selectSurfaces || (selectPicker && Filters.Picker.IsProp))
                        {
                            PropLayer.Manager.RaycastHoverInstance(ref i, ref j, ref stepOver, ref ray, ref smallestDist, ref id, itemLayers);
                        }

                        if (selectNodes || selectBuilding || (selectPicker && Filters.Picker.IsNode))
                        {
                            ushort node = NetManager.instance.m_nodeGrid[i * 270 + j];
                            int count = 0;
                            while (node != 0u)
                            {
                                if (stepOver.isValidN(node) && IsNodeValid(ref nodeBuffer[node], itemLayers) && RayCastNode(ref nodeBuffer[node], ray, -1000f, out float t, out float priority) && t < smallestDist)
                                {
                                    ushort building = 0;
                                    if (!superSelect && !Event.current.alt)
                                    {
                                        building = NetNode.FindOwnerBuilding(node, 363f);
                                    }

                                    if (building != 0)
                                    {
                                        if (selectBuilding)
                                        {
                                            id.Building = Building.FindParentBuilding(building);
                                            if (id.Building == 0) id.Building = building;
                                            smallestDist = t;
                                        }
                                    }
                                    else if (selectNodes || (selectPicker && Filters.Picker.IsNode))
                                    {
                                        if (Filters.Filter(nodeBuffer[node]))
                                        {
                                            id.NetNode = node;
                                            smallestDist = t;
                                        }
                                    }
                                }
                                node = nodeBuffer[node].m_nextGridNode;

                                if (++count > 32768)
                                {
                                    CODebugBase<LogChannel>.Error(LogChannel.Core, "Nodes: Invalid list detected!\n" + Environment.StackTrace);
                                }
                            }
                        }

                        if (selectSegments || selectBuilding || (selectPicker && Filters.Picker.IsSegment))
                        {
                            ushort segment = NetManager.instance.m_segmentGrid[i * 270 + j];
                            int count = 0;
                            while (segment != 0u)
                            {
                                if (stepOver.isValidS(segment) && IsSegmentValid(ref segmentBuffer[segment], itemLayers) &&
                                            segmentBuffer[segment].RayCast(segment, ray, -1000f, false, out float t, out float priority) && t < smallestDist)
                                {
                                    ushort building = 0;
                                    if (!superSelect && !Event.current.alt)
                                    {
                                        building = FindOwnerBuilding(segment, 363f);
                                    }

                                    if (building != 0)
                                    {
                                        if (selectBuilding)
                                        {
                                            id.Building = Building.FindParentBuilding(building);
                                            if (id.Building == 0) id.Building = building;
                                            smallestDist = t;
                                        }
                                    }
                                    else if (selectSegments || (selectPicker && Filters.Picker.IsSegment))
                                    {
                                        if (!selectNodes || (
                                            (!stepOver.isValidN(segmentBuffer[segment].m_startNode) || !RayCastNode(ref nodeBuffer[segmentBuffer[segment].m_startNode], ray, -1000f, out float t2, out priority)) &&
                                            (!stepOver.isValidN(segmentBuffer[segment].m_endNode) || !RayCastNode(ref nodeBuffer[segmentBuffer[segment].m_endNode], ray, -1000f, out t2, out priority))
                                        ))
                                        {
                                            if (Filters.Filter(segmentBuffer[segment]))
                                            {
                                                id.NetSegment = segment;
                                                smallestDist = t;
                                            }
                                        }
                                    }
                                }
                                segment = segmentBuffer[segment].m_nextGridSegment;

                                if (++count > 36864)
                                {
                                    CODebugBase<LogChannel>.Error(LogChannel.Core, "Segments: Invalid list detected!\n" + Environment.StackTrace);
                                    segment = 0;
                                }
                            }
                        }
                    }
                }

                if (selectTrees || (selectPicker && Filters.Picker.IsTree))
                {
                    gridMinX = Mathf.Max((int)((location.x - 8f) / 32f + 270f), 0);
                    gridMinZ = Mathf.Max((int)((location.z - 8f) / 32f + 270f), 0);
                    gridMaxX = Mathf.Min((int)((location.x + 8f) / 32f + 270f), 539);
                    gridMaxZ = Mathf.Min((int)((location.z + 8f) / 32f + 270f), 539);

                    for (int i = gridMinZ; i <= gridMaxZ; i++)
                    {
                        for (int j = gridMinX; j <= gridMaxX; j++)
                        {
                            uint tree = TreeManager.instance.m_treeGrid[i * 540 + j];
                            while (tree != 0)
                            {
                                if (stepOver.isValidT(tree) && IsTreeValid(ref treeBuffer[tree], itemLayers) && treeBuffer[tree].RayCast(tree, ray, out float t, out float targetSqr) && t < smallestDist)
                                {
                                    if (Filters.Filter(treeBuffer[tree].Info))
                                    {
                                        id.Tree = tree;
                                        smallestDist = t;
                                    }
                                }
                                tree = treeBuffer[tree].m_nextGridTree;
                            }
                        }
                    }
                }

                repeatSearch = false;
                if (OptionsKeymapping.stepOverKey.IsPressed())
                {
                    if (!_stepProcessed)
                    {
                        _stepProcessed = true;
                        repeatSearch = true;
                        stepOver.Add(id);
                    }
                }
                else
                {
                    _stepProcessed = false;
                }
            }
            while (repeatSearch);

            m_hoverInstance = id;
            m_debugPanel?.UpdatePanel(id);
            ActionQueue.instance.current?.OnHover();
        }

        private HashSet<Instance> GetMarqueeList(Ray mouseRay)
        {
            HashSet<Instance> list = new HashSet<Instance>();

            Building[] buildingBuffer = Singleton<BuildingManager>.instance.m_buildings.m_buffer;
            NetNode[] nodeBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;
            NetSegment[] segmentBuffer = Singleton<NetManager>.instance.m_segments.m_buffer;
            TreeInstance[] treeBuffer = Singleton<TreeManager>.instance.m_trees.m_buffer;

            m_selection.a = m_clickPositionAbs;
            m_selection.c = RaycastMouseLocation(mouseRay);

            if (m_selection.a.x == m_selection.c.x && m_selection.a.z == m_selection.c.z)
            {
                m_selection = default;
            }
            else
            {
                float angle = Camera.main.transform.localEulerAngles.y * Mathf.Deg2Rad;
                Vector3 down = new Vector3(Mathf.Cos(angle), 0, -Mathf.Sin(angle));
                Vector3 right = new Vector3(-down.z, 0, down.x);

                Vector3 a = m_selection.c - m_selection.a;
                float dotDown = Vector3.Dot(a, down);
                float dotRight = Vector3.Dot(a, right);

                if ((dotDown > 0 && dotRight > 0) || (dotDown <= 0 && dotRight <= 0))
                {
                    m_selection.b = m_selection.a + dotDown * down;
                    m_selection.d = m_selection.a + dotRight * right;
                }
                else
                {
                    m_selection.b = m_selection.a + dotRight * right;
                    m_selection.d = m_selection.a + dotDown * down;
                }

                Vector3 min = m_selection.Min();
                Vector3 max = m_selection.Max();

                int gridMinX = Mathf.Max((int)((min.x - 16f) / 64f + 135f), 0);
                int gridMinZ = Mathf.Max((int)((min.z - 16f) / 64f + 135f), 0);
                int gridMaxX = Mathf.Min((int)((max.x + 16f) / 64f + 135f), 269);
                int gridMaxZ = Mathf.Min((int)((max.z + 16f) / 64f + 135f), 269);

                InstanceID id = new InstanceID();
                ItemClass.Layer itemLayers = GetItemLayers();

                if (PO.Active && (filterProcs || filterPicker))
                {
                    foreach (PO_Object obj in PO.Objects)
                    {
                        if (!filterProcs)
                        { // Implies filterPicker is true
                            if (obj.GetPrefab() != Filters.Picker.Info)
                            {
                                continue;
                            }
                        }
                        if (!obj.isHidden() && PointInRectangle(m_selection, obj.Position))
                        {
                            id.NetLane = obj.Id;
                            list.AddObject(id);
                        }
                    }
                }

                for (int i = gridMinZ; i <= gridMaxZ; i++)
                {
                    for (int j = gridMinX; j <= gridMaxX; j++)
                    {
                        if (filterBuildings || filterSurfaces || (filterPicker && Filters.Picker.IsBuilding))
                        {
                            ushort building = BuildingManager.instance.m_buildingGrid[i * 270 + j];
                            int count = 0;
                            while (building != 0u)
                            {
                                if (IsBuildingValid(ref buildingBuffer[building], itemLayers) && PointInRectangle(m_selection, buildingBuffer[building].m_position))
                                {
                                    if (Filters.Filter(buildingBuffer[building].Info, ref buildingBuffer[building]))
                                    {
                                        id.Building = Building.FindParentBuilding(building);
                                        if (id.Building == 0) id.Building = building;
                                        list.Add(id);
                                    }
                                }
                                building = buildingBuffer[building].m_nextGridBuilding;

                                if (++count > 49152)
                                {
                                    CODebugBase<LogChannel>.Error(LogChannel.Core, "Buildings: Invalid list detected!\n" + Environment.StackTrace);
                                    break;
                                }
                            }
                        }

                        if (filterProps || filterDecals || filterSurfaces || (filterPicker && Filters.Picker.IsProp))
                        {
                            PropLayer.Manager.GetMarqueeList(ref i, ref j, ref id, ref m_selection, ref list, itemLayers);
                        }

                        if (filterNodes || filterBuildings || (filterPicker && Filters.Picker.IsNode))
                        {
                            ushort node = NetManager.instance.m_nodeGrid[i * 270 + j];
                            int count = 0;
                            while (node != 0u)
                            {
                                if (IsNodeValid(ref nodeBuffer[node], itemLayers) && PointInRectangle(m_selection, nodeBuffer[node].m_position))
                                {
                                    ushort building = 0;
                                    if (!superSelect) building = NetNode.FindOwnerBuilding(node, 363f);

                                    if (building != 0)
                                    {
                                        if (filterBuildings)
                                        {
                                            id.Building = Building.FindParentBuilding(building);
                                            if (id.Building == 0) id.Building = building;
                                            list.Add(id);
                                        }
                                    }
                                    else if (filterNodes || (filterPicker && Filters.Picker.IsNode))
                                    {
                                        if (Filters.Filter(nodeBuffer[node]))
                                        {
                                            id.NetNode = node;
                                            list.Add(id);
                                        }
                                    }
                                }
                                node = nodeBuffer[node].m_nextGridNode;

                                if (++count > 32768)
                                {
                                    CODebugBase<LogChannel>.Error(LogChannel.Core, "Nodes: Invalid list detected!\n" + Environment.StackTrace);
                                }
                            }
                        }

                        if (filterSegments || filterBuildings || (filterPicker && Filters.Picker.IsSegment))
                        {
                            ushort segment = NetManager.instance.m_segmentGrid[i * 270 + j];
                            int count = 0;
                            while (segment != 0u)
                            {
                                if (IsSegmentValid(ref segmentBuffer[segment], itemLayers) && PointInRectangle(m_selection, segmentBuffer[segment].m_bounds.center))
                                {
                                    ushort building = 0;
                                    if (!superSelect) building = FindOwnerBuilding(segment, 363f);

                                    if (building != 0)
                                    {
                                        if (filterBuildings)
                                        {
                                            id.Building = Building.FindParentBuilding(building);
                                            if (id.Building == 0) id.Building = building;
                                            list.Add(id);
                                        }
                                    }
                                    else if (filterSegments || (filterPicker && Filters.Picker.IsSegment))
                                    {
                                        if (Filters.Filter(segmentBuffer[segment]))
                                        {
                                            id.NetSegment = segment;
                                            list.Add(id);
                                        }
                                    }
                                }
                                segment = segmentBuffer[segment].m_nextGridSegment;

                                if (++count > 36864)
                                {
                                    CODebugBase<LogChannel>.Error(LogChannel.Core, "Segments: Invalid list detected!\n" + Environment.StackTrace);
                                }
                            }
                        }
                    }
                }

                if (filterTrees || (filterPicker && Filters.Picker.IsTree))
                {
                    gridMinX = Mathf.Max((int)((min.x - 8f) / 32f + 270f), 0);
                    gridMinZ = Mathf.Max((int)((min.z - 8f) / 32f + 270f), 0);
                    gridMaxX = Mathf.Min((int)((max.x + 8f) / 32f + 270f), 539);
                    gridMaxZ = Mathf.Min((int)((max.z + 8f) / 32f + 270f), 539);

                    for (int i = gridMinZ; i <= gridMaxZ; i++)
                    {
                        for (int j = gridMinX; j <= gridMaxX; j++)
                        {
                            uint tree = TreeManager.instance.m_treeGrid[i * 540 + j];

                            while (tree != 0)
                            {
                                if (PointInRectangle(m_selection, treeBuffer[tree].Position))
                                {
                                    if (Filters.Filter(treeBuffer[tree].Info) && IsTreeValid(ref treeBuffer[tree], itemLayers))
                                    {
                                        id.Tree = tree;
                                        list.Add(id);
                                    }
                                }
                                tree = treeBuffer[tree].m_nextGridTree;
                            }
                        }
                    }
                }
            }

            return list;
        }

        public static ushort FindOwnerBuilding(ushort segment, float maxDistance)
        {
            Building[] buildingBuffer = BuildingManager.instance.m_buildings.m_buffer;
            ushort[] buildingGrid = BuildingManager.instance.m_buildingGrid;
            NetNode[] nodeBuffer = NetManager.instance.m_nodes.m_buffer;
            NetSegment[] segmentBuffer = NetManager.instance.m_segments.m_buffer;

            ushort startNode = segmentBuffer[segment].m_startNode;
            ushort endNode = segmentBuffer[segment].m_endNode;
            Vector3 startPosition = nodeBuffer[startNode].m_position;
            Vector3 endPosition = nodeBuffer[endNode].m_position;
            Vector3 vector = Vector3.Min(startPosition, endPosition);
            Vector3 vector2 = Vector3.Max(startPosition, endPosition);
            int gridMinX = Mathf.Max((int)((vector.x - maxDistance) / 64f + 135f), 0);
            int gridMinZ = Mathf.Max((int)((vector.z - maxDistance) / 64f + 135f), 0);
            int gridMaxX = Mathf.Min((int)((vector2.x + maxDistance) / 64f + 135f), 269);
            int gridMaxZ = Mathf.Min((int)((vector2.z + maxDistance) / 64f + 135f), 269);

            ushort result = 0;
            float maxDistSqr = maxDistance * maxDistance;
            for (int i = gridMinZ; i <= gridMaxZ; i++)
            {
                for (int j = gridMinX; j <= gridMaxX; j++)
                {
                    ushort building = buildingGrid[i * 270 + j];
                    int count = 0;
                    while (building != 0)
                    {
                        Vector3 position2 = buildingBuffer[building].m_position;
                        float num8 = position2.x - startPosition.x;
                        float num9 = position2.z - startPosition.z;
                        float num10 = num8 * num8 + num9 * num9;
                        if (num10 < maxDistSqr && buildingBuffer[building].ContainsNode(startNode) && buildingBuffer[building].ContainsNode(endNode))
                        {
                            return building;
                        }
                        building = buildingBuffer[building].m_nextGridBuilding;
                        if (++count >= 49152)
                        {
                            CODebugBase<LogChannel>.Error(LogChannel.Core, "Buildings: Invalid list detected!\n" + Environment.StackTrace);
                            break;
                        }
                    }
                }
            }
            return result;
        }

        private bool isLeft(Vector3 P0, Vector3 P1, Vector3 P2)
        {
            return ((P1.x - P0.x) * (P2.z - P0.z) - (P2.x - P0.x) * (P1.z - P0.z)) > 0;
        }

        internal bool PointInRectangle(Quad3 rectangle, Vector3 p)
        {
            return isLeft(rectangle.a, rectangle.b, p) && isLeft(rectangle.b, rectangle.c, p) && isLeft(rectangle.c, rectangle.d, p) && isLeft(rectangle.d, rectangle.a, p);
        }

        private ItemClass.Layer GetItemLayers()
        {
            ItemClass.Layer itemLayers = ItemClass.Layer.Default;

            if (InfoManager.instance.CurrentMode == InfoManager.InfoMode.Water)
            {
                itemLayers |= ItemClass.Layer.WaterPipes;
            }
            if (InfoManager.instance.CurrentMode == InfoManager.InfoMode.Fishing)
            {
                itemLayers |= ItemClass.Layer.FishingPaths;
            }
            else if (InfoManager.instance.CurrentMode == InfoManager.InfoMode.Transport)
            { // Removes Default assignment
                itemLayers = ItemClass.Layer.MetroTunnels | ItemClass.Layer.BlimpPaths | ItemClass.Layer.FerryPaths | ItemClass.Layer.ShipPaths | ItemClass.Layer.AirplanePaths | ItemClass.Layer.HelicopterPaths;
            }
            else if (InfoManager.instance.CurrentMode == InfoManager.InfoMode.Traffic || InfoManager.instance.CurrentMode == InfoManager.InfoMode.Transport)
            {
                itemLayers |= ItemClass.Layer.MetroTunnels;
            }
            else if (InfoManager.instance.CurrentMode == InfoManager.InfoMode.Underground)
            { // Removes Default assignment
                itemLayers = ItemClass.Layer.MetroTunnels;
            }
            else
            {
                itemLayers |= ItemClass.Layer.Markers;
            }
            return itemLayers;
        }

        private bool IsBuildingValid(ref Building building, ItemClass.Layer itemLayers)
        {
            if (superSelect) return true;
            if ((building.m_flags & Building.Flags.Created) == Building.Flags.Created)
            {
                return (building.Info.m_class.m_layer & itemLayers) != ItemClass.Layer.None;
            }

            return false;
        }

        private bool IsNodeValid(ref NetNode node, ItemClass.Layer itemLayers)
        {
            if (superSelect) return true;
            if ((node.m_flags & NetNode.Flags.Created) == NetNode.Flags.Created)
            {
                return (node.Info.GetConnectionClass().m_layer & itemLayers) != ItemClass.Layer.None;
            }

            return false;
        }

        private bool IsSegmentValid(ref NetSegment segment, ItemClass.Layer itemLayers)
        {
            if (superSelect) return true;
            if ((segment.m_flags & NetSegment.Flags.Created) == NetSegment.Flags.Created)
            {
                return (segment.Info.GetConnectionClass().m_layer & itemLayers) != ItemClass.Layer.None;
            }

            return false;
        }

        private bool IsTreeValid(ref TreeInstance tree, ItemClass.Layer itemLayers)
        {
            if (superSelect) return true;
            if (((TreeInstance.Flags)tree.m_flags & TreeInstance.Flags.Created) == TreeInstance.Flags.Created)
            {
                return (tree.Info.m_class.m_layer & itemLayers) != ItemClass.Layer.None;
            }

            return false;
        }

        private static bool RayCastNode(ref NetNode node, Segment3 ray, float snapElevation, out float t, out float priority)
        {
            NetInfo info = node.Info;
            float num = (float)node.m_elevation + info.m_netAI.GetSnapElevation();
            float t2;
            if (info.m_netAI.IsUnderground())
            {
                t2 = Mathf.Clamp01(Mathf.Abs(snapElevation + num) / 12f);
            }
            else
            {
                t2 = Mathf.Clamp01(Mathf.Abs(snapElevation - num) / 12f);
            }
            float collisionHalfWidth = Mathf.Max(3f, info.m_netAI.GetCollisionHalfWidth());
            float num2 = Mathf.Lerp(info.GetMinNodeDistance(), collisionHalfWidth, t2);
            if (Segment1.Intersect(ray.a.y, ray.b.y, node.m_position.y, out t))
            {
                float num3 = Vector3.Distance(ray.Position(t), node.m_position);
                if (num3 < num2)
                {
                    priority = Mathf.Max(0f, num3 - collisionHalfWidth);
                    return true;
                }
            }
            t = 0f;
            priority = 0f;
            return false;
        }
    }
}
