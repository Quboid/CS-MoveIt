using UnityEngine;
using ColossalFramework;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.Assertions;

namespace MoveIt
{
    public abstract class Action// : MonoBehaviour
    {
        public enum TypeMasks : ushort
        {
            None = 0,
            Building = 1,
            Other = 2,
            Network = 4
        }

        public static HashSet<Instance> selection = new HashSet<Instance>();
        public static bool affectsSegments = true;
        
        public static bool HasSelection() => selection.Count > 0;

        public abstract void Do();
        public abstract void Undo();
        public abstract void ReplaceInstances(List<CloneData> toReplace);

        protected static Building[] buildingBuffer = Singleton<BuildingManager>.instance.m_buildings.m_buffer;
        protected static NetNode[] nodeBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;
        protected static NetSegment[] segmentBuffer = Singleton<NetManager>.instance.m_segments.m_buffer;

        protected Dictionary<BuildingState, BuildingState> pillarsOriginalToClone = new Dictionary<BuildingState, BuildingState>();
        protected bool PillarsProcessed;

        public static TypeMasks TypeMask = TypeMasks.None;
        public Action()
        {
            TypeMask = TypeMasks.None;
            foreach (Instance i in selection)
            {
                if (i.isValid)
                {
                    if (i is MoveableBuilding)
                    {
                        TypeMask |= TypeMasks.Building;
                    }
                    else if (i is MoveableSegment || i is MoveableNode)
                    {
                        TypeMask |= TypeMasks.Network;
                    }
                    else
                    {
                        TypeMask |= TypeMasks.Other;
                    }
                }
            }

            Assert.AreNotEqual(TypeMask, TypeMasks.None);
        }

        internal virtual void OnHover() { }

        internal virtual void Overlays(RenderManager.CameraInfo cameraInfo, Color toolColor, Color despawnColor) { }

        internal virtual void UpdateNodeIdInSegmentState(ushort oldId, ushort newId) { }

        public static bool IsSegmentSelected(ushort segment)
        {
            if (affectsSegments) return false;

            InstanceID id = InstanceID.Empty;
            id.NetSegment = segment;

            return selection.Contains(id);
        }

        public static Vector3 GetCenter()
        {
            return GetTotalBounds().center;
        }

        public static float GetAngle()
        {
            if (selection.Count() == 0)
            {
                return 0f;
            }
            else if (selection.Count() == 1)
            {
                return selection.First().angle;
            }
            List<float> angles = new List<float>();
            foreach (Instance i in selection.Where(i => i is MoveableBuilding || i is MoveableProc || i is MoveableProp))
            {
                angles.Add((i.angle % (Mathf.PI * 2)) * Mathf.Rad2Deg);
            }
            if (angles.Count() == 0)
            {
                GetExtremeObjects(out Instance a, out Instance b);
                return (Mathf.PI / 2) - (float)GetAngleBetweenPointsRads(a.position, b.position);
            }

            return ModeAngle(angles.ToArray());
        }

        protected static double GetAngleBetweenPointsRads(Vector3 a, Vector3 b)
        {
            return (Math.Atan2(b.x - a.x, b.z - a.z) + (Mathf.PI * 2)) % (Mathf.PI * 2);
        }

        //private static float MeanAngle(float[] angles)
        //{
        //    var x = angles.Sum(a => Mathf.Cos(a * Mathf.PI / 180)) / angles.Length;
        //    var y = angles.Sum(a => Mathf.Sin(a * Mathf.PI / 180)) / angles.Length;
        //    return (Mathf.Atan2(y, x) * 180 / Mathf.PI) * Mathf.Deg2Rad;
        //}

        private static float ModeAngle(float[] angles)
        {
            Dictionary<float, uint> angleCount = new Dictionary<float, uint>();

            foreach (float a in angles)
            {
                if (!angleCount.ContainsKey(a))
                {
                    angleCount[a] = 1;
                }
                else
                {
                    angleCount[a]++;
                }
            }

            float angle = 0f;
            uint max = 0;
            foreach (KeyValuePair<float, uint> pair in angleCount)
            {
                if (pair.Value > max)
                {
                    angle = pair.Key;
                    max = pair.Value;
                }
            }

            return angle * Mathf.Deg2Rad;
        }

        public static void ClearPOFromSelection()
        {
            if (!MoveItTool.PO.Enabled) return;

            HashSet<Instance> toRemove = new HashSet<Instance>();
            foreach (Instance i in selection)
            {
                if (i is MoveableProc)
                {
                    toRemove.AddObject(i);
                }
            }
            foreach (Instance i in toRemove)
            {
                selection.RemoveObject(i);
            }

            MoveItTool.m_debugPanel.UpdatePanel();
        }

        public static Bounds GetTotalBounds(bool ignoreSegments = true, bool excludeNetworks = false)
        {
            Bounds totalBounds = default;

            bool init = false;

            foreach (Instance instance in selection)
            {
                if (!init)
                {
                    totalBounds = instance.GetBounds(ignoreSegments);
                    init = true;
                }
                else
                {
                    totalBounds.Encapsulate(instance.GetBounds(ignoreSegments));
                }
            }

            return totalBounds;
        }

        public static void UpdateArea(Bounds bounds, bool full = false)
        {
            try
            {
                if ((TypeMask & (TypeMasks.Building | TypeMasks.Network)) != TypeMasks.None)
                {
                    if (full)
                    {
                        SimulationManager.instance.AddAction(() => { TerrainModify.UpdateArea(bounds.min.x, bounds.min.z, bounds.max.x, bounds.max.z, true, true, false); });
                    }

                    bounds.Expand(32f);
                    MoveItTool.instance.areasToUpdate.Add(bounds);
                    MoveItTool.instance.areaUpdateCountdown = 60;

                    if (full)
                    {
                        SimulationManager.instance.AddAction(() => { Singleton<BuildingManager>.instance.ZonesUpdated(bounds.min.x, bounds.min.z, bounds.max.x, bounds.max.z); });
                        PropLayer.Manager.UpdateProps(bounds.min.x, bounds.min.z, bounds.max.x, bounds.max.z);
                        SimulationManager.instance.AddAction(() => { Singleton<TreeManager>.instance.UpdateTrees(bounds.min.x, bounds.min.z, bounds.max.x, bounds.max.z); });
                        bounds.Expand(64f);
                        SimulationManager.instance.AddAction(() => { Singleton<ElectricityManager>.instance.UpdateGrid(bounds.min.x, bounds.min.z, bounds.max.x, bounds.max.z); });
                        SimulationManager.instance.AddAction(() => { Singleton<WaterManager>.instance.UpdateGrid(bounds.min.x, bounds.min.z, bounds.max.x, bounds.max.z); });
                    }
                }
                else
                {
                    bounds.Expand(32f);
                    MoveItTool.instance.areasToQuickUpdate.Add(bounds);
                    MoveItTool.instance.areaUpdateCountdown = 60;
                }
            }
            catch (IndexOutOfRangeException e)
            {
                Log.Error($"EXCEPTION\n{bounds}\n{e}" + Environment.NewLine + e.StackTrace, "[M10]");
            }
        }

        internal static void GetExtremeObjects(out Instance A, out Instance B)
        {
            List<Instance> inst = new List<Instance>();
            foreach (Instance i in selection)
            {
                if (i is MoveableSegment)
                {
                    continue;
                }
                inst.Add(i);
            }

            if (inst.Count() < 2)
            {
                throw new IndexOutOfRangeException("Less than 2 objects selected");
            }
            A = inst[0];
            B = inst[1];

            float longest = 0;

            for (int i = 0; i < (inst.Count() - 1); i++)
            {
                for (int j = i + 1; j < inst.Count(); j++)
                {
                    float distance = Math.Abs((inst[i].position - inst[j].position).sqrMagnitude);

                    if (distance > longest)
                    {
                        A = inst[i];
                        B = inst[j];
                        longest = distance;
                    }
                }
            }
        }

        protected HashSet<InstanceState> ProcessPillars(HashSet<InstanceState> states, bool makeClone)
        {
            if (!Settings.advancedPillarControl) return states;

            HashSet<ushort> nodesWithAttachments = new HashSet<ushort>();

            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();
            foreach (InstanceState instanceState in states)
            {
                if (instanceState is NodeState ns && ((NetNode)(ns.instance.data)).m_building > 0 && 
                    ((buildingBuffer[((NetNode)(ns.instance.data)).m_building].m_flags & Building.Flags.Hidden) != Building.Flags.Hidden))
                {
                    nodesWithAttachments.Add(ns.instance.id.NetNode);
                }
            }
            HashSet<InstanceState> newStates = new HashSet<InstanceState>(states);
            foreach (InstanceState instanceState in states)
            {
                ushort buildingId = instanceState.instance.id.Building;
                if (instanceState is BuildingState originalState && MoveItTool.m_pillarMap.ContainsKey(buildingId) && MoveItTool.m_pillarMap[buildingId] > 0)
                {
                    ushort nodeId = MoveItTool.m_pillarMap[buildingId];
                    if (nodesWithAttachments.Contains(nodeId)) // The node is also selected
                    {
                        continue;
                    }
                    MoveableBuilding original = (MoveableBuilding)instanceState.instance;
                    buildingBuffer[buildingId].m_flags |= Building.Flags.Hidden;
                    selection.Remove(original);
                    newStates.Remove(originalState);
                    BuildingState cloneState = null;
                    if (makeClone)
                    {
                        MoveableBuilding clone = original.Duplicate();
                        selection.Add(clone);
                        cloneState = (BuildingState)clone.SaveToState();
                        newStates.Add(cloneState);
                        Log.Debug($"Pillar {buildingId} for node {nodeId} duplicated to {clone.id.Building}", "[M11]");
                    }
                    else
                    {
                        Log.Debug($"Pillar {buildingId} for node {nodeId} hidden", "[M12]");
                    }
                    pillarsOriginalToClone.Add(originalState, cloneState);
                    original.isHidden = true;
                }
            }
            if (pillarsOriginalToClone.Count > 0)
            {
                MoveItTool.UpdatePillarMap();
            }
            states = newStates;
            watch.Stop();
            Log.Debug($"Pillars handled in {watch.ElapsedMilliseconds} ms\nSelected nodes:{nodesWithAttachments.Count}, total selection:{states.Count}, dups mapped:{pillarsOriginalToClone.Count}", "[M13]");
            PillarsProcessed = true;

            return states;
        }
    }
}
