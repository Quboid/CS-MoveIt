using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.UI;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using UnityEngine;

namespace MoveIt
{
    public partial class MoveItTool : ToolBase
    {
        protected override void OnToolUpdate()
        {
            if (m_nextAction != ToolAction.None) return;

            if (m_pauseMenu != null && m_pauseMenu.isVisible)
            {
                if (ToolState == ToolStates.Default || ToolState == ToolStates.MouseDragging || ToolState == ToolStates.DrawingSelection)
                {
                    ToolsModifierControl.SetTool<DefaultTool>();
                }

                StopCloning();
                StopTool();

                SetToolState();

                UIView.library.Hide("PauseMenu");

                return;
            }

            lock (ActionQueue.instance)
            {
                bool isInsideUI = this.m_toolController.IsInsideUI;

                if (m_leftClickTime == 0 && Input.GetMouseButton(0))
                    {
                        if (!isInsideUI)
                        {
                            m_leftClickTime = Stopwatch.GetTimestamp();
                            OnLeftMouseDown();
                        }
                    }

                if (m_leftClickTime != 0)
                {
                    long elapsed = ElapsedMilliseconds(m_leftClickTime);

                    if (!Input.GetMouseButton(0))
                    {
                        m_leftClickTime = 0;

                        if (elapsed < 250)
                        {
                            try
                            {
                                OnLeftClick();
                            }
                            catch (MissingMethodException e)
                            {
                                Log.Debug("Prop Painter [OnLeftClick] error: " + e.ToString(), "[M46]");
                            }
                        }
                        else
                        {
                            OnLeftDragStop();
                        }

                        try
                        {
                            OnLeftMouseUp();
                        }
                        catch (MissingMethodException e)
                        {
                            Log.Debug("Prop Painter [OnLeftMouseUp] error: " + e.ToString(), "[M47]");
                        }
                    }
                    else if (elapsed >= 250)
                    {
                        OnLeftDrag();
                    }
                }

                if (m_rightClickTime == 0 && Input.GetMouseButton(1))
                    {
                        if (!isInsideUI)
                        {
                            m_rightClickTime = Stopwatch.GetTimestamp();
                            OnRightMouseDown();
                        }
                    }

                if (m_rightClickTime != 0)
                {
                    long elapsed = ElapsedMilliseconds(m_rightClickTime);

                    if (!Input.GetMouseButton(1))
                    {
                        m_rightClickTime = 0;

                        if (elapsed < 250)
                        {
                            OnRightClick();
                        }
                        else
                        {
                            OnRightDragStop();
                        }

                        OnRightMouseUp();
                    }
                    else if (elapsed >= 250)
                    {
                        OnRightDrag();
                    }
                }

                if (m_middleClickTime == 0 && Input.GetMouseButton(2) && Event.current.control)
                {
                    if (!isInsideUI)
                    {
                        m_middleClickTime = Stopwatch.GetTimestamp();
                        OnMiddleMouseDown();
                    }
                }

                if (m_middleClickTime != 0)
                {
                    long elapsed = ElapsedMilliseconds(m_middleClickTime);

                    if (!Input.GetMouseButton(2))
                    {
                        m_middleClickTime = 0;

                        if (elapsed < 250)
                        {
                            OnMiddleClick();
                        }
                        else
                        {
                            OnMiddleDragStop();
                        }

                        OnMiddleMouseUp();
                    }
                    else if (elapsed >= 250)
                    {
                        OnMiddleDrag();
                    }
                }

                if (!isInsideUI && Cursor.visible)
                {
                    Ray mouseRay = Camera.main.ScreenPointToRay(Input.mousePosition);

                    m_hoverInstance = null;
                    m_marqueeInstances = null;
                    m_segmentGuide = default;

                    switch (ToolState)
                    {
                        case ToolStates.Default:
                        case ToolStates.Aligning:
                        case ToolStates.Picking:
                        case ToolStates.ToolActive:
                            {
                                RaycastHoverInstance(mouseRay);
                                break;
                            }
                        case ToolStates.MouseDragging:
                            {
                                TransformAction action = ActionQueue.instance.current as TransformAction;

                                Vector3 newMove = action.moveDelta;
                                float newAngle = action.angleDelta;
                                float newSnapAngle = 0f;

                                if (snapping)
                                {
                                    action.Virtual = false;
                                }
                                else
                                {
                                    if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                                    {
                                        action.Virtual = !Settings.fastMove;
                                    }
                                    else
                                    {
                                        action.Virtual = Settings.fastMove;
                                    }
                                }

                                if (m_leftClickTime > 0 != m_middleClickTime > 0)
                                {
                                    UpdateSensitivityMode();

                                    float y = action.moveDelta.y;

                                    if (m_isLowSensitivity || m_middleClickTime > 0)
                                    {
                                        Vector3 mouseDeltaBefore = m_sensitivityTogglePosAbs - m_clickPositionAbs;
                                        Vector3 mouseDeltaAfter = (RaycastMouseLocation(mouseRay) - m_sensitivityTogglePosAbs) / 5f;
                                        newMove = m_dragStartRelative + mouseDeltaBefore + mouseDeltaAfter;
                                    }
                                    else
                                    {
                                        newMove = m_dragStartRelative + (RaycastMouseLocation(mouseRay) - m_clickPositionAbs);
                                    }

                                    newMove.y = y;
                                }

                                if (m_rightClickTime > 0)
                                {
                                    UpdateSensitivityMode();

                                    if (m_isLowSensitivity)
                                    {
                                        float mouseRotateBefore = m_sensitivityTogglePosX - m_sensitivityAngleOffset;
                                        float mouseRotateAfter = (Input.mousePosition.x - m_sensitivityTogglePosX) / 5;
                                        float mouseTravel = (mouseRotateBefore + mouseRotateAfter - m_mouseStartX) / Screen.width * 1.2f;

                                        newAngle = ushort.MaxValue * 9.58738E-05f * mouseTravel;
                                    }
                                    else
                                    {
                                        newAngle = ushort.MaxValue * 9.58738E-05f * (Input.mousePosition.x - m_mouseStartX) / Screen.width * 1.2f;
                                    }
                                    if (Event.current.alt)
                                    {
                                        float quarterPI = Mathf.PI / 4;
                                        newAngle = quarterPI * Mathf.Round(newAngle / quarterPI);
                                    }
                                    newAngle += m_startAngle;
                                    action.autoCurve = false;
                                }
                                else if (snapping)
                                {
                                    newMove = GetSnapDelta(newMove, newAngle, action.center, out action.autoCurve);

                                    if (action.autoCurve)
                                    {
                                        action.segmentCurve = m_segmentGuide;
                                    }
                                }
                                else
                                {
                                    action.autoCurve = false;
                                }

                                if (action.moveDelta != newMove || action.angleDelta != newAngle || action.snapAngle != newSnapAngle)
                                {
                                    action.moveDelta = newMove;
                                    action.angleDelta = newAngle;
                                    action.snapAngle = newSnapAngle;
                                    action.followTerrain = followTerrain;
                                    m_nextAction = ToolAction.Do;
                                }

                                UIToolOptionPanel.RefreshSnapButton();
                                break;
                            }
                        case ToolStates.Cloning:
                            {
                                if (m_rightClickTime != 0) break;

                                UpdateSensitivityMode();

                                CloneActionBase action = ActionQueue.instance.current as CloneActionBase;

                                Vector3 newMove;
                                float y = action.moveDelta.y;
                                if (m_isLowSensitivity)
                                {
                                    Vector3 mouseDeltaBefore = m_sensitivityTogglePosAbs - m_clickPositionAbs;
                                    Vector3 mouseDeltaAfter = (RaycastMouseLocation(mouseRay) - m_sensitivityTogglePosAbs) / 5;
                                    newMove = mouseDeltaBefore + mouseDeltaAfter;
                                }
                                else
                                {
                                    newMove = RaycastMouseLocation(mouseRay) - action.center;
                                }
                                newMove.y = y;

                                if (MergeNodes)
                                {
                                    action.m_snapNode = GetMergingNodes(action, newMove, action.angleDelta, action.center);
                                    if (snapping && action.m_snapNode != null)
                                    {
                                        newMove += action.m_snapNode.ParentNetNode.m_position - action.m_snapNode.adjustedState.position;
                                        GetMergingNodes(action, newMove, action.angleDelta, action.center); // Recalculate node merges after snapping
                                    }
                                }
                                else if (snapping)
                                {
                                    newMove = GetSnapDelta(newMove, action.angleDelta, action.center, out bool autoCurve);
                                }

                                if (action.moveDelta != newMove)
                                {
                                    action.moveDelta = newMove;
                                }

                                UIToolOptionPanel.RefreshSnapButton();
                                break;
                            }
                        case ToolStates.RightDraggingClone:
                            {
                                UpdateSensitivityMode();

                                CloneActionBase action = ActionQueue.instance.current as CloneActionBase;

                                float newAngle;

                                if (m_isLowSensitivity)
                                {
                                    float mouseRotateBefore = m_sensitivityTogglePosX - m_sensitivityAngleOffset;
                                    float mouseRotateAfter = (Input.mousePosition.x - m_sensitivityTogglePosX) / 5;
                                    float mouseTravel = (mouseRotateBefore + mouseRotateAfter - m_mouseStartX) / Screen.width * 1.2f;

                                    newAngle = ushort.MaxValue * 9.58738E-05f * mouseTravel;
                                }
                                else
                                {
                                    newAngle = ushort.MaxValue * 9.58738E-05f * (Input.mousePosition.x - m_mouseStartX) / Screen.width * 1.2f;
                                }

                                if (Event.current.alt)
                                {
                                    float quarterPI = Mathf.PI / 4;
                                    newAngle = quarterPI * Mathf.Round(newAngle / quarterPI);
                                }
                                newAngle += m_startAngle;

                                if (action.angleDelta != newAngle)
                                {
                                    action.angleDelta = newAngle;
                                }

                                UIToolOptionPanel.RefreshSnapButton();
                                break;
                            }
                        case ToolStates.DrawingSelection:
                            {
                                RaycastHoverInstance(mouseRay);
                                m_marqueeInstances = GetMarqueeList(mouseRay);
                                break;
                            }
                    }
                }
            }
        }

        protected override void OnToolLateUpdate()
        { }

        private bool ProcessMoveKeys(Event e, out Vector3 direction, out float angle)
        {
            direction = Vector3.zero;
            angle = 0;

            float magnitude = 8f;
            if (e.alt && e.shift)
            {
                magnitude /= 64f;
            }
            else
            {
                if (e.shift) magnitude *= 8f;
                if (e.alt) magnitude /= 8f;
            }

            if (IsKeyDown(OptionsKeymapping.moveXpos, e))
            {
                direction.x += magnitude;
            }
            if (IsKeyDown(OptionsKeymapping.moveXneg, e))
            {
                direction.x -= magnitude;
            }

            if (IsKeyDown(OptionsKeymapping.moveYpos, e))
            {
                direction.y += magnitude;
            }
            if (IsKeyDown(OptionsKeymapping.moveYneg, e))
            {
                direction.y -= magnitude;
            }

            if (IsKeyDown(OptionsKeymapping.moveZpos, e))
            {
                direction.z += magnitude;
            }
            if (IsKeyDown(OptionsKeymapping.moveZneg, e))
            {
                direction.z -= magnitude;
            }

            if (IsKeyDown(OptionsKeymapping.turnPos, e))
            {
                angle -= magnitude * 20f * 9.58738E-05f;
            }
            if (IsKeyDown(OptionsKeymapping.turnNeg, e))
            {
                angle += magnitude * 20f * 9.58738E-05f;
            }

            if (direction != Vector3.zero || angle != 0)
            {
                if (m_keyTime == 0)
                {
                    m_keyTime = Stopwatch.GetTimestamp();
                    return true;
                }
                else if (ElapsedMilliseconds(m_keyTime) >= 333)
                {
                    return true;
                }
            }
            else
            {
                m_keyTime = 0;
            }

            return false;
        }

        private bool ProcessScaleKeys(Event e, out float magnitude)
        {
            magnitude = 0.01f;
            if (e.alt && e.shift)
            {
                magnitude /= 64f;
            }
            else
            {
                if (e.shift) magnitude *= 8f;
                if (e.alt) magnitude /= 8f;
            }

            if (IsKeyDown(OptionsKeymapping.scaleIn, e))
            {
                magnitude = 0 - magnitude;
            }
            else if (IsKeyDown(OptionsKeymapping.scaleOut, e))
            {
            }
            else
            {
                m_scaleKeyTime = 0;
                return false;
            }

            if (m_scaleKeyTime == 0)
            {
                m_scaleKeyTime = Stopwatch.GetTimestamp();
                return true;
            }
            else if (ElapsedMilliseconds(m_scaleKeyTime) >= 333)
            {
                return true;
            }

            return false;
        }

        private bool IsKeyDown(SavedInputKey inputKey, Event e)
        {
            int code = inputKey.value;
            KeyCode keyCode = (KeyCode)(code & 0xFFFFFFF);

            bool ctrl = ((code & 0x40000000) != 0);

            return Input.GetKey(keyCode) && ctrl == e.control;
        }

        private long ElapsedMilliseconds(long startTime)
        {
            long endTime = Stopwatch.GetTimestamp();
            long elapsed;

            if (endTime > startTime)
            {
                elapsed = endTime - startTime;
            }
            else
            {
                elapsed = startTime - endTime;
            }

            return elapsed / (Stopwatch.Frequency / 1000);
        }

        /// <summary>
        /// Search a CloneActionBase action for possible merges and return most suitable snap candidate
        /// </summary>
        /// <param name="action"></param>
        /// <param name="moveDelta"></param>
        /// <param name="angleDelta"></param>
        /// <param name="center"></param>
        /// <returns></returns>
        private NodeMergeClone GetMergingNodes(CloneActionBase action, Vector3 moveDelta, float angleDelta, Vector3 center)
        {
            action.m_snapNode = null;
            action.m_nodeMergeData.Clear();

            // Look for nodes
            bool found = false;
            foreach (InstanceState state in action.m_states)
            {
                if (state is NodeState)
                {
                    found = true;
                    break;
                }
            }
            if (!found) return null;

            HashSet<InstanceState> states = new HashSet<InstanceState>();
            Dictionary<InstanceState, InstanceState> statesMap = action.CalculateStates(moveDelta, angleDelta, center, followTerrain, ref states);

            NodeMergeClone snapCandidate = null;
            float distance = NodeMerging.MAX_MERGE_DISTANCE;
            foreach (InstanceState state in states)
            {
                if (state is NodeState ns)
                {
                    NodeMergeClone mergeData = ns.FindNearestNode((NodeState)statesMap[ns]);
                    if (mergeData == null) continue;
                    action.m_nodeMergeData.Add(mergeData);

                    if (mergeData.Distance < distance)
                    {
                        snapCandidate = mergeData;
                        distance = mergeData.Distance;
                    }
                }
            }
            if (snapCandidate != null) snapCandidate.status = NodeMergeStatuses.Snap;

            //string msg = "";
            //foreach (var x in action.m_nodeMergeData) msg += $"{x}, ";
            //if (msg != "") msg = $"\n  {action.m_nodeMergeData.Count}: " + msg;
            //Log.Debug($"AAA02 candidate found:{(snapCandidate == null ? "<null>" : snapCandidate.nodeState.Info.Prefab.name + " (#" + snapCandidate.StateId + ")")}{msg}", "[M48]");

            return snapCandidate;
        }

        private Vector3 GetSnapDelta(Vector3 moveDelta, float angleDelta, Vector3 center, out bool autoCurve)
        {
            autoCurve = false;

            if (VectorUtils.XZ(moveDelta) == Vector2.zero)
            {
                return moveDelta;
            }

            Vector3 newMoveDelta = moveDelta;

            NetManager netManager = NetManager.instance;
            NetSegment[] segmentBuffer = netManager.m_segments.m_buffer;
            NetNode[] nodeBuffer = netManager.m_nodes.m_buffer;
            Building[] buildingBuffer = BuildingManager.instance.m_buildings.m_buffer;

            bool snap = false;

            HashSet<InstanceState> newStates = new HashSet<InstanceState>();
            if (ActionQueue.instance.current is TransformAction transformAction)
            {
                newStates = transformAction.CalculateStates(moveDelta, angleDelta, center, followTerrain);
            }
            else if (ActionQueue.instance.current is CloneActionBase cloneAction)
            {
                cloneAction.CalculateStates(moveDelta, angleDelta, center, followTerrain, ref newStates);
            }

            // Snap to direction
            if (newStates.Count == 1)
            {
                foreach (InstanceState state in newStates)
                {
                    if (state.instance.id.Type == InstanceType.NetSegment)
                    {
                        return SnapSegmentDirections(state.instance.id.NetSegment, state.position, moveDelta);
                    }
                    else if (state.instance.id.Type == InstanceType.NetNode)
                    {
                        if (TrySnapNodeDirections(state.instance.id.NetNode, state.position, moveDelta, out newMoveDelta, out autoCurve))
                        {
                            DebugUtils.Log("Snap to direction: " + moveDelta + ", " + newMoveDelta);
                            return newMoveDelta;
                        }
                    }
                }
            }

            HashSet<ushort> ingnoreSegments = new HashSet<ushort>();
            HashSet<ushort> segmentList = new HashSet<ushort>();

            ushort[] closeSegments = new ushort[16];

            // Get list of closest segments
            foreach (InstanceState state in newStates)
            {
                netManager.GetClosestSegments(state.position, closeSegments, out int closeSegmentCount);
                segmentList.UnionWith(closeSegments);

                if (ToolState != ToolStates.Cloning)
                {
                    ingnoreSegments.UnionWith(state.instance.segmentList);
                }
            }

            float distanceSq = float.MaxValue;

            // Snap to node
            foreach (ushort segment in segmentList)
            {
                if (!ingnoreSegments.Contains(segment))
                {
                    foreach (InstanceState state in newStates)
                    {
                        if (state.instance.id.Type == InstanceType.NetNode)
                        {
                            float minSqDistance = segmentBuffer[segment].Info.GetMinNodeDistance() / 2f;
                            minSqDistance *= minSqDistance;

                            ushort startNode = segmentBuffer[segment].m_startNode;
                            ushort endNode = segmentBuffer[segment].m_endNode;

                            snap = TrySnapping(nodeBuffer[startNode].m_position, state.position, minSqDistance, ref distanceSq, moveDelta, ref newMoveDelta) || snap;
                            snap = TrySnapping(nodeBuffer[endNode].m_position, state.position, minSqDistance, ref distanceSq, moveDelta, ref newMoveDelta) || snap;
                        }
                    }
                }
            }

            if (snap)
            {
                DebugUtils.Log("Snap to node: " + moveDelta + ", " + newMoveDelta);
                return newMoveDelta;
            }

            // Snap to segment
            foreach (ushort segment in segmentList)
            {
                if (!ingnoreSegments.Contains(segment))
                {
                    foreach (InstanceState state in newStates)
                    {
                        if (state.instance.id.Type == InstanceType.NetNode)
                        {
                            float minSqDistance = segmentBuffer[segment].Info.GetMinNodeDistance() / 2f;
                            minSqDistance *= minSqDistance;

                            segmentBuffer[segment].GetClosestPositionAndDirection(state.position, out Vector3 testPos, out Vector3 direction);

                            snap = TrySnapping(testPos, state.position, minSqDistance, ref distanceSq, moveDelta, ref newMoveDelta) || snap;
                        }
                    }
                }
            }

            if (snap)
            {
                DebugUtils.Log("Snap to segment: " + moveDelta + ", " + newMoveDelta);
                return newMoveDelta;
            }

            // Snap to grid
            ushort block = 0;
            ushort previousBlock = 0;
            Vector3 refPosition = Vector3.zero;
            bool smallRoad = false;

            foreach (ushort segment in segmentList)
            {
                bool hasBlocks = segment != 0 && (segmentBuffer[segment].m_blockStartLeft != 0 || segmentBuffer[segment].m_blockStartRight != 0 || segmentBuffer[segment].m_blockEndLeft != 0 || segmentBuffer[segment].m_blockEndRight != 0);
                if (hasBlocks && !ingnoreSegments.Contains(segment))
                {
                    foreach (InstanceState state in newStates)
                    {
                        if (state.instance.id.Type != InstanceType.NetSegment)
                        {
                            Vector3 testPosition = state.position;

                            if (state.instance.id.Type == InstanceType.Building)
                            {
                                ushort building = state.instance.id.Building;
                                testPosition = GetBuildingSnapPoint(state.position, state.angle, buildingBuffer[building].Length, buildingBuffer[building].Width);
                            }

                            segmentBuffer[segment].GetClosestZoneBlock(testPosition, ref distanceSq, ref block);

                            if (block != previousBlock)
                            {
                                refPosition = testPosition;

                                if (state.instance.id.Type == InstanceType.NetNode)
                                {
                                    if (nodeBuffer[state.instance.id.NetNode].Info.m_halfWidth <= 4f)
                                    {
                                        smallRoad = true;
                                    }
                                }

                                previousBlock = block;
                            }
                        }
                    }
                }
            }

            if (block != 0)
            {
                Vector3 newPosition = refPosition;
                ZoneBlock zoneBlock = ZoneManager.instance.m_blocks.m_buffer[block];
                SnapToBlock(ref newPosition, zoneBlock.m_position, zoneBlock.m_angle, smallRoad);

                DebugUtils.Log("Snap to grid: " + moveDelta + ", " + (moveDelta + newPosition - refPosition));
                return moveDelta + newPosition - refPosition;
            }

            // Snap to editor grid
            if ((ToolManager.instance.m_properties.m_mode & ItemClass.Availability.AssetEditor) != ItemClass.Availability.None)
            {
                Vector3 assetGridPosition = Vector3.zero;
                float testMagnitude = 0;

                foreach (InstanceState state in newStates)
                {
                    Vector3 testPosition = state.position;

                    if (state.instance.id.Type == InstanceType.Building)
                    {
                        ushort building = state.instance.id.Building;
                        testPosition = GetBuildingSnapPoint(state.position, state.angle, buildingBuffer[building].Length, buildingBuffer[building].Width);
                    }


                    float x = Mathf.Round(testPosition.x / 8f) * 8f;
                    float z = Mathf.Round(testPosition.z / 8f) * 8f;

                    Vector3 newPosition = new Vector3(x, testPosition.y, z);
                    float deltaMagnitude = (newPosition - testPosition).sqrMagnitude;

                    if (assetGridPosition == Vector3.zero || deltaMagnitude < testMagnitude)
                    {
                        refPosition = testPosition;
                        assetGridPosition = newPosition;
                        deltaMagnitude = testMagnitude;
                    }
                }

                DebugUtils.Log("Snap to grid: " + moveDelta + ", " + (moveDelta + assetGridPosition - refPosition));
                return moveDelta + assetGridPosition - refPosition;
            }

            return moveDelta;
        }

        private bool TrySnapping(Vector3 testPos, Vector3 newPosition, float minSqDistance, ref float distanceSq, Vector3 moveDelta, ref Vector3 newMoveDelta)
        {
            float testSqDist = Vector2.SqrMagnitude(VectorUtils.XZ(testPos - newPosition));

            if (testSqDist < minSqDistance && testSqDist < distanceSq)
            {
                newMoveDelta = moveDelta + (testPos - newPosition);
                newMoveDelta.y = moveDelta.y;

                distanceSq = testSqDist;

                return true;
            }

            return false;
        }

        private Vector3 GetBuildingSnapPoint(Vector3 position, float angle, int length, int width)
        {
            float x = 0;
            float z = length * 4f;

            if (width % 2 != 0) x = 4f;

            float ca = Mathf.Cos(angle);
            float sa = Mathf.Sin(angle);

            return position + new Vector3(ca * x - sa * z, 0f, sa * x + ca * z);
        }

        private void SnapToBlock(ref Vector3 point, Vector3 refPoint, float refAngle, bool smallRoad)
        {
            Vector3 direction = new Vector3(Mathf.Cos(refAngle), 0f, Mathf.Sin(refAngle));
            Vector3 forward = direction * 8f;
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);

            if (smallRoad)
            {
                refPoint.x += forward.x * 0.5f + right.x * 0.5f;
                refPoint.z += forward.z * 0.5f + right.z * 0.5f;
            }

            Vector2 delta = new Vector2(point.x - refPoint.x, point.z - refPoint.z);
            float num = Mathf.Round((delta.x * forward.x + delta.y * forward.z) * 0.015625f);
            float num2 = Mathf.Round((delta.x * right.x + delta.y * right.z) * 0.015625f);
            point.x = refPoint.x + num * forward.x + num2 * right.x;
            point.z = refPoint.z + num * forward.z + num2 * right.z;
        }

        private Vector3 SnapSegmentDirections(ushort segment, Vector3 newPosition, Vector3 moveDelta)
        {
            NetManager netManager = NetManager.instance;
            NetSegment[] segmentBuffer = netManager.m_segments.m_buffer;
            NetNode[] nodeBuffer = netManager.m_nodes.m_buffer;

            float minSqDistance = segmentBuffer[segment].Info.GetMinNodeDistance() / 2f;
            minSqDistance *= minSqDistance;

            ushort startNode = segmentBuffer[segment].m_startNode;
            ushort endNode = segmentBuffer[segment].m_endNode;

            Vector3 startPos = nodeBuffer[segmentBuffer[segment].m_startNode].m_position;
            Vector3 endPos = nodeBuffer[segmentBuffer[segment].m_endNode].m_position;

            Vector3 newMoveDelta = moveDelta;
            float distanceSq = minSqDistance;
            bool snap = false;

            // Snap to tangent intersection
            for (int i = 0; i < 8; i++)
            {
                ushort segmentA = nodeBuffer[startNode].GetSegment(i);
                if (segmentA != 0 && segmentA != segment)
                {
                    for (int j = 0; j < 8; j++)
                    {
                        ushort segmentB = nodeBuffer[endNode].GetSegment(j);

                        if (segmentB != 0 && segmentB != segment)
                        {
                            Vector3 startDir = segmentBuffer[segmentA].m_startNode == startNode ? segmentBuffer[segmentA].m_startDirection : segmentBuffer[segmentA].m_endDirection;
                            Vector3 endDir = segmentBuffer[segmentB].m_startNode == endNode ? segmentBuffer[segmentB].m_startDirection : segmentBuffer[segmentB].m_endDirection;

                            if (!NetSegment.IsStraight(startPos, startDir, endPos, endDir, out float num))
                            {
                                float dot = startDir.x * endDir.x + startDir.z * endDir.z;
                                if (dot >= -0.999f && Line2.Intersect(VectorUtils.XZ(startPos), VectorUtils.XZ(startPos + startDir), VectorUtils.XZ(endPos), VectorUtils.XZ(endPos + endDir), out float u, out float v))
                                {
                                    snap = TrySnapping(startPos + startDir * u, newPosition, minSqDistance, ref distanceSq, moveDelta, ref newMoveDelta) || snap;
                                }
                            }
                        }
                    }
                }
            }

            if (!snap)
            {
                // Snap to start tangent
                for (int i = 0; i < 8; i++)
                {
                    ushort segmentA = nodeBuffer[startNode].GetSegment(i);
                    if (segmentA != 0 && segmentA != segment)
                    {
                        Vector3 startDir = segmentBuffer[segmentA].m_startNode == startNode ? segmentBuffer[segmentA].m_startDirection : segmentBuffer[segmentA].m_endDirection;
                        Vector3 offset = Line2.Offset(startDir, startPos - newPosition);
                        offset = newPosition + offset - startPos;
                        float num = offset.x * startDir.x + offset.z * startDir.z;

                        TrySnapping(startPos + startDir * num, newPosition, minSqDistance, ref distanceSq, moveDelta, ref newMoveDelta);
                    }
                }

                // Snap to end tangent
                for (int i = 0; i < 8; i++)
                {
                    ushort segmentB = nodeBuffer[endNode].GetSegment(i);

                    if (segmentB != 0 && segmentB != segment)
                    {
                        Vector3 endDir = segmentBuffer[segmentB].m_startNode == endNode ? segmentBuffer[segmentB].m_startDirection : segmentBuffer[segmentB].m_endDirection;
                        Vector3 offset = Line2.Offset(endDir, endPos - newPosition);
                        offset = newPosition + offset - endPos;
                        float num = offset.x * endDir.x + offset.z * endDir.z;

                        TrySnapping(endPos + endDir * num, newPosition, minSqDistance, ref distanceSq, moveDelta, ref newMoveDelta);
                    }
                }
            }

            // Snap straight
            TrySnapping((startPos + endPos) / 2f, newPosition, minSqDistance, ref distanceSq, moveDelta, ref newMoveDelta);

            return newMoveDelta;
        }

        private bool TrySnapNodeDirections(ushort node, Vector3 newPosition, Vector3 moveDelta, out Vector3 newMoveDelta, out bool autoCurve)
        {
            string snapType = "";

            m_segmentGuide = default;

            NetManager netManager = NetManager.instance;
            NetSegment[] segmentBuffer = netManager.m_segments.m_buffer;
            NetNode[] nodeBuffer = netManager.m_nodes.m_buffer;

            float minSqDistance = nodeBuffer[node].Info.GetMinNodeDistance() / 2f;
            minSqDistance *= minSqDistance;

            autoCurve = false;
            newMoveDelta = moveDelta;
            float distanceSq = minSqDistance;

            bool snap = false;

            // Snap to curve
            for (int i = 0; i < 8; i++)
            {
                ushort segmentA = nodeBuffer[node].GetSegment(i);
                if (segmentA != 0)
                {
                    for (int j = i + 1; j < 8; j++)
                    {
                        ushort segmentB = nodeBuffer[node].GetSegment(j);

                        if (segmentB != 0 && segmentB != segmentA)
                        {
                            NetSegment segment = default;
                            segment.m_startNode = segmentBuffer[segmentA].m_startNode == node ? segmentBuffer[segmentA].m_endNode : segmentBuffer[segmentA].m_startNode;
                            segment.m_endNode = segmentBuffer[segmentB].m_startNode == node ? segmentBuffer[segmentB].m_endNode : segmentBuffer[segmentB].m_startNode;

                            segment.m_startDirection = (nodeBuffer[segment.m_endNode].m_position - nodeBuffer[segment.m_startNode].m_position).normalized;
                            segment.m_endDirection = -segment.m_startDirection;

                            segment.GetClosestPositionAndDirection(newPosition, out Vector3 testPos, out _);
                            // Straight
                            if (TrySnapping(testPos, newPosition, minSqDistance, ref distanceSq, moveDelta, ref newMoveDelta))
                            {
                                autoCurve = true;
                                m_segmentGuide = segment;
                                snapType = "Straight";
                                snap = true;
                            }

                            for (int k = 0; k < 8; k++)
                            {
                                ushort segmentC = nodeBuffer[segment.m_startNode].GetSegment(k);
                                if (segmentC != 0 && segmentC != segmentA)
                                {
                                    for (int l = 0; l < 8; l++)
                                    {
                                        ushort segmentD = nodeBuffer[segment.m_endNode].GetSegment(l);

                                        if (segmentD != 0 && segmentD != segmentB)
                                        {
                                            segment.m_startDirection = segmentBuffer[segmentC].m_startNode == segment.m_startNode ? -segmentBuffer[segmentC].m_startDirection : -segmentBuffer[segmentC].m_endDirection;
                                            segment.m_endDirection = segmentBuffer[segmentD].m_startNode == segment.m_endNode ? -segmentBuffer[segmentD].m_startDirection : -segmentBuffer[segmentD].m_endDirection;

                                            Vector2 A = VectorUtils.XZ(nodeBuffer[segment.m_endNode].m_position - nodeBuffer[segment.m_startNode].m_position).normalized;
                                            Vector2 B = VectorUtils.XZ(segment.m_startDirection);
                                            float side1 = A.x * B.y - A.y * B.x;

                                            B = VectorUtils.XZ(segment.m_endDirection);
                                            float side2 = A.x * B.y - A.y * B.x;

                                            if (Mathf.Sign(side1) != Mathf.Sign(side2) ||
                                                (side1 != side2 && (side1 == 0 || side2 == 0)) ||
                                                Vector2.Dot(A, VectorUtils.XZ(segment.m_startDirection)) < 0 ||
                                                Vector2.Dot(A, VectorUtils.XZ(segment.m_endDirection)) > 0)
                                            {
                                                continue;
                                            }

                                            Bezier3 bezier = default;
                                            bezier.a = Singleton<NetManager>.instance.m_nodes.m_buffer[segment.m_startNode].m_position;
                                            bezier.d = Singleton<NetManager>.instance.m_nodes.m_buffer[segment.m_endNode].m_position;
                                            bool smoothStart = (Singleton<NetManager>.instance.m_nodes.m_buffer[segment.m_startNode].m_flags & NetNode.Flags.Middle) != NetNode.Flags.None;
                                            bool smoothEnd = (Singleton<NetManager>.instance.m_nodes.m_buffer[segment.m_endNode].m_flags & NetNode.Flags.Middle) != NetNode.Flags.None;
                                            NetSegment.CalculateMiddlePoints(bezier.a, segment.m_startDirection, bezier.d, segment.m_endDirection, smoothStart, smoothEnd, out bezier.b, out bezier.c);

                                            testPos = bezier.Position(0.5f);
                                            // Curve Middle
                                            if (TrySnapping(testPos, newPosition, minSqDistance, ref distanceSq, moveDelta, ref newMoveDelta))
                                            {
                                                autoCurve = true;
                                                m_segmentGuide = segment;
                                                snapType = "Curve Middle";
                                                snap = true;
                                            }
                                            else
                                            {
                                                segment.GetClosestPositionAndDirection(newPosition, out testPos, out _);
                                                // Curve
                                                if (TrySnapping(testPos, newPosition, minSqDistance, ref distanceSq, moveDelta, ref newMoveDelta))
                                                {
                                                    autoCurve = true;
                                                    m_segmentGuide = segment;
                                                    snapType = "Curve";
                                                    snap = true;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Snap to tangent
            for (int i = 0; i < 8; i++)
            {
                ushort segment = nodeBuffer[node].GetSegment(i);
                if (segment != 0)
                {
                    ushort testNode = segmentBuffer[segment].m_startNode == node ? segmentBuffer[segment].m_endNode : segmentBuffer[segment].m_startNode;
                    Vector3 testPos = nodeBuffer[testNode].m_position;

                    for (int j = 0; j < 8; j++)
                    {
                        ushort segmentA = nodeBuffer[testNode].GetSegment(j);
                        if (segmentA != 0 && segmentA != segment)
                        {
                            // Straight
                            Vector3 startDir = segmentBuffer[segmentA].m_startNode == testNode ? segmentBuffer[segmentA].m_startDirection : segmentBuffer[segmentA].m_endDirection;
                            Vector3 offset = Line2.Offset(startDir, testPos - newPosition);
                            offset = newPosition + offset - testPos;
                            float num = offset.x * startDir.x + offset.z * startDir.z;

                            if (TrySnapping(testPos + startDir * num, newPosition, minSqDistance, ref distanceSq, moveDelta, ref newMoveDelta))
                            {
                                m_segmentGuide = default;

                                m_segmentGuide.m_startNode = node;
                                m_segmentGuide.m_endNode = testNode;

                                m_segmentGuide.m_startDirection = startDir;
                                m_segmentGuide.m_endDirection = -startDir;
                                snapType = "Tangent Straight";
                                autoCurve = false;
                                snap = true;
                            }
                            else
                            {
                                // 90°
                                startDir = new Vector3(-startDir.z, startDir.y, startDir.x);
                                offset = Line2.Offset(startDir, testPos - newPosition);
                                offset = newPosition + offset - testPos;
                                num = offset.x * startDir.x + offset.z * startDir.z;

                                if (TrySnapping(testPos + startDir * num, newPosition, minSqDistance, ref distanceSq, moveDelta, ref newMoveDelta))
                                {
                                    m_segmentGuide = default;

                                    m_segmentGuide.m_startNode = node;
                                    m_segmentGuide.m_endNode = testNode;

                                    m_segmentGuide.m_startDirection = startDir;
                                    m_segmentGuide.m_endDirection = -startDir;
                                    snapType = "Tangent 90°";
                                    autoCurve = false;
                                    snap = true;
                                }
                            }
                        }
                    }
                }
            }

            if (snap)
            {
                DebugUtils.Log("Snapping " + snapType + " " + autoCurve);
            }

            return snap;
        }
    }
}
