using ColossalFramework.UI;
using System.Collections.Generic;
using UnityEngine;

namespace MoveIt
{
    class UIFilters
    {
        public static List<UICheckBox> NetworkCBs = new List<UICheckBox>();
        public static List<UICheckBox> FilterCBs = new List<UICheckBox>();
        public static UIButton ToggleNF;
        public static UIPanel FilterPanel;
        public static Color32 TextColor = new Color32(175, 216, 235, 255);
        public static Color32 ActiveLabelColor = new Color32(255, 255, 255, 255);
        public static Color32 InactiveLabelColor = new Color32(170, 170, 175, 255);

        public static UITextureAtlas GetIconsAtlas()
        {
            Texture2D[] textures =
            {
                UIToolOptionPanel.instance.atlas["OptionBase"].texture,
                UIToolOptionPanel.instance.atlas["OptionBaseHovered"].texture,
                UIToolOptionPanel.instance.atlas["OptionBasePressed"].texture,
                UIToolOptionPanel.instance.atlas["OptionBaseDisabled"].texture,
                UIToolOptionPanel.instance.atlas["OptionBaseFocused"].texture
            };

            string[] spriteNames = new string[]
            {
                "NFExpand",
                "NFExpandHover",
                "NFCollapse",
                "NFCollapseHover"
            };

            UITextureAtlas loadedAtlas = ResourceLoader.CreateTextureAtlas("MoveIt_NFBtn", spriteNames, "MoveIt.Icons.");
            ResourceLoader.AddTexturesInAtlas(loadedAtlas, textures);

            return loadedAtlas;
        }

        public static UIButton CreateToggleNFBtn()
        {
            ToggleNF = SamsamTS.UIUtils.CreateButton(FilterPanel.Find<UIPanel>("m_filtersPanelList"));
            ToggleNF.textHorizontalAlignment = UIHorizontalAlignment.Center;
            ToggleNF.textColor = TextColor;
            ToggleNF.disabledTextColor = TextColor;
            ToggleNF.focusedTextColor = TextColor;
            ToggleNF.pressedTextColor = TextColor;
            ToggleNF.autoSize = false;
            ToggleNF.width = 130f;
            ToggleNF.height = 16f;
            ToggleNF.horizontalAlignment = UIHorizontalAlignment.Center;
            ToggleNF.relativePosition = new Vector2(10f, 0f);
            ToggleNF.atlas = GetIconsAtlas();
            ToggleNF.normalBgSprite = null;
            ToggleNF.hoveredBgSprite = null;
            ToggleNF.pressedBgSprite = null;
            ToggleNF.disabledBgSprite = null;
            ToggleNF.normalFgSprite = "NFExpand";
            ToggleNF.hoveredFgSprite = "NFExpandHover";
            ToggleNF.tooltip = "Network Filters";
            ToggleNF.eventClicked += (c, p) =>
            {
                ToggleNetworkFiltersPanel();
            };

            _updateToggleNFBtn();
            return ToggleNF;
        }

        private static void _updateToggleNFBtn()
        {
            if (MoveItTool.filterNetworks)
            { // Network Filters visible
                ToggleNF.normalFgSprite = "NFCollapse";
                ToggleNF.hoveredFgSprite = "NFCollapseHover";
            }
            else
            { // Network Filters hidden
                ToggleNF.normalFgSprite = "NFExpand";
                ToggleNF.hoveredFgSprite = "NFExpandHover";
            }
        }

        public static void ToggleNetworkFiltersPanel()
        {
            MoveItTool.filterNetworks = !MoveItTool.filterNetworks;
            int filterRows = Filters.NetworkFilters.Count;

            if (MoveItTool.filterNetworks)
            {
                foreach (UICheckBox cb in NetworkCBs)
                {
                    if (cb != null)
                    {
                        cb.isVisible = true;
                    }
                }

                FilterPanel.height += MoveItTool.UI_Filter_CB_Height * filterRows;
                FilterPanel.absolutePosition += new Vector3(0f, 0 - (MoveItTool.UI_Filter_CB_Height * filterRows));
                _updateToggleNFBtn();
            }
            else
            {
                foreach (UICheckBox cb in NetworkCBs)
                {
                    if (cb != null)
                    {
                        cb.isVisible = false;
                    }
                }

                FilterPanel.height -= MoveItTool.UI_Filter_CB_Height * filterRows;
                FilterPanel.absolutePosition -= new Vector3(0f, 0 - (MoveItTool.UI_Filter_CB_Height * filterRows));
                _updateToggleNFBtn();
            }

            RefreshFilters();
        }

        public static UICheckBox CreateFilterCB(UIComponent parent, string name, string label = null, bool isChecked = true)
        {
            UICheckBox checkBox = _createFilterCB(parent, name, label, isChecked);
            checkBox.isVisible = true;
            checkBox.eventClicked += (c, p) =>
            {
                Filters.ToggleFilter(name);
                RefreshFilters();
            };
            FilterCBs.Add(checkBox);
            return checkBox;
        }

        public static UICheckBox CreateNetworkFilterCB(UIComponent parent, string name, string label = null, bool isChecked = true)
        {
            UICheckBox checkBox = _createFilterCB(parent, name, label, isChecked);
            checkBox.isVisible = false;
            checkBox.eventClicked += (c, p) =>
            {
                Filters.ToggleNetworkFilter(name);
                RefreshFilters();
            };
            NetworkCBs.Add(checkBox);
            return checkBox;
        }

        private static UICheckBox _createFilterCB(UIComponent parent, string name, string label, bool isChecked)
        {
            if (label == null) label = name;
            UICheckBox checkBox = SamsamTS.UIUtils.CreateCheckBox(parent);
            checkBox.label.text = label;
            checkBox.name = name;
            checkBox.isChecked = isChecked;
            checkBox.size = new Vector2(130, 20);
            return checkBox;
        }

        public static void UpdatePickerLabel(string text, string tooltip, Color32 color, bool? isChecked = null)
        {
            UICheckBox checkBox = FilterCBs.Find(cb => cb.name == "Picker");

            if (checkBox == null)
                return;

            checkBox.label.text = text;
            checkBox.label.textColor = color;
            checkBox.tooltip = tooltip;
            if (isChecked == false)
                checkBox.isChecked = false;
            else if (isChecked == true)
                checkBox.isChecked = true;
        }

        public static void UpdatePickerButton(byte state)
        {
            switch (state)
            {
                case 1:
                    UIToolOptionPanel.instance.m_picker.normalBgSprite = "OptionsDropboxListbox";
                    break;
                case 2: // Active (picking object)
                    UIToolOptionPanel.instance.m_picker.normalBgSprite = "OptionsDropboxListboxHovered";
                    break;
            }
        }

        internal static void POToggled()
        {
            UICheckBox cbProcedural = FilterCBs.Find(cb => cb.name == "PO");

            if (MoveItTool.PO.Active)
            {
                FilterPanel.height += MoveItTool.UI_Filter_CB_Height;
                FilterPanel.absolutePosition += new Vector3(0f, 0 - MoveItTool.UI_Filter_CB_Height);
                cbProcedural.isVisible = true;
            }
            else
            {
                FilterPanel.height -= MoveItTool.UI_Filter_CB_Height;
                FilterPanel.absolutePosition -= new Vector3(0f, 0 - MoveItTool.UI_Filter_CB_Height);
                cbProcedural.isVisible = false;
            }

            RefreshFilters();
        }

        public static void RefreshFilters()
        {
            UICheckBox cbNodes = FilterCBs.Find(cb => cb.name == "Nodes");
            UICheckBox cbSegments = FilterCBs.Find(cb => cb.name == "Segments");

            if (MoveItTool.filterNodes || MoveItTool.filterSegments)
            {
                foreach (UICheckBox cb in NetworkCBs)
                {
                    cb.label.textColor = ActiveLabelColor;
                }
            }
            else
            {
                foreach (UICheckBox cb in NetworkCBs)
                {
                    cb.label.textColor = InactiveLabelColor;
                }
            }

            cbNodes.label.textColor = ActiveLabelColor;
            cbSegments.label.textColor = ActiveLabelColor;
            if (MoveItTool.filterNetworks)
            {
                bool active = false;
                foreach (NetworkFilter nf in Filters.NetworkFilters.Values)
                {
                    if (nf.enabled)
                    {
                        active = true;
                        break;
                    }
                }
                if (!active)
                {
                    cbNodes.label.textColor = InactiveLabelColor;
                    cbSegments.label.textColor = InactiveLabelColor;
                }
            }
        }
    }
}
