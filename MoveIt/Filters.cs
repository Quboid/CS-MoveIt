using ColossalFramework.UI;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoveIt
{
    public static class Filters
    {
        static readonly string[] SurfaceMeshNames = new string[]
        {
            "ploppablegravel",
            "ploppablecliffgrass",
            "ploppableasphalt-prop"
        };
        static readonly string[] SurfaceExtraBuildingNames = new string[]
        {
            "1136492728.R69 Docks", "999653286.Ploppable"
        };
        static readonly string[] SurfaceExtraPropNames = new string[]
        {
            "999653286.Ploppable"
        };
        static readonly string[] SurfaceExtraDecalNames = new string[]
        {
            "ploppableasphalt-decal"
        };
        static readonly string[] SurfaceBrushNames = new string[]
        {
            "418194210.", "416836974.", "416837329.", "416837674.", "416916315.", "416924392.", // Agriculture
            "418187161.", "416103351.", "423541340.", "416107948.", "416109689.", "416916560.", "416924629.", // Concrete
            "416106438.", "416108293.", "416110102.", "416917279.", "416924830.", "416924830.", // Gravel
            "418188094.", "416107568.", "422323255.", "416109334.", "416111243.", "416917700.", "416925008.", // Ruined
            "418188427.", "418188861.", "418415108.", // Tiled
            "418187791.", "418188652.", "418414886.", // Marble
            "1471531686.", // vilgard92's Concrete Brushes
            "1762784478.", // Clipping

            "3170169000.", "3170170659.", "3170171205.", "3170171747.", "3170191244.", // Agriculture Reupload
            "3170169096.", "3170170739.", "3170171296.", "3170171842.", "3170189957.", "3170243270.", // Concrete Reupload
            "3170169232.", "3170169993.", "3170170813.", "3170171408.", "3170171956.", // Gravel Reupload
            "3170169383.", "3170170282.", "3170170983.", "3170171577.", "3170172108.", // Ruined Reupload
            "3170170496.", "3170171150.", "3170171683.", // Tiled Reupload
            "3170170182.", "3170170908.", "3170171502.", // Marble Reupload
        };

        public static Dictionary<string, NetworkFilter> NetworkFilters = new Dictionary<string, NetworkFilter>
        {
            { "Roads", new NetworkFilter(true, new List<Type> { typeof(RoadBaseAI) }, new List<string> { "Pedestrian Path", "Beautification Item" } ) },
            { "Tracks", new NetworkFilter(true, new List<Type> { typeof(TrainTrackBaseAI), typeof(MonorailTrackAI), typeof(MetroTrackBaseAI) } ) },
            { "Paths", new NetworkFilter(true, new List<Type> { typeof(PedestrianPathAI), typeof(PedestrianTunnelAI), typeof(PedestrianBridgeAI), typeof(PedestrianWayAI) } ) },
            { "Fences", new NetworkFilter(true, new List<Type> { typeof(DecorationWallAI) } ) },
            { "Powerlines", new NetworkFilter(true, new List<Type> { typeof(PowerLineAI) } ) },
            { "Others", new NetworkFilter(true) }
        };
        public static PickerFilter Picker = new PickerFilter();

        public static void SetAnyFilter(string name, bool active)
        {
            if (NetworkFilters.ContainsKey(name))
            {
                SetNetworkFilter(name, active);
            }
            else
            {
                SetFilter(name, active);
            }
        }

        public static void SetFilter(string name, bool active)
        {
            switch (name)
            {
                case "Picker":
                    MoveItTool.filterPicker = active;
                    break;
                case "Buildings":
                    MoveItTool.filterBuildings = active;
                    break;
                case "Props":
                    MoveItTool.filterProps = active;
                    break;
                case "Decals":
                    MoveItTool.filterDecals = active;
                    break;
                case "Surfaces":
                    MoveItTool.filterSurfaces = active;
                    break;
                case "Trees":
                    MoveItTool.filterTrees = active;
                    break;
                case "PO":
                    MoveItTool.filterProcs = active;
                    break;
                case "Nodes":
                    MoveItTool.filterNodes = active;
                    break;
                case "Segments":
                    MoveItTool.filterSegments = active;
                    break;

                default:
                    throw new Exception($"Failed (name:{name}, active:{active})");
            }
        }

        public static void SetNetworkFilter(string name, bool active)
        {
            NetworkFilters[name].enabled = active;
        }

        public static void ToggleFilter(string name)
        {
            switch (name)
            {
                case "Picker":
                    MoveItTool.filterPicker = !MoveItTool.filterPicker;
                    break;
                case "Buildings":
                    MoveItTool.filterBuildings = !MoveItTool.filterBuildings;
                    break;
                case "Props":
                    MoveItTool.filterProps = !MoveItTool.filterProps;
                    break;
                case "Decals":
                    MoveItTool.filterDecals = !MoveItTool.filterDecals;
                    break;
                case "Surfaces":
                    MoveItTool.filterSurfaces = !MoveItTool.filterSurfaces;
                    break;
                case "Trees":
                    MoveItTool.filterTrees = !MoveItTool.filterTrees;
                    break;
                case "PO":
                    MoveItTool.filterProcs = !MoveItTool.filterProcs;
                    break;
                case "Nodes":
                    MoveItTool.filterNodes = !MoveItTool.filterNodes;
                    break;
                case "Segments":
                    MoveItTool.filterSegments = !MoveItTool.filterSegments;
                    break;

                default:
                    throw new Exception($"Failed (name:{name})");
            }
        }

        public static void ToggleNetworkFilter(string name)
        {
            NetworkFilters[name].enabled = !NetworkFilters[name].enabled;
        }

        public static bool IsSurface(BuildingInfo info)
        {
            foreach (string subname in SurfaceExtraBuildingNames)
            {
                if (subname.Length > info.name.Length) continue;
                if (subname == info.name.Substring(0, subname.Length))
                {
                    return true;
                }
            }

            foreach (string subname in SurfaceBrushNames)
            {
                if (subname.Length > info.name.Length) continue;
                if (subname == info.name.Substring(0, subname.Length))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsSurface(PropInfo info)
        {
            if (Array.Exists(SurfaceMeshNames, s => s.Equals(info.m_mesh.name)))
            {
                return true;
            }

            if (info.m_isDecal)
            {
                if (Array.Exists(SurfaceExtraDecalNames, s => s.Equals(info.m_mesh.name)))
                {
                    return true;
                }
            }
            else
            {
                foreach (string subname in SurfaceExtraPropNames)
                {
                    if (subname.Length > info.name.Length) continue;
                    if (subname == info.name.Substring(0, subname.Length))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool Filter(BuildingInfo info, ref Building building, bool isHover = false)
        {
            // Dont' select hidden buildings
            if ((building.m_flags & Building.Flags.Hidden) == Building.Flags.Hidden) return false;
            if (MoveItTool.filterPicker && info == Picker.Info) return true;
            if (isHover)
            {
                //Select P&P on hover with Alt
                if ((building.m_flags & Building.Flags.Untouchable) == Building.Flags.Untouchable)
                {
                    if (Settings.altSelectNodeBuildings)
                    {
                        if (!Event.current.alt)
                        {
                            return false;
                        }
                    }
                    return true;
                }

                // If single mode, filters don't apply
                if (!MoveItTool.marqueeSelection)
                {
                    return true;
                }
            }

            if (IsSurface(info))
            {
                if (MoveItTool.filterSurfaces) return true;
                else return false;
            }

            if (MoveItTool.filterBuildings)
            {
                //Select P&P on hover with Alt
                if ((building.m_flags & Building.Flags.Untouchable) == Building.Flags.Untouchable)
                {
                    if (Settings.altSelectNodeBuildings)
                    {
                        if (!Event.current.alt)
                        {
                            return false;
                        }
                    }
                    return true;
                }
                return true;
            }
            return false;
        }

        public static bool Filter(PropInfo info)
        {
            if (!MoveItTool.marqueeSelection) return true;

            if (MoveItTool.filterPicker && info == Picker.Info)
            {
                return true;
            }

            if (info.m_isDecal)
            {
                if (MoveItTool.filterSurfaces && IsSurface(info))
                {
                    return true;
                }
                if (MoveItTool.filterDecals && !IsSurface(info))
                {
                    return true;
                }
                return false;
            }

            if (IsSurface(info))
            {
                if (MoveItTool.filterSurfaces)
                {
                    return true;
                }
                return false;
            }

            if (MoveItTool.filterProps)
            {
                return true;
            }
            return false;
        }

        public static bool Filter(TreeInfo info)
        {
            if (!MoveItTool.marqueeSelection) return true;

            if (MoveItTool.filterTrees)
            {
                return true;
            }
            if (MoveItTool.filterPicker && info == Picker.Info)
            {
                return true;
            }
            return false;
        }

        public static bool Filter(NetNode node)
        {
            if (MoveItTool.MT_Tool == MoveItTool.MT_Tools.Group || MoveItTool.MT_Tool == MoveItTool.MT_Tools.Inplace)
            {
                return false;
            }
            if (!MoveItTool.marqueeSelection) return true;

            if (MoveItTool.filterPicker && node.Info == Picker.Info)
            {
                return true;
            }
            if (MoveItTool.filterNodes)
            {
                return _networkFilter(node.Info);
            }
            return false;
        }

        public static bool Filter(NetSegment segment)
        {
            if (!MoveItTool.marqueeSelection) return true;

            if (MoveItTool.filterPicker && segment.Info == Picker.Info)
            {
                return true;
            }
            if (MoveItTool.filterSegments)
            {
                return _networkFilter(segment.Info);
            }
            return false;
        }

        private static bool _networkFilter(NetInfo info)
        {
            if (!MoveItTool.filterNetworks) return true;

            NetworkFilter nf = NetworkFilter.GetNetworkFilter(info);
            return nf.enabled;
        }
    }

    public class NetworkFilter
    {
        public bool enabled;
        public List<Type> aiTypes;
        public List<string> excludeClasses;

        public NetworkFilter(bool e, List<Type> ai = null, List<string> exclude = null)
        {
            enabled = e;
            aiTypes = ai;
            excludeClasses = exclude;
        }

        public static void SetNetworkFilter(string name, bool e)
        {
            Filters.NetworkFilters[name].enabled = e;
        }

        public static NetworkFilter GetNetworkFilter(NetInfo info)
        {
            foreach (NetworkFilter nf in Filters.NetworkFilters.Values)
            {
                if (nf.aiTypes != null)
                {
                    foreach (Type t in nf.aiTypes)
                    {
                        if (info.GetAI().GetType() == t || info.GetAI().GetType().IsSubclassOf(t))
                        {
                            if (nf.excludeClasses == null)
                            {
                                return nf;
                            }
                            if (!nf.excludeClasses.Contains(info.m_class.name))
                            {
                                return nf;
                            }
                        }
                    }
                }
            }
            return Filters.NetworkFilters["Others"];
        }
    }

    public class PickerFilter
    {
        public PrefabInfo Info { get; } = null;

        public PickerFilter()
        {
            UIFilters.UpdatePickerLabel(Name, "Pick an object to filter for objects of the same type", UIFilters.InactiveLabelColor, false);
        }

        public PickerFilter(PrefabInfo pi)
        {
            Info = pi;

            UIFilters.UpdatePickerLabel(Name, Name, UIFilters.ActiveLabelColor, true);
        }

        public string Name
        {
            get
            {
                if (Info == null)
                    return "Picker";
                if (Info is BuildingInfo bi)
                    return bi.m_generatedInfo.name;
                if (Info is PropInfo pi)
                    return pi.m_generatedInfo.name;
                if (Info is TreeInfo ti)
                    return ti.m_generatedInfo.name;
                return Info.name;
            }
        }

        public bool IsBuilding { get => Info is BuildingInfo; }
        public bool IsProp { get => Info is PropInfo; }
        public bool IsTree { get => Info is TreeInfo; }
        public bool IsSegment { get => Info is NetInfo; }
        public bool IsNode { get => Info is NetInfo; }
    }
}
