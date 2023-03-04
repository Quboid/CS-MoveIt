using ColossalFramework;
using ColossalFramework.Globalization;
using ColossalFramework.Plugins;
using ICities;
using MoveIt.Lang;
using QCommonLib.QTasks;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MoveIt
{
    public class MoveItLoader : LoadingExtensionBase
    {
        public static bool IsGameLoaded { get; private set; } = false;
        public static LoadMode loadMode;
        private static GameObject MoveToToolObject, TaskManagerObject;

        public override void OnLevelLoaded(LoadMode mode)
        {
            loadMode = mode;
            InstallMod();
        }

        public override void OnLevelUnloading()
        {
            UninstallMod();
        }

        public static void InstallMod()
        {
            if (MoveItTool.instance == null)
            {
                // Creating the instance
                ToolController toolController = UnityEngine.Object.FindObjectOfType<ToolController>();

                MoveItTool.instance = toolController.gameObject.AddComponent<MoveItTool>();
            }
            else
            {
                Log.Error($"InstallMod with existing instance!", "[M53]");
            }

            Log.IsDebug = true;
            Directory.CreateDirectory(MoveItTool.saveFolder);

            MoveItTool.stepOver = new StepOver();

            MoveToToolObject = new GameObject("MIT_MoveToPanel");
            MoveToToolObject.AddComponent<MoveToPanel>();
            MoveItTool.m_moveToPanel = MoveToToolObject.GetComponent<MoveToPanel>();

            TaskManagerObject = new GameObject("MIT_TaskManager");
            TaskManagerObject.AddComponent<QTaskManager>();
            MoveItTool.TaskManager = TaskManagerObject.GetComponent<QTaskManager>();
            MoveItTool.TaskManager.Log = Log.instance;

            UIFilters.FilterCBs.Clear();
            UIFilters.NetworkCBs.Clear();

            Filters.Picker = new PickerFilter();

            MoveItTool.filterBuildings = true;
            MoveItTool.filterProps = true;
            MoveItTool.filterDecals = true;
            MoveItTool.filterSurfaces = true;
            MoveItTool.filterTrees = true;
            MoveItTool.filterNodes = true;
            MoveItTool.filterSegments = true;
            MoveItTool.filterNetworks = false;

            IsGameLoaded = true;

            // Touch each prop to ensure lights are functional
            try
            {
                PropLayer.Manager.TouchProps();
            }
            catch (Exception e)
            {
                Log.Error(e, "[M54]");
            }
        }

        public static void UninstallMod()
        {
            if (ToolsModifierControl.toolController.CurrentTool is MoveItTool)
                ToolsModifierControl.SetTool<DefaultTool>();

            ActionQueue.instance?.CleanQueue();
            UnityEngine.Object.Destroy(MoveToToolObject);
            UnityEngine.Object.Destroy(TaskManagerObject);
            WaitCursor.Close();
            DebugPanel.Close();
            if (PO_Manager.gameObject != null)
            {
                UnityEngine.Object.Destroy(PO_Manager.gameObject);
            }
            UIToolOptionPanel.instance = null;
            UIMoreTools.MoreToolsPanel = null;
            UIMoreTools.MoreToolsBtn = null;
            MoveItTool.TaskManager = null;
            Action.selection.Clear();
            Filters.Picker = null;
            MoveItTool.PO = null;
            UnityEngine.Object.Destroy(MoveItTool.instance.m_button);

            GUI.XMLWindow.Close();

            // Unified UI
            MoveItTool.instance.DisableUUI();

            if (MoveItTool.instance != null)
            {
                MoveItTool.instance.enabled = false;
                MoveItTool.instance = null;
            }

            IsGameLoaded = false;

            LocaleManager.eventLocaleChanged -= LocaleChanged;
        }

        internal static void LocaleChanged()
        {
            Log.Debug($"Move It Locale changed {Str.Culture?.Name}->{ModInfo.Culture.Name} ({SingletonLite<LocaleManager>.instance.language})", "[M55]");
            Str.Culture = ModInfo.Culture;
        }
    }

    /// <summary>
    /// Used by Move It to find integrated mods
    /// </summary>
    public static class IntegrationHelper
    {
        /// <summary>
        /// Search for mods with Move It integration (assemblies which contain <see cref="MoveItIntegrationBase"/> implementations
        /// </summary>
        /// <returns>List of <see cref="MoveItIntegrationBase"/> instances, one from each integrationed mod</returns>
        public static List<MoveItIntegration.MoveItIntegrationBase> GetIntegrations()
        {
            var integrations = new List<MoveItIntegration.MoveItIntegrationBase>();

            foreach (var mod in PluginManager.instance.GetPluginsInfo())
            {
                if (!mod.isEnabled) continue;

                foreach (var assembly in mod.GetAssemblies())
                {
                    try
                    {
                        foreach (Type type in assembly.GetExportedTypes())
                        {
                            if (type.IsClass && typeof(MoveItIntegration.IMoveItIntegrationFactory).IsAssignableFrom(type))
                            {
                                var factory = (MoveItIntegration.IMoveItIntegrationFactory)Activator.CreateInstance(type);
                                var instance = factory.GetInstance();
                                integrations.Add(instance);
                            }
                        }
                    }
                    catch { }
                }
            }

            //string msg = $"ZZZ ({integrations.Count}): ";
            //foreach (var x in integrations)
            //{
            //    msg += $"{x.Name} ({x.ID}), ";
            //}
            //Debug.Log(msg);

            return integrations;
        }
    }
}
