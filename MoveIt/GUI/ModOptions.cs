using ColossalFramework.UI;
using ICities;
using MoveIt.Lang;
using System;
using System.Diagnostics;
using UnityEngine;

namespace MoveIt.GUI
{
    public class ModOptions
    {
        public static string URLPatreon { get; } = "https://www.patreon.com/Quboid";
        public static string URLPaypal { get; } = "https://www.paypal.me/QuboidCSL1";
        public static string URLCoffee { get; } = "https://www.buymeacoffee.com/Quboid";

        public ModOptions(UIHelperBase helper, string name)
        {
            try
            {
                UIHelperBase group = helper.AddGroup(name);
                UIPanel panel = ((UIHelper)group).self as UIPanel;

                UILabel sellout = panel.AddUIComponent<UILabel>();
                sellout.name = "sellout";
                sellout.textScale = 1.2f;
                sellout.text = Str.options_Beg;

                UIButton button = (UIButton)group.AddButton("Buy Me A Coffee", () => OpenUrl(URLCoffee));
                button.autoSize = false;
                button.textHorizontalAlignment = UIHorizontalAlignment.Center;
                button.size = new Vector2(250, 40);
                button.tooltip = "Buy Me A Coffee";

                button = (UIButton)group.AddButton(Str.options_Patreon, () => OpenUrl(URLPatreon));
                button.autoSize = false;
                button.textHorizontalAlignment = UIHorizontalAlignment.Center;
                button.size = new Vector2(250, 40);
                button.tooltip = Str.options_PatreonTooltip;

                button = (UIButton)group.AddButton(Str.options_Paypal, () => OpenUrl(URLPaypal));
                button.autoSize = false;
                button.textHorizontalAlignment = UIHorizontalAlignment.Center;
                button.size = new Vector2(250, 40);
                button.tooltip = Str.options_PaypalTooltip;

                group.AddSpace(10);

                group = helper.AddGroup(Str.options_Options);

                UICheckBox checkBox = (UICheckBox)group.AddCheckbox(Str.options_AutoCloseToolbox, Settings.autoCloseAlignTools.value, (b) =>
                {
                    Settings.autoCloseAlignTools.value = b;
                    if (UIMoreTools.MoreToolsPanel != null)
                    {
                        UIMoreTools.CloseMenu();
                    }
                });
                checkBox.tooltip = Str.options_AutoCloseToolbox_Tooltip;

                group.AddSpace(10);

                checkBox = (UICheckBox)group.AddCheckbox(Str.options_PreferFastmove, Settings.fastMove.value, (b) =>
                {
                    Settings.fastMove.value = b;
                });
                checkBox.tooltip = Str.options_PreferFastmove_Tooltip;

                group.AddSpace(10);

                checkBox = (UICheckBox)group.AddCheckbox(Str.options_UseCompass, Settings.useCardinalMoves.value, (b) =>
                {
                    Settings.useCardinalMoves.value = b;
                });
                checkBox.tooltip = Str.options_UseCompass_Tooltip;

                group.AddSpace(10);

                checkBox = (UICheckBox)group.AddCheckbox(Str.options_RightClickCancel, Settings.rmbCancelsCloning.value, (b) =>
                {
                    Settings.rmbCancelsCloning.value = b;
                });
                checkBox.tooltip = Str.options_RightClickCancel_Tooltip;

                group.AddSpace(10);

                checkBox = (UICheckBox)group.AddCheckbox(Str.options_AdvancedPillarControl, Settings.advancedPillarControl.value, (b) =>
                {
                    Settings.advancedPillarControl.value = b;
                });
                checkBox.tooltip = Str.options_AdvancedPillarControl_Tooltip;

                group.AddSpace(10);

                checkBox = (UICheckBox)group.AddCheckbox(Str.options_AltForPillars, Settings.altSelectNodeBuildings.value, (b) =>
                {
                    Settings.altSelectNodeBuildings.value = b;
                });

                group.AddSpace(10);

                checkBox = (UICheckBox)group.AddCheckbox(Str.options_UseUUI, Settings.useUUI.value, (b) =>
                {
                    Settings.useUUI.value = b;
                    MoveItTool.instance?.DisableUUI();
                    MoveItTool.instance?.EnableUUI();
                });

                group.AddSpace(10);
                group = helper.AddGroup(Str.options_ShortcutsGeneral);
                panel = ((UIHelper)group).self as UIPanel;
                group.AddSpace(10);

                ((UIPanel)((UIHelper)group).self).gameObject.AddComponent<OptionsKeymappingMain>();

                group.AddSpace(10);
                group = helper.AddGroup(Str.options_ShortcutsToolbox);
                panel = ((UIHelper)group).self as UIPanel;
                group.AddSpace(10);

                ((UIPanel)((UIHelper)group).self).gameObject.AddComponent<OptionsKeymappingToolbox>();

                group.AddSpace(10);
                group = helper.AddGroup(Str.options_ExtraOptions);
                panel = ((UIHelper)group).self as UIPanel;
                group.AddSpace(10);

                button = (UIButton)group.AddButton(Str.options_RemoveGhostNodes, Utils.CleanGhostNodes);
                button.tooltip = Str.options_RemoveGhostNodes_Tooltip;

                button = (UIButton)group.AddButton(Str.options_fixTreeSnapping, Utils.FixTreeFixedHeightFlag);
                button.tooltip = Str.options_fixTreeSnappingTooltip;

                group.AddSpace(10);

                button = (UIButton)group.AddButton(Str.options_ResetButtonPosition, () =>
                {
                    UIMoveItButton.savedX.value = -1000;
                    UIMoveItButton.savedY.value = -1000;
                    MoveItTool.instance?.m_button?.ResetPosition();
                });

                group.AddSpace(20);

                checkBox = (UICheckBox)group.AddCheckbox(Str.options_superSelect, MoveItTool.superSelect, (b) =>
                {
                    MoveItTool.superSelect = b;
                });
                checkBox.tooltip = Str.options_superSelect_Tooltip;

                checkBox = (UICheckBox)group.AddCheckbox(Str.options_DisableDebugLogging, DebugUtils.hideDebugMessages.value, (b) =>
                {
                    DebugUtils.hideDebugMessages.value = b;
                });
                checkBox.tooltip = Str.options_DisableDebugLogging_Tooltip;

                checkBox = (UICheckBox)group.AddCheckbox(Str.options_ShowDebugPanel, Settings.showDebugPanel.value, (b) =>
                {
                    Settings.showDebugPanel.value = b;
                    DebugPanel.instance?.Visible(b);
                });
                checkBox.name = "MoveIt_DebugPanel";

                group.AddSpace(5);
                UILabel emlLabel = panel.AddUIComponent<UILabel>();
                emlLabel.name = "emlLabel";
                emlLabel.text = PropLayer.getVersionText();

                UILabel nsLabel = panel.AddUIComponent<UILabel>();
                nsLabel.name = "nsLabel";
                nsLabel.text = NS_Manager.getVersionText();

                group = helper.AddGroup(Str.options_ProceduralObjects);
                panel = ((UIHelper)group).self as UIPanel;

                UILabel poLabel = panel.AddUIComponent<UILabel>();
                poLabel.name = "poLabel";
                poLabel.text = PO_Manager.getVersionText();

                // TODO add users of MoveITIntegration.dll here by name/description

                UILabel poWarning = panel.AddUIComponent<UILabel>();
                poWarning.name = "poWarning";
                poWarning.padding = new RectOffset(25, 0, 0, 15);
                poWarning.text = Str.options_PODeleteWarning;

                checkBox = (UICheckBox)group.AddCheckbox(Str.options_HidePODeletionWarning, !Settings.POShowDeleteWarning.value, (b) =>
                {
                    Settings.POShowDeleteWarning.value = !b;
                });

                group.AddSpace(15);

                panel.gameObject.AddComponent<OptionsKeymappingPO>();

                group.AddSpace(15);

            }
            catch (Exception e)
            {
                UnityEngine.Debug.Log("Move It OnSettingsUI failed");
                UnityEngine.Debug.LogException(e);
            }
        }

        public static void OpenUrl(string url)
        {
            Process.Start(url);
        }
    }
}
