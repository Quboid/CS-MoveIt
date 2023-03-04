using ColossalFramework;
using ColossalFramework.UI;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoveIt
{
    internal class DebugPanel : MonoBehaviour
    {
        internal static DebugPanel instance;

        internal static DebugPanel Initialise()
        {
            var go = new GameObject("MIT_DebugPanel");
            go.AddComponent<DebugPanel>();
            instance = go.GetComponent<DebugPanel>();
            instance.Init();

            return instance;
        }

        internal static void Close()
        {
            Destroy(instance.gameObject);
            instance = null;
        }


        internal UIPanel Panel;
        private UILabel HoverLarge, HoverSmall, ActionStatus, ToolStatus, SelectedLarge, SelectedSmall;
        private InstanceID HoveredIId, lastIId;
        private ushort ticker;

        internal void Init()
        {
            Create();
            ticker = 0;
        }

        internal void Visible(bool show)
        {
            Panel.isVisible = show;
        }

        internal void SetHovered(InstanceID instanceId)
        {
            HoveredIId = instanceId;
        }

        internal void Update()
        {
            if (!Settings.showDebugPanel)
            {
                return;
            }
            if (ticker++ > 0)
            {
                if (ticker > 5) ticker = 0;
                return;
            }

            ActionStatus.text = ActionQueue.instance.current == null ? "" : $"{ActionQueue.instance.current.GetType()}";
            ToolStatus.text = $"{MoveItTool.ToolState} ({MoveItTool.MT_Tool}.{MoveItTool.AlignToolPhase}), TM:{MoveItTool.TaskManager.Active}";

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
            if (HoveredIId == null)
            {
                return;
            }
            if (HoveredIId == InstanceID.Empty)
            {
                lastIId = HoveredIId;
                HoverLarge.textColor = (MoveItTool.superSelect ? new Color32(255, 55, 55, 255) : new Color32(255, 255, 255, 255));
                return;
            }
            if (lastIId == HoveredIId)
            {
                return;
            }

            HoverLarge.textColor = new Color32(127, 217, 255, 255);
            HoverLarge.text = "";
            HoverSmall.text = "";

            switch (HoveredIId.Type)
            {
                case InstanceType.Building:
                    BuildingInfo info1 = BuildingManager.instance.m_buildings.m_buffer[HoveredIId.Building].Info;
                    HoverLarge.text = $"B:{HoveredIId.Building}  {info1.name}";
                    HoverLarge.tooltip = info1.name;
                    HoverSmall.text = $"{info1.GetType()} ({info1.GetAI().GetType()})\n{info1.m_class.name}\n({info1.m_class.m_service}.{info1.m_class.m_subService})";
                    break;

                case InstanceType.Prop:
                    string type = "P";
                    PropInfo info2 = (PropInfo)PropLayer.Manager.GetInfo(HoveredIId).Prefab;
                    if (info2.m_isDecal) type = "D";
                    HoverLarge.text = $"{type}:{PropLayer.Manager.GetId(HoveredIId)}  {info2.name}";
                    HoverLarge.tooltip = info2.name;
                    HoverSmall.text = $"{info2.GetType()}\n{info2.m_class.name}";
                    break;

                case InstanceType.NetLane:
                    IInfo info3 = MoveItTool.PO.GetProcObj(HoveredIId.NetLane).Info;
                    HoverLarge.text = $"PO:{HoveredIId.NetLane}  {info3.Name}";
                    HoverLarge.tooltip = info3.Name;
                    HoverSmall.text = $"\n";
                    break;

                case InstanceType.Tree:
                    TreeInfo info4 = TreeManager.instance.m_trees.m_buffer[HoveredIId.Tree].Info;
                    HoverLarge.text = $"T:{HoveredIId.Tree}  {info4.name}";
                    HoverLarge.tooltip = info4.name;
                    HoverSmall.text = $"{info4.GetType()}\n{info4.m_class.name}";
                    break;

                case InstanceType.NetNode:
                    NetInfo info5 = NetManager.instance.m_nodes.m_buffer[HoveredIId.NetNode].Info;
                    HoverLarge.text = $"N:{HoveredIId.NetNode}  {info5.name}";
                    HoverLarge.tooltip = info5.name;
                    HoverSmall.text = $"{info5.GetType()} ({info5.GetAI().GetType()})\n{info5.m_class.name}";
                    break;

                case InstanceType.NetSegment:
                    NetInfo info6 = NetManager.instance.m_segments.m_buffer[HoveredIId.NetSegment].Info;
                    HoverLarge.text = $"S:{HoveredIId.NetSegment}  {info6.name}";
                    HoverLarge.tooltip = info6.name;
                    HoverSmall.text = $"{info6.GetType()} ({info6.GetAI().GetType()})\n{info6.m_class.name}";
                    break;
            }

            lastIId = HoveredIId;
        }

        private void Create()
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
