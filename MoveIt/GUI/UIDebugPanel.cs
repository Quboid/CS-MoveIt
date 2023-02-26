using ColossalFramework;
using ColossalFramework.UI;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoveIt
{
    internal class DebugPanel : MonoBehaviour
    {
        internal UIPanel Panel;
        private UILabel HoverLarge, HoverSmall, ActionStatus, ToolStatus, SelectedLarge, SelectedSmall;
        private InstanceID id, lastId;

        internal DebugPanel()
        {
            _initialise();
        }

        internal void Visible(bool show)
        {
            Panel.isVisible = show;
        }

        internal void UpdatePanel(InstanceID instanceId)
        {
            id = instanceId;
            UpdatePanel();
        }

        internal void UpdatePanel()
        {
            if (!Settings.showDebugPanel)
            {
                return;
            }

            StartCoroutine(UpdateDo());
        }

        internal IEnumerator<object> UpdateDo()
        {
            yield return new WaitForSeconds(0.05f);
            ActionStatus.text = ActionQueue.instance.current == null ? "" : $"{ActionQueue.instance.current.GetType()}";
            ToolStatus.text = $"{MoveItTool.ToolState} ({MoveItTool.MT_Tool}.{MoveItTool.AlignToolPhase}), TM:{MoveItTool.TaskManager.Active} POProc:{MoveItTool.POProcessing}";

            SelectedLarge.text = $"Objects Selected: {Action.selection.Count}";
            ushort[] types = new ushort[8];
            foreach (Instance instance in Action.selection)
            {
                if (instance is MoveableBuilding)
                {
                    types[0]++;
                }
                else if (instance is MoveableProp)
                {
                    PropInfo info = (PropInfo)PropLayer.Manager.GetInfo(instance.id).Prefab;
                    if (info.m_isDecal)
                    {
                        types[2]++;
                    }
                    else if (Filters.IsSurface(info))
                    {
                        types[3]++;
                    }
                    else
                    {
                        types[1]++;
                    }
                }
                else if (instance is MoveableTree)
                {
                    types[4]++;
                }
                else if (instance is MoveableProc)
                {
                    types[5]++;
                }
                else if (instance is MoveableNode)
                {
                    types[6]++;
                }
                else if (instance is MoveableSegment)
                {
                    types[7]++;
                }
                else
                {
                    throw new Exception($"Instance is invalid type (<{instance.GetType()}>)");
                }
            }
            SelectedSmall.text = $"B:{types[0]}, P:{types[1]}, D:{types[2]}, S:{types[3]}, T:{types[4]}, PO:{types[5]}, N:{types[6]}, S:{types[7]}\n ";

            // End with updating the hovered item
            if (id == null)
            {
                yield break;
            }
            if (id == InstanceID.Empty)
            {
                lastId = id;
                HoverLarge.textColor = (MoveItTool.superSelect ? new Color32(255, 55, 55, 255) : new Color32(255, 255, 255, 255));
                yield break;
            }
            if (lastId == id)
            {
                yield break;
            }

            HoverLarge.textColor = new Color32(127, 217, 255, 255);
            HoverLarge.text = "";
            HoverSmall.text = "";

            if (id.Building > 0)
            {
                BuildingInfo info = BuildingManager.instance.m_buildings.m_buffer[id.Building].Info;
                HoverLarge.text = $"B:{id.Building}  {info.name}";
                HoverLarge.tooltip = info.name;
                HoverSmall.text = $"{info.GetType()} ({info.GetAI().GetType()})\n{info.m_class.name}\n({info.m_class.m_service}.{info.m_class.m_subService})";
            }
            else if (id.Prop > 0)
            {
                string type = "P";
                PropInfo info = (PropInfo)PropLayer.Manager.GetInfo(id).Prefab;
                if (info.m_isDecal) type = "D";
                HoverLarge.text = $"{type}:{PropLayer.Manager.GetId(id)}  {info.name}";
                HoverLarge.tooltip = info.name;
                HoverSmall.text = $"{info.GetType()}\n{info.m_class.name}";
            }
            else if (id.NetLane > 0)
            {
                IInfo info = MoveItTool.PO.GetProcObj(id.NetLane).Info;
                HoverLarge.text = $"PO:{id.NetLane}  {info.Name}";
                HoverLarge.tooltip = info.Name;
                HoverSmall.text = $"\n";
            }
            else if (id.Tree > 0)
            {
                TreeInfo info = TreeManager.instance.m_trees.m_buffer[id.Tree].Info;
                HoverLarge.text = $"T:{id.Tree}  {info.name}";
                HoverLarge.tooltip = info.name;
                HoverSmall.text = $"{info.GetType()}\n{info.m_class.name}";
            }
            else if (id.NetNode > 0)
            {
                NetInfo info = NetManager.instance.m_nodes.m_buffer[id.NetNode].Info;
                HoverLarge.text = $"N:{id.NetNode}  {info.name}";
                HoverLarge.tooltip = info.name;
                HoverSmall.text = $"{info.GetType()} ({info.GetAI().GetType()})\n{info.m_class.name}";
            }
            else if (id.NetSegment > 0)
            {
                NetInfo info = NetManager.instance.m_segments.m_buffer[id.NetSegment].Info;
                HoverLarge.text = $"S:{id.NetSegment}  {info.name}";
                HoverLarge.tooltip = info.name;
                HoverSmall.text = $"{info.GetType()} ({info.GetAI().GetType()})\n{info.m_class.name}";
            }

            lastId = id;
        }

        private void _initialise()
        {
            Panel = UIView.GetAView().AddUIComponent(typeof(UIPanel)) as UIPanel;
            Panel.name = "MoveIt_DebugPanel";
            Panel.atlas = ResourceLoader.GetAtlas("Ingame");
            Panel.backgroundSprite = "SubcategoriesPanel";
            Panel.size = new Vector2(300, 120);
            Panel.absolutePosition = new Vector3(Panel.GetUIView().GetScreenResolution().x - 422, 3);
            Panel.clipChildren = true;
            Panel.isVisible = Settings.showDebugPanel;

            HoverLarge = Panel.AddUIComponent<UILabel>();
            HoverLarge.textScale = 0.8f;
            HoverLarge.text = "None";
            HoverLarge.relativePosition = new Vector3(6, 7);
            HoverLarge.width = HoverLarge.parent.width - 20;
            HoverLarge.clipChildren = true;
            HoverLarge.useDropShadow = true;
            HoverLarge.dropShadowOffset = new Vector2(2, -2);
            HoverLarge.eventClick += (c, p) =>
            {
                Clipboard.text = HoverLarge.tooltip;
            };

            HoverSmall = Panel.AddUIComponent<UILabel>();
            HoverSmall.textScale = 0.6f;
            HoverSmall.text = "No item being hovered\n ";
            HoverSmall.relativePosition = new Vector3(5, HoverLarge.relativePosition.y + 16);
            HoverSmall.width = HoverSmall.parent.width - 20;
            HoverSmall.clipChildren = true;
            HoverSmall.useDropShadow = true;
            HoverSmall.dropShadowOffset = new Vector2(1, -1);

            ActionStatus = Panel.AddUIComponent<UILabel>();
            ActionStatus.textScale = 0.6f;
            ActionStatus.text = "";
            ActionStatus.relativePosition = new Vector3(5, HoverSmall.relativePosition.y + 40);
            ActionStatus.width = ActionStatus.parent.width - 20;
            ActionStatus.clipChildren = true;
            ActionStatus.useDropShadow = true;
            ActionStatus.dropShadowOffset = new Vector2(1, -1);

            ToolStatus = Panel.AddUIComponent<UILabel>();
            ToolStatus.textScale = 0.6f;
            ToolStatus.text = "";
            ToolStatus.relativePosition = new Vector3(5, ActionStatus.relativePosition.y + 13);
            ToolStatus.width = ToolStatus.parent.width - 20;
            ToolStatus.clipChildren = true;
            ToolStatus.useDropShadow = true;
            ToolStatus.dropShadowOffset = new Vector2(1, -1);

            SelectedLarge = Panel.AddUIComponent<UILabel>();
            SelectedLarge.textScale = 0.8f;
            SelectedLarge.text = "Objects Selected: 0";
            SelectedLarge.relativePosition = new Vector3(6, ToolStatus.relativePosition.y + 16);
            SelectedLarge.width = SelectedLarge.parent.width - 20;
            SelectedLarge.clipChildren = true;
            SelectedLarge.useDropShadow = true;
            SelectedLarge.dropShadowOffset = new Vector2(2, -2);

            SelectedSmall = Panel.AddUIComponent<UILabel>();
            SelectedSmall.textScale = 0.6f;
            SelectedSmall.text = "B:0, P:0, D:0, S:0, T:0, PO:0, N:0, S:0\n ";
            SelectedSmall.relativePosition = new Vector3(5, SelectedLarge.relativePosition.y + 15);
            SelectedSmall.width = SelectedSmall.parent.width - 20;
            SelectedSmall.clipChildren = true;
            SelectedSmall.useDropShadow = true;
            SelectedSmall.dropShadowOffset = new Vector2(1, -1);
        }
    }
}
