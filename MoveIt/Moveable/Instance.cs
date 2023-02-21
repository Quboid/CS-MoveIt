using ColossalFramework;
using MoveItIntegration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using UnityEngine;

namespace MoveIt
{
    [Serializable]
    public struct IntegrationEntry {
        public string ID;
        public string Version;
        public string Data; 
    }

    [XmlInclude(typeof(BuildingState)), XmlInclude(typeof(NodeState)), XmlInclude(typeof(PropState)), XmlInclude(typeof(ProcState)), XmlInclude(typeof(SegmentState)), XmlInclude(typeof(TreeState))]
    public class InstanceState
    {
        [XmlIgnore]
        public Instance instance;

        [XmlIgnore]
        public IInfo Info = new Info_Prefab();

        public Vector3 position;
        public float angle;
        public float terrainHeight;
        public bool isCustomContent;

        private string m_loadedName;

        public uint id
        {
            get
            {
                return instance.id.RawData;
            }

            set
            {
                InstanceID instanceID = InstanceID.Empty;
                instanceID.RawData = value;

                instance = instanceID;
            }
        }

        public string prefabName
        {
            get
            {
                if (Info.Prefab != null)
                {
                    return Info.Name;
                }
                else
                {
                    return m_loadedName;
                }
            }

            set
            {
                m_loadedName = value;

                switch (instance.id.Type)
                {
                    case InstanceType.Building:
                        {
                            Info.Prefab = PrefabCollection<BuildingInfo>.FindLoaded(value);
                            break;
                        }
                    case InstanceType.Prop:
                        {
                            Info.Prefab = PrefabCollection<PropInfo>.FindLoaded(value);
                            break;
                        }
                    case InstanceType.Tree:
                        {
                            Info.Prefab = PrefabCollection<TreeInfo>.FindLoaded(value);
                            break;
                        }
                    case InstanceType.NetNode:
                    case InstanceType.NetSegment:
                        {
                            Info.Prefab = PrefabCollection<NetInfo>.FindLoaded(value);
                            break;
                        }
                    case InstanceType.NetLane:
                        {
                            Info.Prefab = PrefabCollection<BuildingInfo>.FindLoaded(value);
                            if (Info.Prefab == null)
                            {
                                Info.Prefab = PrefabCollection<PropInfo>.FindLoaded(value);
                            }
                            break;
                        }
                }
            }
        }

        public virtual void ReplaceInstance(Instance newInstance)
        {
            instance = newInstance;

            if (newInstance.id.Type != instance.id.Type)
            {
                DebugUtils.Warning("Mismatching instances type ('" + newInstance.id.Type + "' -> '" + newInstance.id.Type + "').");
            }

            if (newInstance.Info.Prefab != Info.Prefab)
            {
                DebugUtils.Warning($"Mismatching instances info:\n{Info.Prefab.name} <{Info.GetHashCode()}>\n{newInstance.Info.Prefab.name} <{newInstance.Info.GetHashCode()}>\n");
            }
        }

        [XmlIgnore]
        public Dictionary<MoveItIntegrationBase, object> IntegrationData = new Dictionary<MoveItIntegrationBase, object>();

        [XmlArray("IntegrationEntry_List")]
        [XmlArrayItem("IntegrationEntry_Item")]
        public IntegrationEntry[] IntegrationData64
        {
            get
            {
                List<IntegrationEntry> ret = new List<IntegrationEntry>();
                foreach (var item in IntegrationData)
                {
                    try
                    {
                        MoveItIntegrationBase integration = item.Key;
                        string data = integration.Encode64(item.Value);
                        if (data != null && data.Length > 0)
                        {
                            ret.Add(new IntegrationEntry
                            {
                                ID = integration.ID,
                                Version = integration.DataVersion.ToString(),
                                Data = data,
                            });
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error("Failed to export integration: " + item.Key, "[M27]");
                        DebugUtils.LogException(e);
                    }
                }
                return ret.ToArray();
            }
            set
            {
                IntegrationData = new Dictionary<MoveItIntegrationBase, object>();
                if (value == null) return;
                foreach (var entry in value)
                {
                    if (entry.Data == null) continue;
                    try { 
                        MoveItIntegrationBase integration = MoveItTool.GetIntegrationByID(entry.ID);
                        if (integration == null) continue;
                        Version version = new Version(entry.Version);
                        IntegrationData[integration] = integration.Decode64(entry.Data, version);
                    }
                    catch (Exception e)
                    {
                        Log.Error("Failed to import integration: " + entry.ID, "[M28]");
                        DebugUtils.LogException(e);
                    }

                }
            }
        }

        public virtual void SaveIntegrations(bool integrate)
        {
            if (!integrate) return;

            foreach (var integration in MoveItTool.Integrations)
            {
                try
                {
                    IntegrationData[integration] = integration.Copy(instance.id);
                }
                catch (Exception e)
                {
                    Log.Error($"integration {integration} Failed to copy {instance?.id}" + integration, "[M29]");
                    DebugUtils.LogException(e);
                }
            }
        }
    }

    public interface IInfo
    {
        string Name { get; }
        PrefabInfo Prefab { get; set; }
    }

    public class Info_Prefab : IInfo
    {
        public Info_Prefab(object i) => Prefab = (PrefabInfo)i;
        public Info_Prefab() => Prefab = null;

        public string Name
        {
            get => (Prefab == null) ? "<null>" : Prefab.name;
        }

        public PrefabInfo Prefab { get; set; } = null;
    }

    public abstract class Instance
    {
        protected static NetManager netManager;
        protected static Building[] buildingBuffer;
        protected static NetSegment[] segmentBuffer;
        protected static NetNode[] nodeBuffer;
        protected static NetLane[] laneBuffer;

        public List<Instance> subInstances = new List<Instance>();
        internal bool isHidden = false;

        public Instance(InstanceID instanceID)
        {
            id = instanceID;
            netManager = Singleton<NetManager>.instance;
            buildingBuffer = Singleton<BuildingManager>.instance.m_buildings.m_buffer;
            segmentBuffer = Singleton<NetManager>.instance.m_segments.m_buffer;
            nodeBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;
            laneBuffer = Singleton<NetManager>.instance.m_lanes.m_buffer;
        }

        public InstanceID id
        {
            get;
            protected set;
        }

        public abstract HashSet<ushort> segmentList
        {
            get;
        }

        internal bool _virtual = false;
        public bool Virtual
        {
            get => _virtual;
            set
            {
                if (value == true)
                {
                    if (_virtual == false)
                    {
                        _virtual = true;
                        InitialiseTransform();
                        SetHidden(true);
                        foreach (Instance i in subInstances)
                        {
                            i.Virtual = true;
                        }
                    }
                }
                else
                {
                    if (_virtual == true)
                    {
                        _virtual = false;
                        SetHidden(false);
                        foreach (Instance i in subInstances)
                        {
                            i.Virtual = false;
                        }
                    }
                }
            }
        }

        internal virtual void InitialiseTransform()
        {
            TransformPosition = position;
            TransformAngle = angle;
        }

        internal float GetYPosition()
        {
            // Airports DLC runways belong to a building with height of -100, so sample the runway
            try
            {
                if (Info.Prefab.name == "Runway Owner")
                {
                    foreach (Instance i in subInstances)
                    {
                        if (i.Info.Name == "Airport Area Runway")
                        {
                            return i.position.y;
                        }
                    }
                }
            }
            catch (Exception) { };

            return position.y;
        }

        internal virtual void SetHidden(bool hide) { }

        internal Building.Flags ToggleBuildingHiddenFlag(ushort id, bool hide)
        {
            if (isHidden) return buildingBuffer[id].m_flags;
            if (hide)
            {
                if ((buildingBuffer[id].m_flags & Building.Flags.Hidden) == Building.Flags.Hidden)
                {
                    throw new Exception($"Building already hidden\n#{id}:{buildingBuffer[id].Info.name}");
                }

                return buildingBuffer[id].m_flags | Building.Flags.Hidden;
            }
            else
            {
                if ((buildingBuffer[id].m_flags & Building.Flags.Hidden) != Building.Flags.Hidden)
                {
                    throw new Exception($"Building not hidden\n#{id}:{buildingBuffer[id].Info.name}");
                }
            }

            return buildingBuffer[id].m_flags & ~Building.Flags.Hidden;
        }

        public abstract Vector3 position { get; set; }

        public abstract float angle { get; set; }

        public virtual Vector3 TransformPosition { get; set; }

        public virtual float TransformAngle { get; set; }

        public Vector3 OverlayPosition
        {
            get
            {
                if (Virtual)
                    return TransformPosition;
                return position;
            }
        }

        public float OverlayAngle
        {
            get
            {
                if (Virtual)
                    return TransformAngle;
                return angle;
            }
        }

        public abstract bool isValid { get; }

        public object data
        {
            get
            {
                switch (id.Type)
                {
                    case InstanceType.Building:
                        {
                            return BuildingManager.instance.m_buildings.m_buffer[id.Building];
                        }
                    case InstanceType.Prop:
                        {
                            return PropLayer.Manager.Buffer(id);
                        }
                    case InstanceType.Tree:
                        {
                            return TreeManager.instance.m_trees.m_buffer[id.Tree];
                        }
                    case InstanceType.NetNode:
                        {
                            return NetManager.instance.m_nodes.m_buffer[id.NetNode];
                        }
                    case InstanceType.NetSegment:
                        {
                            return NetManager.instance.m_segments.m_buffer[id.NetSegment];
                        }
                }

                return null;
            }
        }

        public IInfo Info { get; set; }

        public abstract InstanceState SaveToState(bool integrate = true);
        public abstract void LoadFromState(InstanceState state);
        public abstract void Transform(InstanceState state, ref Matrix4x4 matrix4x, float deltaHeight, float deltaAngle, Vector3 center, bool followTerrain);
        public abstract void Move(Vector3 location, float angle);
        public abstract void SetHeight(float height);
        public abstract void SetHeight();
        public abstract Instance Clone(InstanceState state, ref Matrix4x4 matrix4x, float deltaHeight, float deltaAngle, Vector3 center, bool followTerrain, Dictionary<ushort, ushort> clonedNodes, Action action);
        public abstract Instance Clone(InstanceState state, Dictionary<ushort, ushort> clonedNodes); // For Deletion Undo (bulldoze, convertToPO)
        public abstract void Delete();
        public abstract Bounds GetBounds(bool ignoreSegments = true);
        public abstract void RenderOverlay(RenderManager.CameraInfo cameraInfo, Color toolColor, Color despawnColor);
        public abstract void RenderCloneOverlay(InstanceState state, ref Matrix4x4 matrix4x, Vector3 deltaPosition, float deltaAngle, Vector3 center, bool followTerrain, RenderManager.CameraInfo cameraInfo, Color toolColor);
        public abstract void RenderCloneGeometry(InstanceState state, ref Matrix4x4 matrix4x, Vector3 deltaPosition, float deltaAngle, Vector3 center, bool followTerrain, RenderManager.CameraInfo cameraInfo, Color toolColor);
        public virtual void RenderGeometry(RenderManager.CameraInfo cameraInfo, Color toolColor) { }

        internal static bool isVirtual()
        {
            return ActionQueue.instance.current is TransformAction ta && ta.Virtual;
        }

        public static implicit operator Instance(InstanceID id)
        {
            switch (id.Type)
            {
                case InstanceType.Building:
                    return new MoveableBuilding(id);
                case InstanceType.NetNode:
                    return new MoveableNode(id);
                case InstanceType.NetSegment:
                    return new MoveableSegment(id);
                case InstanceType.Prop:
                    return new MoveableProp(id);
                case InstanceType.Tree:
                    return new MoveableTree(id);
                case InstanceType.NetLane:
                    return new MoveableProc(id);
            }
            return null;
        }

        public override bool Equals(object obj)
        {
            if (obj is Instance instance)
            {
                return instance.id == id;
            }

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return id.GetHashCode();
        }

        protected static void UpdateSegmentBlocks(ushort segment, ref NetSegment data)
        {
            MoveItTool.instance.segmentsToUpdate.Add(segment);
            MoveItTool.instance.segmentUpdateCountdown = 10;
        }

        protected static void CalculateSegmentDirections(ref NetSegment segment, ushort segmentID)
        {
            if (segment.m_flags != NetSegment.Flags.None)
            {
                segment.m_startDirection.y = 0;
                segment.m_endDirection.y = 0;

                segment.m_startDirection.Normalize();
                segment.m_endDirection.Normalize();

                segment.m_startDirection = segment.FindDirection(segmentID, segment.m_startNode);
                segment.m_endDirection = segment.FindDirection(segmentID, segment.m_endNode);
            }
        }

        public override string ToString()
        {
            return id.Debug();
        }
    }

    public static class InstanceIDExtension
    {
        public static string Debug(this InstanceID id)
        {
            string msg = "";

            switch (id.Type)
            {
                case InstanceType.Building:
                    msg += "B" + id.Building;
                    break;

                case InstanceType.Prop:
                    msg += "P" + id.Prop;
                    break;

                case InstanceType.Tree:
                    msg += "T" + id.Tree;
                    break;

                case InstanceType.NetLane:
                    msg += "PO" + id.NetLane;
                    break;

                case InstanceType.NetNode:
                    msg += "N" + id.NetNode;
                    break;

                case InstanceType.NetSegment:
                    msg += "S" + id.NetSegment;
                    break;
            }

            return msg;
        }
    }
}
