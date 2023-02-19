using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using UnifiedUI.Helpers;
using UnityEngine;

namespace MoveIt
{
    public partial class MoveItTool : ToolBase
    {
        #region Debug Overlays
        internal struct DebugOverlay
        {
            internal Quad3 quad;
            internal Color32 color;

            public DebugOverlay(Quad3 q, Color32 c)
            {
                quad = q;
                color = c;
            }
        }
        private static List<DebugOverlay> DebugBoxes = new List<DebugOverlay>();
        private static List<Vector3> DebugPoints = new List<Vector3>();
        internal static void AddDebugBox(Bounds b, Color32? c = null)
        {
            if (c == null)
            {
                c = new Color32(255, 255, 255, 63);
            }
            Quad3 q = default;
            q.a = new Vector3(b.min.x, b.min.y, b.min.z);
            q.b = new Vector3(b.min.x, b.min.y, b.max.z);
            q.c = new Vector3(b.max.x, b.min.y, b.max.z);
            q.d = new Vector3(b.max.x, b.min.y, b.min.z);
            DebugOverlay d = new DebugOverlay(q, (Color32)c);
            DebugBoxes.Add(d);
            //Log.Debug($"\nBounds:{b}", "[M06]");
        }
        internal static void AddDebugPoint(Vector3 v)
        {
            DebugPoints.Add(v);
            Log.Debug($"\nPoint:{v}", "[M07]");
        }
        internal void ClearDebugOverlays()
        {
            DebugBoxes.Clear();
            DebugPoints.Clear();
        }
        internal static Color32 GetRandomDebugColor()
        {
            return new Color32(RandomByte(100, 255), RandomByte(100, 255), RandomByte(100, 255), 63);
        }
        #endregion

        internal void EnableUUI()
        {
            if (!Settings.useUUI)
            {
                return;
            }

            try
            {
                var hotkeys = new UUIHotKeys { ActivationKey = OptionsKeymapping.toggleTool };
                foreach (var item in OptionsKeymapping.InToolKeysForSelection) {
                    hotkeys.AddInToolKey(item, Action.HasSelection);
                }
                foreach (var item in OptionsKeymapping.InToolKeysAlways) {
                    hotkeys.AddInToolKey(item);
                }
                Texture2D texture = ResourceLoader.loadTextureFromAssembly("MoveIt.Icons.MoveIt.png");

                UUIButton = UUIHelpers.RegisterToolButton(
                    name: "MoveItTool",
                    groupName: null,
                    tooltip: "Move It",
                    tool: this,
                    icon: texture,
                    hotkeys: hotkeys
                    );
                m_button.Hide();
            }
            catch (Exception e)
            {
                Log.Error(e, "[M08]");
                UIView.ForwardException(e);
            }
        }

        internal void DisableUUI()
        {
            m_button?.Show();
            UUIButton?.Destroy();
            UUIButton = null;
        }

        internal static void UpdatePillarMap()
        {
            if (!Settings.advancedPillarControl) return;

            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();
            m_pillarMap = new Dictionary<ushort, ushort>();

            for (ushort i = 0; i < nodeBuffer.Length; i++)
            {
                NetNode n = nodeBuffer[i];
                if ((n.m_flags & NetNode.Flags.Created) == NetNode.Flags.Created)
                {
                    if (n.m_building > 0)
                    {
                        if ((buildingBuffer[n.m_building].m_flags & Building.Flags.Hidden) != Building.Flags.Hidden)
                        {
                            if (!m_pillarMap.ContainsKey(n.m_building))
                            {
                                try
                                {
                                    m_pillarMap.Add(n.m_building, i);
                                }
                                catch (Exception e)
                                {
                                    string msg2 = $"BuildingID: #{n.m_building} {buildingBuffer[n.m_building].Info?.name}, Count:{m_pillarMap.Count}" + Environment.NewLine;
                                    foreach (var kvp in m_pillarMap)
                                    {
                                        msg2 += $"{kvp.Key}->{kvp.Value}, ";
                                    }
                                    Log.Error(msg2 + e, "[M01]");
                                }
                            }
                        }
                    }
                }
            }
            watch.Stop();
            //Log.Debug($"Move It found {m_pillarMap.Count} attached pillar/pylons in {watch.ElapsedMilliseconds} ms.", "[M02]");
        }

        public static bool IsExportSelectionValid()
        {
            return CloneActionBase.GetCleanSelection(out Vector3 center).Count > 0;
        }

        public bool Export(string filename)
        {
            string path = Path.Combine(saveFolder, filename + ".xml");

            try
            {
                HashSet<Instance> selection = CloneActionBase.GetCleanSelection(out Vector3 center);

                if (selection.Count == 0) return false;

                bool includesPO = false;

                Selection selectionState = new Selection
                {
                    version = ModInfo.version,
                    center = center,
                    includesPO = includesPO,
                    terrainHeight = Singleton<TerrainManager>.instance.SampleOriginalRawHeightSmooth(center),
                    states = new InstanceState[selection.Count]
                };

                int i = 0;
                foreach (Instance instance in selection)
                {
                    selectionState.states[i++] = instance.SaveToState();
                }

                using (FileStream stream = new FileStream(path, FileMode.OpenOrCreate))
                {
                    stream.SetLength(0); // Emptying the file
                    XmlSerializer xmlSerializer = new XmlSerializer(typeof(Selection));
                    XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
                    ns.Add("", "");
                    xmlSerializer.Serialize(stream, selectionState, ns);
                }
            }
            catch (Exception e)
            {
                DebugUtils.Log("Couldn't export selection");
                DebugUtils.LogException(e);

                UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Export failed", "The selection couldn't be exported to '" + path + "'\n\n" + e.Message, true);
                return false;
            }

            return true;
        }

        public void Import(string filename)
        {
            ImportImpl(filename, false);
        }

        public void Restore(string filename)
        {
            ImportImpl(filename, true);
        }

        private void ImportImpl(string filename, bool restore)
        {
            lock (ActionQueue.instance)
            {
                StopCloning();
                StopTool();

                //bool activatePO = true;
                //if (!PO.Active)
                //{
                //    activatePO = false;
                //    PO.InitialiseTool(true);
                //}

                XmlSerializer xmlSerializer = new XmlSerializer(typeof(Selection));
                Selection selectionState;

                string path = Path.Combine(saveFolder, filename + ".xml");

                try
                {
                    // Trying to Deserialize the file
                    using (FileStream stream = new FileStream(path, FileMode.Open))
                    {
                        selectionState = xmlSerializer.Deserialize(stream) as Selection;
                        if (selectionState.version != null && new Version(selectionState.version).CompareTo(new Version(2, 9, 9)) == -1)
                        { // XML pre-dates terrain height date
                            foreach (InstanceState state in selectionState.states)
                            {
                                selectionState.terrainHeight = state.terrainHeight;
                                break;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    // Couldn't Deserialize (XML malformed?)
                    DebugUtils.Log("Couldn't load file");
                    DebugUtils.LogException(e);

                    UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Import failed", "Couldn't load '" + path + "'\n\n" + e.Message, true);
                    return;
                }

                //if (!activatePO && !selectionState.includesPO)
                //{
                //    PO.InitialiseTool(false);
                //}

                ImportFromArray(selectionState, restore);
            }
        }

        internal void ImportFromArray(Selection selectionState, bool restore = false)
        {
            if (selectionState != null && selectionState.states != null && selectionState.states.Length > 0)
            {
                HashSet<string> missingPrefabs = new HashSet<string>();
                HashSet<ushort> includedNodes = new HashSet<ushort>();
                HashSet<ushort> includedSegments = new HashSet<ushort>();
                List<InstanceState> newStates = new List<InstanceState>(selectionState.states);
                int startCount = selectionState.states.Length;

                // Look for missing prefabs, nodes, and segments
                foreach (InstanceState state in selectionState.states)
                {
                    if (state == null || state.Info == null || state.instance == null)
                    {
                        newStates.Remove(state);
                        continue;
                    }
                    if (state.Info.Prefab == null)
                    {
                        missingPrefabs.Add(state.prefabName);
                        newStates.Remove(state);
                    }
                    else
                    {
                        InstanceID id = state.instance.id;
                        if (id.Type == InstanceType.NetNode)
                            includedNodes.Add(id.NetNode);
                        else if (id.Type == InstanceType.NetSegment)
                            includedSegments.Add(id.NetSegment);
                    }
                }
                selectionState.states = newStates.ToArray();

                // Strip out segments with missing nodes
                foreach (InstanceState state in selectionState.states)
                {
                    if (state is SegmentState ss)
                    {
                        if (!(includedNodes.Contains(ss.startNodeId) && includedNodes.Contains(ss.endNodeId)))
                        {
                            newStates.Remove(state);
                            includedSegments.Remove(ss.instance.id.NetSegment);
                        }
                    }
                }
                selectionState.states = newStates.ToArray();

                // Remove missing segment references and strip out nodes that have no remaining segments
                foreach (InstanceState state in selectionState.states)
                {
                    if (state is NodeState ns)
                    {
                        List<ushort> newSegmentsList = new List<ushort>(ns.segmentsList);
                        foreach (ushort segId in ns.segmentsList)
                        {
                            if (!includedSegments.Contains(segId))
                            {
                                newSegmentsList.Remove(segId);
                            }
                        }
                        if (newSegmentsList.Count == 0)
                        {
                            newStates.Remove(state);
                            includedNodes.Remove(ns.instance.id.NetNode);
                        }
                        else ns.segmentsList = newSegmentsList;
                    }
                }
                selectionState.states = newStates.ToArray();
                Log.Info($"Cleaned import (state count:{startCount}->{newStates.Count})", "[M03]");

                if (missingPrefabs.Count > 0)
                {
                    DebugUtils.Warning("Missing prefabs: " + string.Join(", ", missingPrefabs.ToArray()));

                    UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Assets missing", "The following assets are missing and will be ignored:\n\n" + string.Join("\n", missingPrefabs.ToArray()), false);
                }

                if ((ToolManager.instance.m_properties.m_mode & ItemClass.Availability.AssetEditor) != ItemClass.Availability.None)
                { // Set props to fixed-height if in asset editor
                    foreach (InstanceState state in selectionState.states)
                    {
                        if (state is PropState ps)
                        {
                            ps.fixedHeight = true;
                            ps.position.y = ps.position.y - ps.terrainHeight + 60f; // 60 is editor's terrain height
                        }
                    }
                }
                else if ((ToolManager.instance.m_properties.m_mode & ItemClass.Availability.Game) != ItemClass.Availability.None)
                { 
                    if (!restore && !followTerrain)
                    { // Set import height to terrain height
                        float heightDelta = Singleton<TerrainManager>.instance.SampleOriginalRawHeightSmooth(RaycastMouseLocation()) - selectionState.terrainHeight;

                        foreach (InstanceState state in selectionState.states)
                        {
                            state.position.y += heightDelta;
                        }
                        selectionState.center.y += heightDelta;
                    }
                }

                CloneActionBase action = new CloneActionImport(selectionState.states, selectionState.center);

                if (action.Count > 0)
                {
                    ActionQueue.instance.Push(action);

                    if (restore)
                    {
                        if (MergeNodes) action.m_snapNode = GetMergingNodes(action, Vector3.zero, action.angleDelta, action.center);
                        SimulationManager.instance.AddAction(() => { ActionQueue.instance.Do(); });
                    }
                    else
                    {
                        SetToolState(ToolStates.Cloning); // For clone
                    }

                    UIToolOptionPanel.RefreshCloneButton();
                    UIToolOptionPanel.RefreshAlignHeightButton();
                }
            }
        }

        public void Delete(string filename)
        {
            try
            {
                string path = Path.Combine(saveFolder, filename + ".xml");

                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                DebugUtils.Log("Couldn't delete file");
                DebugUtils.LogException(ex);

                return;
            }
        }

        internal static byte RandomByte(byte min, byte max)
        {
            return (byte)Rand.Next(min, max);
        }

        protected static void ReleaseSegmentBlock(ushort segment, ref ushort segmentBlock)
        {
            if (segmentBlock != 0)
            {
                ZoneManager.instance.ReleaseBlock(segmentBlock);
                segmentBlock = 0;
            }
        }

        internal static HashSet<Bounds> MergeBounds(HashSet<Bounds> outerList)
        {
            HashSet<Bounds> innerList = new HashSet<Bounds>();
            HashSet<Bounds> newList = new HashSet<Bounds>();

            int c = 0;

            do
            {
                foreach (Bounds outer in outerList)
                {
                    //Color32 color = GetRandomDebugColor();
                    //AddDebugBox(outer, color);

                    bool merged = false;

                    float outerVolume = outer.size.x * outer.size.y * outer.size.z;
                    foreach (Bounds inner in innerList)
                    {
                        float separateVolume = (inner.size.x * inner.size.y * inner.size.z) + outerVolume;

                        Bounds encapsulated = inner;
                        encapsulated.Encapsulate(outer);
                        float encapsulateVolume = encapsulated.size.x * encapsulated.size.y * encapsulated.size.z;

                        if (!merged && encapsulateVolume < separateVolume)
                        {
                            newList.Add(encapsulated);
                            merged = true;
                        }
                        else
                        {
                            newList.Add(inner);
                        }
                    }
                    if (!merged)
                    {
                        newList.Add(outer);
                    }

                    innerList = new HashSet<Bounds>(newList);
                    newList.Clear();
                }

                if (outerList.Count <= innerList.Count)
                {
                    break;
                }
                outerList = new HashSet<Bounds>(innerList);
                innerList.Clear();

                if (c > 1000)
                {
                    Log.Error($"Looped bounds-merge a thousand times", "[M04]");
                    break;
                }

                c++;
            }
            while (true);

            //foreach (Bounds b in innerList)
            //{
            //    b.Expand(4f);
            //    AddDebugBox(b, new Color32(255, 0, 0, 200));
            //}
            //Log.Debug($"\nStart:{originalList.Count}\nInner:{innerList.Count}", "[M05]");
            return innerList;
        }

        internal void ProcessMirror(AlignMirrorAction action)
        {
            StartCoroutine(ProcessMirrorIterate(action));
        }

        internal IEnumerator<object> ProcessMirrorIterate(AlignMirrorAction action)
        {
            const uint MaxAttempts = 1000_000;

            uint c = 0;
            while (c < MaxAttempts && POProcessing > 0)
            {
                c++;
                yield return new WaitForSeconds(0.05f);
            }

            if (c == MaxAttempts)
            {
                throw new Exception($"Failed to mirror PO");
            }

            action.DoMirrorProcess();
        }
    }
}
