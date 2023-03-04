using ColossalFramework.Plugins;
using MoveIt.Lang;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace MoveIt
{
    internal class PO_Manager
    {
        internal PO_Logic Logic;
        public static GameObject gameObject;

        private HashSet<uint> visibleIds = new HashSet<uint>();
        internal Dictionary<uint, PO_Object> visibleObjects = new Dictionary<uint, PO_Object>();

        internal List<PO_Object> Objects => new List<PO_Object>(visibleObjects.Values);
        internal List<PO_Group> Groups = new List<PO_Group>();

        internal static readonly string[] VersionNames = { "1.7" };
        private readonly BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

        internal bool Enabled = false;
        private bool _active = false;
        public bool Active
        {
            get
            {
                if (!Enabled)
                    return false;
                return _active;
            }
            set
            {
                if (!Enabled)
                    _active = false;
                _active = value;
            }
        }

        internal PO_Manager()
        {
            try
            {
                InitialiseLogic();
            }
            catch (TypeLoadException)
            {
                Enabled = false;
            }
        }

        /// <summary>
        /// Get a PO object that is visible (not filtered out)
        /// </summary>
        /// <param name="id">The NetLane id</param>
        /// <returns>PO Object wrapper</returns>
        internal PO_Object GetProcObj(uint id)
        {
            if (visibleObjects.ContainsKey(id))
            {
                return visibleObjects[id];
            }

            return new PO_Object();
            throw new KeyNotFoundException($"Key {id} not found in visibleObjects");
        }

        private void InitialiseLogic()
        {
            if (isModEnabled())
            {
                gameObject = new GameObject("MIT_POLogic");
                gameObject.AddComponent<PO_Logic>();
                Logic = gameObject.GetComponent<PO_Logic>();

                Enabled = true;
            }
            else
            {
                Enabled = false;
            }
        }

        public void InitGroups()
        {
            if (!PO_Logic.POHasGroups)
            {
                Log.Debug($"PO Groups feature not found!");
                return;
            }

            foreach (PO_Group g in Groups)
            {
                foreach (PO_Object o in g.objects)
                {
                    o.Group = null;
                }
            }

            Groups = new List<PO_Group>();

            object groupList = PO_Logic.tPOLogic.GetField("groups", flags).GetValue(PO_Logic.POLogic);
            if (groupList == null)
            {
                Log.Debug($"PO Groups is null!");
                return;
            }
            int count = (int)groupList.GetType().GetProperty("Count").GetValue(groupList, null);

            for (int i = 0; i < count; i++)
            {
                var v = groupList.GetType().GetMethod("get_Item").Invoke(groupList, new object[] { i });
                Groups.Add(new PO_Group(v));
            }

            // Update selection instances
            foreach (Instance instance in Action.selection)
            {
                if (instance is MoveableProc mpo)
                {
                    mpo.m_procObj = MoveItTool.PO.visibleObjects[mpo.m_procObj.Id];
                }
            }
        }

        internal void Clone(CloneDataPO cloneData)
        {
            if (!Enabled) return;

            Logic.Clone((MoveableProc)cloneData.Original);
        }

        //internal void Clone(ProcState original, Vector3 position, float angle, Action action)
        //{
        //    if (!Enabled) return;

        //    Logic.Clone((MoveableProc)original.instance, position, angle, action);
        //}

        internal void MapGroupClones(HashSet<InstanceState> m_states, CloneActionBase action)
        {
            if (!Enabled) return;

            Logic.MapGroupClones(m_states, action);
        }

        internal void StartConvertAction()
        {
            if (InitialiseTool(true))
            {
                ConvertToPOAction convertAction = new ConvertToPOAction();
                ActionQueue.instance.Push(convertAction);
                ActionQueue.instance.Do();
            }
        }

        internal void InitialiseTool()
        {
            InitialiseTool(!MoveItTool.PO.Active);
        }

        internal bool InitialiseTool(bool enable)
        {
            if (MoveItTool.PO.Active == enable)
            {
                return true;
            }

            try
            {
                MoveItTool.PO.Active = enable;
                if (MoveItTool.PO.Active)
                {
                    if (MoveItTool.ToolState == MoveItTool.ToolStates.Cloning)
                    {
                        MoveItTool.instance.StopCloning();
                    }

                    MoveItTool.PO.ToolEnabled();
                    UIToolOptionPanel.instance.PO_button.activeStateIndex = 1;
                    ActionQueue.instance.Push(new TransformAction());
                }
                else
                {
                    UIToolOptionPanel.instance.PO_button.activeStateIndex = 0;
                    Action.ClearPOFromSelection();
                }
                UIFilters.POToggled();
            }
            catch (ArgumentException e)
            {
                Log.Error($"PO Integration failed:\n{e}");
                if (MoveItTool.PO.Active)
                {
                    MoveItTool.PO.Active = false;
                    UIToolOptionPanel.instance.PO_button.activeStateIndex = 0;
                }
                return false;
            }
            return true;
        }

        /// <returns>Bool - whether any PO changed since MIT was disabled</returns>
        internal bool ToolEnabled()
        {
            Dictionary<uint, PO_Object> newVisible = new Dictionary<uint, PO_Object>();
            HashSet<uint> newIds = new HashSet<uint>();

            foreach (PO_Object obj in Logic.Objects)
            {
                newVisible.Add(obj.Id, obj);
                newIds.Add(obj.Id);
            }

            HashSet<uint> removed = new HashSet<uint>(visibleIds);
            removed.ExceptWith(newIds);
            HashSet<uint> added = new HashSet<uint>(newIds);
            added.ExceptWith(visibleIds);

            List<Instance> toRemove = new List<Instance>();
            foreach (Instance instance in Action.selection)
            {
                uint id = instance.id.NetLane;
                if (id > 0)
                {
                    if (removed.Contains(id))
                    {
                        toRemove.Add(instance);
                    }
                }
            }
            foreach (Instance instance in toRemove)
            {
                Action.selection.RemoveObject(instance);
            }

            //Log.Debug($"Visible from:{visibleObjects.Count} to:{newVisible.Count}\nSelected from:{selectedIds.Count} to:{newSelectedIds.Count}");

            visibleObjects = newVisible;
            visibleIds = newIds;

            try
            {
                if (PO_Logic.IsGroupFilterEnabled())
                {
                    InitGroups();
                    string msg = $"PO Groups: {Groups.Count} found\n";
                    //foreach (PO_Group g in MoveItTool.PO.Groups)
                    //{
                    //    msg += $"{g.objects.Count} ({g.root.Id}), ";
                    //}
                    //Log.Debug(msg);
                }
                else
                {
                    Log.Debug($"PO Groups disabled");
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }

            if (added.Count > 0 || removed.Count > 0)
                return true;

            return false;
        }

        internal void Delete(PO_Object obj)
        {
            if (!Enabled) return;

            Logic.Delete(obj);
        }

        internal PO_Object ConvertToPO(Instance instance)
        {
            if (!Enabled) return null;

            return Logic.ConvertToPO(instance);
        }

        internal static bool isModEnabled()
        {
            if (!isModInstalled())
            {
                return false;
            }

            return true;
        }

        internal static bool isModInstalled()
        {
            if (!PluginManager.instance.GetPluginsInfo().Any(mod => (
                    mod.publishedFileID.AsUInt64 == 1094334744uL || 
                    mod.name.Contains("ProceduralObjects") || 
                    mod.name.Contains("Procedural Objects") ||
                    mod.name.Contains("1094334744")
            ) && mod.isEnabled))
            {
                return false;
            }

            return true;
        }

        internal static string getVersionText()
        {
            if (isModInstalled())
            {
                if (VersionNames.Contains(PO_Logic.getVersion().Substring(0, 3)))
                {
                    return String.Format(Str.integration_PO_Found, PO_Logic.getVersion().Substring(0, 3));
                }
                else
                {
                    return String.Format(Str.integration_PO_WrongVersion, PO_Logic.getVersion().Substring(0, 3));
                }
            }

            return Str.integration_PO_Notfound;
        }

        internal static string getVersion()
        {
            try
            {
                return _getVersion();
            }
            catch (TypeLoadException)
            {
                return "";
            }
        }

        private static string _getVersion()
        {
            return PO_Logic.getVersion();
        }
    }
}
