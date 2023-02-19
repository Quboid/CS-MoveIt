using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.IO;
using ColossalFramework.Plugins;
using ColossalFramework.UI;
using MoveItIntegration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Serialization;
using UnityEngine;

namespace MoveIt
{
    public partial class MoveItTool : ToolBase
    {
        public static void SetToolState(ToolStates state = ToolStates.Default, MT_Tools tool = MT_Tools.Off, ushort toolPhase = 0)
        {
            ToolStates previousState = ToolState;
            if (ToolState != state)
            {
                if (state != ToolStates.ToolActive && state != ToolStates.Aligning)
                {
                    UIMoreTools.m_activeToolMenu = null;
                }

                if (ToolState == ToolStates.ToolActive)
                {
                    if (MT_Tool == MT_Tools.MoveTo)
                    {
                        m_moveToPanel.Visible(false);
                    }
                }
            }

            ToolState = state;
            m_toolsMode = tool;
            m_alignToolPhase = toolPhase;

            if (state == ToolStates.ToolActive || state == ToolStates.Aligning || previousState == ToolStates.ToolActive || previousState == ToolStates.Aligning)
            {
                UIMoreTools.UpdateMoreTools();
            }
            m_debugPanel?.UpdatePanel();
        }

        public void ProcessAligning(MT_Tools mode)
        {
            if (ToolState == ToolStates.Aligning && MT_Tool == mode)
            {
                StopTool();
            }
            else
            {
                StartTool(ToolStates.Aligning, mode);
            }
        }

        public bool StartTool(ToolStates newToolState, MT_Tools mode)
        {
            if (ToolState == ToolStates.Cloning || ToolState == ToolStates.RightDraggingClone)
            {
                StopCloning();
            }

            if (ToolState != ToolStates.Default && ToolState != ToolStates.Aligning && ToolState != ToolStates.ToolActive) return false;

            if (!Action.HasSelection()) return false;

            SetToolState(newToolState, mode, 1);
            UIMoreTools.CheckCloseMenu();
            return true;
        }

        // Called when a tool might not be active
        public void StopTool()
        {
            if (ToolState != ToolStates.Aligning && ToolState != ToolStates.ToolActive) return;

            DeactivateTool();
        }

        public bool DeactivateTool()
        {
            if (MT_Tool == MT_Tools.MoveTo)
            {
                m_moveToPanel.Visible(false);
            }

            SetToolState();
            Action.UpdateArea(Action.GetTotalBounds(false));
            return false;
        }

        public void StartCloning()
        {
            lock (ActionQueue.instance)
            {
                if (ToolState != ToolStates.Default && ToolState != ToolStates.Aligning) return;

                if (Action.HasSelection())
                {
                    CloneAction action = new CloneAction();// GameObject("MIT_CloneAction").AddComponent<CloneAction>();

                    if (action.Count > 0)
                    {
                        UpdateSensitivityMode();

                        m_sensitivityTogglePosAbs = m_clickPositionAbs = action.center;

                        ActionQueue.instance.Push(action);

                        SetToolState(ToolStates.Cloning);
                        UIToolOptionPanel.RefreshCloneButton();
                        UIToolOptionPanel.RefreshAlignHeightButton();
                    }
                }
            }
        }

        public void StopCloning()
        {
            lock (ActionQueue.instance)
            {
                if (ToolState == ToolStates.Cloning || ToolState == ToolStates.RightDraggingClone)
                {
                    ProcessSensitivityMode(false);

                    ActionQueue.instance.Undo();
                    ActionQueue.instance.Invalidate();
                    SetToolState();

                    UIToolOptionPanel.RefreshCloneButton();
                }
            }
        }

        public void StartBulldoze()
        {
            if (ToolState != ToolStates.Default) return;

            if (Action.HasSelection())
            {
                lock (ActionQueue.instance)
                {
                    ActionQueue.instance.Push(new BulldozeAction());
                }
                m_nextAction = ToolAction.Do;
            }
        }

        public void StartReset()
        {
            if (ToolState != ToolStates.Default) return;

            if (Action.HasSelection())
            {
                lock (ActionQueue.instance)
                {
                    ActionQueue.instance.Push(new ResetAction());
                }
                m_nextAction = ToolAction.Do;
            }
        }

        /// <summary>
        /// Lets other mods paste an object via Move It, e.g. Find It placing selected
        /// object with Move It rather than default tool.
        /// </summary>
        /// <param name="prefab">The prefab to paste</param>
        /// <returns></returns>
        public static bool PasteFromExternal(PrefabInfo prefab)
        {
            if (prefab == null)
            {
                Log.Info($"PasteFromExternal prefab is null!", "[M09]");
            }

            if (prefab is NetInfo)
            {
                return false;
            }

            CloneActionFindIt action = new CloneActionFindIt(prefab);

            if (action.Count > 0)
            {
                ActionQueue.instance.Push(action);

                SetToolState(ToolStates.Cloning);

                UIToolOptionPanel.RefreshCloneButton();
                UIToolOptionPanel.RefreshAlignHeightButton();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Method for other mods (e.g. Picker) to hook when cloning a single object
        /// </summary>
        /// <param name="prefab">The PrefabInfo of the object</param>
        public static void CloneSingleObject(PrefabInfo prefab)
        { }
    }
}
