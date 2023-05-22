using ColossalFramework.UI;
using ColossalFramework;
using System;
using UnityEngine;

namespace MoveIt
{
    public partial class MoveItTool : ToolBase
    {
        private void OnLeftMouseDown()
        {
            Log.Debug("OnLeftMouseDown: " + ToolState, "[M32]");

            Vector3 mousePos = RaycastMouseLocation();
            if (ToolState == ToolStates.Default)
            {
                if (marqueeSelection && (m_hoverInstance == null || !Action.selection.Contains(m_hoverInstance)))
                {
                    m_selection = default;
                    m_marqueeInstances = null;

                    SetToolState(ToolStates.DrawingSelection);
                }

                m_lastInstance = m_hoverInstance;

                m_sensitivityTogglePosAbs = mousePos;
                m_clickPositionAbs = mousePos;
            }
            else if (ToolState == ToolStates.MouseDragging)
            {
                TransformAction action = ActionQueue.instance.current as TransformAction;
                m_dragStartRelative = action.moveDelta;
                UpdateSensitivityMode();

                m_sensitivityTogglePosAbs = mousePos;
                m_clickPositionAbs = mousePos;
            }
            else if (ToolState == ToolStates.Cloning)
            {
                CloneActionBase action = ActionQueue.instance.current as CloneActionBase;
                action.followTerrain = followTerrain;

                if (POProcessing == 0)
                {
                    SetToolState();
                    m_nextAction = ToolAction.Do;

                    UpdateSensitivityMode();
                }
            }
        }

        private void OnLeftMouseUp()
        {
            Log.Debug("OnLeftMouseUp: " + ToolState, "[M33]");

            if (ToolState == ToolStates.DrawingSelection)
            {
                SetToolState();

                Event e = Event.current;

                if (m_marqueeInstances == null || m_marqueeInstances.Count == 0 ||
                    (e.alt && !Action.selection.Overlaps(m_marqueeInstances)) ||
                    ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && Action.selection.IsSupersetOf(m_marqueeInstances))
                    ) return;

                if (!(ActionQueue.instance.current is SelectAction))
                {
                    SelectAction action = new SelectAction(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
                    ActionQueue.instance.Push(action);
                }
                else
                {
                    ActionQueue.instance.Invalidate();
                }

                if (e.alt)
                {
                    Action.selection.ExceptWith(m_marqueeInstances);
                    m_debugPanel.UpdatePanel();
                }
                else
                {
                    if (!(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
                    {
                        Action.selection.Clear();
                    }
                    Action.selection.UnionWith(m_marqueeInstances);
                    m_debugPanel.UpdatePanel();
                }

                m_marqueeInstances = null;
            }
        }

        private void OnLeftClick()
        {
            Log.Debug("OnLeftClick: " + ToolState, "[M34]");

            if (POProcessing > 0)
            {
                return;
            }

            if (ToolState == ToolStates.Default || ToolState == ToolStates.DrawingSelection || ToolState == ToolStates.ToolActive)
            {
                Event e = Event.current;
                if (m_hoverInstance == null) return;

                #region Debug Ouput
                //Instance instance = m_hoverInstance;
                //InstanceID instanceID = instance.id;
                //Log.Debug($"instance:{(instance == null ? "null" : instance.GetType().ToString())}", "[M30]");

                //if (instanceID.Building > 0)
                //{
                //    MoveableBuilding mb = (MoveableBuilding)instance;
                //    string msg = $"{mb.id.Building}:{mb.Info.Name}\n";
                //    foreach (Instance subInstance in mb.subInstances)
                //    {
                //        msg += $" - {subInstance.id.Building}/{subInstance.id.NetNode}: {subInstance.Info.Name}\n";
                //        if (subInstance.id.Building > 0)
                //        {
                //            foreach (Instance subSubInstance in ((MoveableBuilding)subInstance).subInstances)
                //            {
                //                msg += $"    - {subSubInstance.id.Building}/{subSubInstance.id.NetNode}: {subSubInstance.Info.Name}\n";
                //            }
                //        }
                //    }
                //    msg += "End";
                //    Log.Debug(msg, "[M31]");
                //}
                #endregion

                if (!(ActionQueue.instance.current is SelectAction))
                {
                    ActionQueue.instance.Push(new SelectAction(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)));
                }
                else
                {
                    ActionQueue.instance.Invalidate();
                }

                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) //"if (e.shift)" apparently fails in Linux
                {
                    if (e.alt && m_hoverInstance is MoveableSegment ms && FindOwnerBuilding(ms.id.NetSegment, 363f) == 0)
                    {
                        MoveableNode closest = ms.GetNodeByDistance();
                        MoveableNode furthest = ms.GetNodeByDistance(true);

                        if (!Action.selection.Contains(closest))
                        {
                            Action.selection.Add(closest);
                        }
                        else if (!Action.selection.Contains(furthest))
                        {
                            Action.selection.Add(furthest);
                        }
                        else
                        {
                            Action.selection.Remove(furthest);
                        }
                    }
                    else
                    {
                        if (Action.selection.Contains(m_hoverInstance))
                        {
                            Action.selection.RemoveObject(m_hoverInstance);
                        }
                        else
                        {
                            Action.selection.AddObject(m_hoverInstance);
                        }
                    }
                }
                else
                {
                    if (e.alt && m_hoverInstance is MoveableSegment ms && FindOwnerBuilding(ms.id.NetSegment, 363f) == 0)
                    {
                        MoveableNode closest = ms.GetNodeByDistance();
                        MoveableNode furthest = ms.GetNodeByDistance(true);

                        if (Action.selection.Contains(closest) && !Action.selection.Contains(furthest))
                        {
                            Action.selection.Clear();
                            Action.selection.Add(furthest);
                        }
                        else
                        {
                            Action.selection.Clear();
                            Action.selection.Add(closest);
                        }
                    }
                    else
                    {
                        Action.selection.Clear();
                        Action.selection.AddObject(m_hoverInstance);
                    }
                }

                SetToolState();
            }
            else if (ToolState == ToolStates.Aligning)
            {
                if (MT_Tool == MT_Tools.Height)
                {
                    SetToolState();

                    AlignHeightAction action = new AlignHeightAction();
                    if (m_hoverInstance != null)
                    {
                        action.height = m_hoverInstance.GetYPosition();
                        ActionQueue.instance.Push(action);

                        m_nextAction = ToolAction.Do;
                    }

                    DeactivateTool();
                }
                else if (MT_Tool == MT_Tools.Mirror)
                {
                    SetToolState();

                    AlignMirrorAction action = new AlignMirrorAction();
                    if (m_hoverInstance != null && m_hoverInstance is MoveableSegment ms)
                    {
                        NetSegment[] segmentBuffer = Singleton<NetManager>.instance.m_segments.m_buffer;

                        Vector3 startPos = nodeBuffer[segmentBuffer[ms.id.NetSegment].m_startNode].m_position;
                        Vector3 endPos = nodeBuffer[segmentBuffer[ms.id.NetSegment].m_endNode].m_position;

                        action.mirrorPivot = ((endPos - startPos) / 2) + startPos;
                        action.mirrorAngle = -Mathf.Atan2(endPos.x - startPos.x, endPos.z - startPos.z);
                        action.followTerrain = followTerrain;

                        ActionQueue.instance.Push(action);
                        ActionQueue.instance.Do();

                        Singleton<SimulationManager>.instance.AddAction(() => ProcessMirror(action));
                    }

                    DeactivateTool();
                }
                else if (MT_Tool == MT_Tools.Inplace || MT_Tool == MT_Tools.Group)
                {
                    float angle;

                    if (m_hoverInstance is MoveableBuilding mb)
                    {
                        angle = Singleton<BuildingManager>.instance.m_buildings.m_buffer[mb.id.Building].m_angle;
                    }
                    else if (m_hoverInstance is MoveableProp mp)
                    {
                        angle = PropLayer.Manager.Buffer(mp.id).Angle;
                    }
                    else if (m_hoverInstance is MoveableProc mpo)
                    {
                        angle = PO.GetProcObj(mpo.id.NetLane).Angle;
                    }
                    else if (m_hoverInstance is MoveableSegment ms)
                    {
                        NetSegment[] segmentBuffer = Singleton<NetManager>.instance.m_segments.m_buffer;

                        Vector3 startPos = NetManager.instance.m_nodes.m_buffer[segmentBuffer[ms.id.NetSegment].m_startNode].m_position;
                        Vector3 endPos = NetManager.instance.m_nodes.m_buffer[segmentBuffer[ms.id.NetSegment].m_endNode].m_position;

                        angle = (float)Math.Atan2(endPos.z - startPos.z, endPos.x - startPos.x);
                    }
                    else
                    {
                        return;
                    }

                    // Add action to queue, also enables Undo/Redo
                    AlignRotationAction action;
                    switch (MT_Tool)
                    {
                        case MT_Tools.Group:
                            action = new AlignGroupAction();
                            break;

                        default:
                            action = new AlignIndividualAction();
                            break;
                    }
                    action.newAngle = angle;
                    action.followTerrain = followTerrain;
                    ActionQueue.instance.Push(action);
                    m_nextAction = ToolAction.Do;

                    DeactivateTool();
                }
                else if (MT_Tool == MT_Tools.Slope)
                {
                    if (m_hoverInstance == null) return;

                    AlignSlopeAction action;
                    switch (AlignToolPhase)
                    {
                        case 1: // Point A selected, prepare for Point B
                            AlignToolPhase++;
                            action = new AlignSlopeAction
                            {
                                PointA = m_hoverInstance
                            };
                            ActionQueue.instance.Push(action);
                            UIMoreTools.UpdateMoreTools();
                            break;

                        case 2: // Point B selected, fire action
                            AlignToolPhase++;
                            action = ActionQueue.instance.current as AlignSlopeAction;
                            action.PointB = m_hoverInstance;
                            action.followTerrain = followTerrain;
                            m_nextAction = ToolAction.Do;
                            DeactivateTool();
                            break;
                    }
                }
            }
            else if (ToolState == ToolStates.Picking)
            {
                if (m_hoverInstance == null) return;

                Filters.Picker = new PickerFilter(m_hoverInstance.Info.Prefab);
                Filters.SetFilter("Picker", true);
                UIFilters.UpdatePickerButton(1);

                foreach (UICheckBox cb in UIFilters.FilterCBs)
                {
                    if (cb.name != "Picker")
                    {
                        cb.isChecked = false;
                        Filters.SetFilter(cb.name, false);
                    }
                    else
                    {
                        cb.isChecked = true;
                    }
                }
                UIFilters.RefreshFilters();

                SetToolState();
            }
        }

        private void OnLeftDrag()
        {
            Log.Debug("OnLeftDrag: " + ToolState, "[M35]");

            if (ToolState == ToolStates.Default || ToolState == ToolStates.ToolActive)
            {
                if (m_lastInstance == null) return;

                TransformAction action;
                if (Action.selection.Contains(m_lastInstance))
                {
                    action = ActionQueue.instance.current as TransformAction;
                    if (action == null)
                    {
                        action = new TransformAction();
                        ActionQueue.instance.Push(action);
                    }
                }
                else
                {
                    ActionQueue.instance.Push(new SelectAction());
                    Action.selection.AddObject(m_lastInstance);

                    action = new TransformAction();
                    ActionQueue.instance.Push(action);
                }

                m_dragStartRelative = action.moveDelta;
                UpdateSensitivityMode();

                SetToolState(ToolStates.MouseDragging);
                action.InitialiseDrag();
            }
        }

        private void OnLeftDragStop()
        {
            Log.Debug("OnLeftDragStop: " + ToolState, "[M36]");

            if (ToolState == ToolStates.MouseDragging && m_rightClickTime == 0)
            {
                ProcessSensitivityMode(false);

                SetToolState();
                ((TransformAction)ActionQueue.instance.current).FinaliseDrag();

                UIToolOptionPanel.RefreshSnapButton();
            }
        }

        private void OnRightMouseDown()
        {
            Log.Debug("OnRightMouseDown: " + ToolState, "[M37]");

            if (ToolState == ToolStates.Default)
            {
                m_sensitivityTogglePosX = m_mouseStartX = Input.mousePosition.x;
                m_sensitivityAngleOffset = 0f;
            }
            else if (ToolState == ToolStates.MouseDragging)
            {
                TransformAction action = ActionQueue.instance.current as TransformAction;
                m_startAngle = action.angleDelta;

                m_sensitivityTogglePosX = m_mouseStartX = Input.mousePosition.x;
                m_sensitivityAngleOffset = 0f;
            }
            else if (ToolState == ToolStates.Cloning)
            {
                CloneActionBase action = ActionQueue.instance.current as CloneActionBase;
                m_startAngle = action.angleDelta;

                m_sensitivityTogglePosX = m_mouseStartX = Input.mousePosition.x;
                m_sensitivityAngleOffset = 0f;
            }
        }

        private void OnRightMouseUp()
        { }

        private void OnRightClick()
        {
            Log.Debug("OnRightClick: " + ToolState, "[M38]");

            if (ToolState == ToolStates.Default)
            {
                if (!(ActionQueue.instance.current is SelectAction))
                {
                    SelectAction action = new SelectAction();
                    ActionQueue.instance.Push(action);
                }
                else
                {
                    Action.selection.Clear();
                    ActionQueue.instance.Invalidate();
                }
                m_debugPanel.UpdatePanel();
            }
            else if (ToolState == ToolStates.Cloning)
            {
                if (Settings.rmbCancelsCloning.value)
                {
                    StopCloning();
                }
                else
                {
                    // Rotate 45° clockwise
                    CloneActionBase action = ActionQueue.instance.current as CloneActionBase;
                    action.angleDelta -= Mathf.PI / 4;
                }
            }
            else if (ToolState == ToolStates.Aligning || ToolState == ToolStates.ToolActive)
            {
                DeactivateTool();
            }
            else if (ToolState == ToolStates.Picking)
            {
                UIFilters.UpdatePickerButton(1);
                SetToolState();
            }
            else if (ToolState != ToolStates.MouseDragging)
            {
                SetToolState();
            }
        }

        private void OnRightDrag()
        {
            Log.Debug("OnRightDrag: " + ToolState, "[M39]");

            if (ToolState == ToolStates.Default || ToolState == ToolStates.ToolActive)
            {
                TransformAction action = ActionQueue.instance.current as TransformAction;
                if (action == null)
                {
                    if (!Action.HasSelection()) return;

                    action = new TransformAction();
                    ActionQueue.instance.Push(action);
                }

                m_startAngle = action.angleDelta;
                UpdateSensitivityMode();

                SetToolState(ToolStates.MouseDragging);
                action.InitialiseDrag();
            }
            else if (ToolState == ToolStates.Cloning)
            {
                SetToolState(ToolStates.RightDraggingClone);
            }
        }

        private void OnRightDragStop()
        {
            Log.Debug("OnRightDragStop: " + ToolState, "[M40]");

            if (ToolState == ToolStates.MouseDragging && m_leftClickTime == 0)
            {
                ProcessSensitivityMode(false);

                SetToolState();
                ((TransformAction)ActionQueue.instance.current).FinaliseDrag();

                UIToolOptionPanel.RefreshSnapButton();
            }
            else if (ToolState == ToolStates.RightDraggingClone)
            {
                SetToolState(ToolStates.Cloning);
            }
        }

        private void OnMiddleMouseDown()
        {
            Log.Debug("OnMiddleMouseDown: " + ToolState, "[M41]");

            Vector3 mousePos = RaycastMouseLocation();

            if (ToolState == ToolStates.Default)
            {
                m_lastInstance = m_hoverInstance;

                m_sensitivityTogglePosAbs = mousePos;
                m_clickPositionAbs = mousePos;
            }
            else if (ToolState == ToolStates.MouseDragging)
            {
                TransformAction action = ActionQueue.instance.current as TransformAction;
                m_dragStartRelative = action.moveDelta;
                UpdateSensitivityMode();

                m_sensitivityTogglePosAbs = mousePos;
                m_clickPositionAbs = mousePos;
            }
        }

        private void OnMiddleMouseUp()
        {
            //Log.Debug("OnMiddleMouseUp: " + ToolState, "[M42]");
        }

        private void OnMiddleClick()
        {
            //Log.Debug("OnMiddleClick: " + ToolState, "[M43]");
        }

        private void OnMiddleDrag()
        {
            //Log.Debug($"OnMiddleDrag: {ToolState}\nm_dragStartRelative:{m_dragStartRelative}\nm_sensitivityTogglePosAbs:{m_sensitivityTogglePosAbs}\nm_clickPositionAbs:{m_clickPositionAbs}", "[M44]");

            if (ToolState == ToolStates.Default)
            {
                if (!(ActionQueue.instance.current is TransformAction action))
                {
                    action = new TransformAction();
                    ActionQueue.instance.Push(action);
                }

                m_dragStartRelative = action.moveDelta;
                UpdateSensitivityMode();

                SetToolState(ToolStates.MouseDragging);
                action.InitialiseDrag();
            }
        }

        private void OnMiddleDragStop()
        {
            Log.Debug("OnMiddleDragStop: " + ToolState, "[M45]");

            if (ToolState == ToolStates.MouseDragging && m_rightClickTime == 0)
            {
                ProcessSensitivityMode(false);

                SetToolState();
                ((TransformAction)ActionQueue.instance.current).FinaliseDrag();

                UIToolOptionPanel.RefreshSnapButton();
            }
        }

        private void UpdateSensitivityMode()
        {
            if (Event.current.control)
            {
                if (!m_isLowSensitivity)
                {
                    ProcessSensitivityMode(true);
                }
            }
            else
            {
                if (m_isLowSensitivity)
                {
                    ProcessSensitivityMode(false);
                }
            }
        }

        internal void ProcessSensitivityMode(bool enable)
        {
            if (ActionQueue.instance.current is TransformAction || ActionQueue.instance.current is CloneAction)
            {
                if (enable)
                {
                    m_sensitivityTogglePosAbs = RaycastMouseLocation();
                    m_sensitivityTogglePosX = Input.mousePosition.x;
                }

                m_isLowSensitivity = enable;
            }
        }
    }
}
