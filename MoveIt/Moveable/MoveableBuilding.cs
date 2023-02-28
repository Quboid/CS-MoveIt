using ColossalFramework;
using ColossalFramework.Math;
using System;
using System.Threading;
using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;

namespace MoveIt
{
    public class BuildingState : InstanceState
    {
        public Building.Flags flags;
        public int length;
        public bool isSubInstance;
        public bool isHidden;

        [XmlElement("subStates")]
        public InstanceState[] subStates;
        
        public override void ReplaceInstance(Instance instance)
        {
            base.ReplaceInstance(instance);

            MoveableBuilding building = instance as MoveableBuilding;

            int count = 0;
            foreach(Instance subInstance in building.subInstances)
            {
                subStates[count++].instance = subInstance;
            }
        }
    }

    public class MoveableBuilding : Instance
    {
        internal bool isSubInstance = false;
        internal AssetEditorSubBuilding m_assetEditorSubBuilding = null;

        public override HashSet<ushort> segmentList
        {
            get
            {
                HashSet<ushort> segments = new HashSet<ushort>();

                ushort node = buildingBuffer[id.Building].m_netNode;
                int count = 0;
                while (node != 0)
                {
                    ItemClass.Layer layer = nodeBuffer[node].Info.m_class.m_layer;
                    if (layer != ItemClass.Layer.PublicTransport)
                    {
                        InstanceID nodeID = default;
                        nodeID.NetNode = node;
                        segments.UnionWith(new MoveableNode(nodeID).segmentList);
                    }

                    node = nodeBuffer[node].m_nextBuildingNode;

                    if (++count > 32768)
                    {
                        CODebugBase<LogChannel>.Error(LogChannel.Core, "Nodes: Invalid list detected!\n" + Environment.StackTrace);
                        break;
                    }
                }

                return segments;
            }
        }

        public MoveableBuilding(InstanceID instanceID, bool sub = false) : base(instanceID)
        {
            isSubInstance = sub;
            isHidden = (buildingBuffer[id.Building].m_flags & Building.Flags.Hidden) == Building.Flags.Hidden;
            Info = new Info_Prefab(BuildingManager.instance.m_buildings.m_buffer[instanceID.Building].Info);

            m_assetEditorSubBuilding = new AssetEditorSubBuilding(instanceID);

            ResetSubInstances();
        }

        public override InstanceState SaveToState(bool integrate = true)
        {
            BuildingState state = new BuildingState
            {
                instance = this,
                Info = Info,
                position = buildingBuffer[id.Building].m_position,
                angle = buildingBuffer[id.Building].m_angle,
                flags = buildingBuffer[id.Building].m_flags,
                length = buildingBuffer[id.Building].Length,
                isSubInstance = isSubInstance,
                isHidden = isHidden,
                isCustomContent = Info.Prefab.m_isCustomContent
            };
            state.terrainHeight = TerrainManager.instance.SampleOriginalRawHeightSmooth(state.position);

            List<InstanceState> subStates = new List<InstanceState>();

            foreach (Instance subInstance in subInstances)
            {
                if (subInstance != null && subInstance.isValid)
                {
                    if (subInstance.id.Building > 0)
                    {
                        subStates.Add(((MoveableBuilding)subInstance).SaveToState());
                    }
                    else
                    {
                        subStates.Add(subInstance.SaveToState());
                    }
                }
            }

            if (subStates.Count > 0)
                state.subStates = subStates.ToArray();

            state.SaveIntegrations(integrate);

            return state;
        }

        public override void LoadFromState(InstanceState state)
        {
            if (!(state is BuildingState buildingState)) return;

            ushort building = buildingState.instance.id.Building;

            buildingBuffer[building].m_flags = buildingState.flags;
            AddFixedHeightFlag(building);
            RelocateBuilding(building, ref buildingBuffer[building], buildingState.position, buildingState.angle);
            isSubInstance = buildingState.isSubInstance;
            isHidden = buildingState.isHidden;
            m_assetEditorSubBuilding?.LoadFromState(buildingState);

            if (buildingState.subStates != null)
            {
                foreach (InstanceState subState in buildingState.subStates)
                {
                    subState.instance.LoadFromState(subState);
                }
            }
            buildingBuffer[building].m_flags = buildingState.flags;
        }

        public override Vector3 position
        {
            get
            {
                if (id.IsEmpty) return Vector3.zero;
                return buildingBuffer[id.Building].m_position;
            }
            set
            {
                if (id.IsEmpty) return;
                buildingBuffer[id.Building].m_position = value;
            }
        }

        public override float angle
        {
            get
            {
                if (id.IsEmpty) return 0f;
                return buildingBuffer[id.Building].m_angle;
            }
            set
            {
                if (id.IsEmpty) return;
                buildingBuffer[id.Building].m_angle = (value + Mathf.PI * 2) % (Mathf.PI * 2);
            }
        }

        public override bool isValid
        {
            get
            {
                if (id.IsEmpty) return false;
                return (buildingBuffer[id.Building].m_flags & Building.Flags.Created) != Building.Flags.None;
            }
        }

        public int Length
        {
            get
            {
                return buildingBuffer[id.Building].m_length;
            }
        }

        public override void Transform(InstanceState instanceState, ref Matrix4x4 matrix4x, float deltaHeight, float deltaAngle, Vector3 center, bool followTerrain)
        {
            BuildingState state = instanceState as BuildingState;

            Vector3 newPosition = matrix4x.MultiplyPoint(state.position - center);
            newPosition.y = state.position.y + deltaHeight;

            float terrainHeight = TerrainManager.instance.SampleOriginalRawHeightSmooth(newPosition);
            bool isFixed = GetFixedHeightFlag(id.Building);
            if (!isFixed) AddFixedHeightFlag(id.Building);

            if (followTerrain)
            {
                newPosition.y = newPosition.y + terrainHeight - state.terrainHeight;
            }

            AddFixedHeightFlag(id.Building);
            Move(newPosition, state.angle + deltaAngle);

            Matrix4x4 matrixSub = default;
            matrixSub.SetTRS(Vector3.zero, Quaternion.AngleAxis(deltaAngle * Mathf.Rad2Deg, Vector3.down), Vector3.one);

            if (state.subStates != null)
            {
                foreach (InstanceState subState in state.subStates)
                {
                    Vector3 subOffset = (subState.position - center) - (state.position - center);
                    Vector3 subPosition = TransformPosition + matrixSub.MultiplyPoint(subOffset);

                    subPosition.y = subState.position.y - state.position.y + newPosition.y;

                    subState.instance.Move(subPosition, subState.angle + deltaAngle);
                    if (subState.instance is MoveableNode mn)
                    {
                        if (mn.Pillar != null)
                        {
                            mn.Pillar.Move(subPosition, subState.angle + deltaAngle);
                        }
                    }

                    if (subState is BuildingState bs)
                    {
                        if (bs.subStates != null)
                        {
                            foreach (InstanceState subSubState in bs.subStates)
                            {
                                Vector3 subSubOffset = (subSubState.position - center) - (state.position - center);
                                Vector3 subSubPosition = TransformPosition + matrixSub.MultiplyPoint(subSubOffset);

                                subSubPosition.y = subSubState.position.y - state.position.y + newPosition.y;

                                subSubState.instance.Move(subSubPosition, subSubState.angle + deltaAngle);
                                if (subSubState.instance is MoveableNode mn2)
                                {
                                    if (mn2.Pillar != null)
                                    {
                                        mn2.Pillar.Move(subSubPosition, subSubState.angle + deltaAngle);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (!isFixed && Mathf.Abs(terrainHeight - newPosition.y) < 0.01f)
            {
                RemoveFixedHeightFlag(id.Building);
            }
        }

        internal override void SetHidden(bool hide)
        {
            buildingBuffer[id.Building].m_flags = ToggleBuildingHiddenFlag(id.Building, hide);
        }

        public void InitialiseDrag()
        {
            Bounds bounds = new Bounds(position, new Vector3(Length, 0, Length));
            bounds.Expand(32f);
            Action.UpdateArea(bounds);
        }

        public void FinaliseDrag()
        {
            Bounds bounds = new Bounds(position, new Vector3(Length, 0, Length));
            bounds.Expand(32f);
            Action.UpdateArea(bounds);
        }

        public override void Move(Vector3 location, float angle)
        {
            if (!isValid) return;

            m_assetEditorSubBuilding.Move(location, angle);

            TransformAngle = angle;
            TransformPosition = location;

            if (!isVirtual())
            {
                RelocateBuilding(id.Building, ref buildingBuffer[id.Building], location, angle);
            }
        }

        public override void SetHeight(float height)
        {
            Vector3 newPosition = position;

            float terrainHeight = TerrainManager.instance.SampleOriginalRawHeightSmooth(newPosition);
            bool isFixed = GetFixedHeightFlag(id.Building);
            if (!isFixed) AddFixedHeightFlag(id.Building);

            foreach (Instance subInstance in subInstances)
            {
                Vector3 subPosition = subInstance.position;
                subPosition.y = subPosition.y - newPosition.y + height;

                subInstance.Move(subPosition, subInstance.angle);
            }

            newPosition.y = height;
            Move(newPosition, angle);

            if (!isFixed && Mathf.Abs(terrainHeight - newPosition.y) < 0.01f)
            {
                RemoveFixedHeightFlag(id.Building);
            }
        }

        public override void SetHeight()
        {
            Building b = (Building)data;
            b.m_baseHeight = 0;
            SetHeight(TerrainManager.instance.SampleOriginalRawHeightSmooth(position));
        }

        internal MoveableBuilding Duplicate()
        {
            if (BuildingManager.instance.CreateBuilding(out ushort clone, ref SimulationManager.instance.m_randomizer,
                (BuildingInfo)Info.Prefab, position, angle,
                buildingBuffer[id.Building].Length, SimulationManager.instance.m_currentBuildIndex))
            {
                buildingBuffer[clone].m_flags |= Building.Flags.FixedHeight;
                buildingBuffer[clone].m_position = position;
                return new MoveableBuilding(new InstanceID() { Building = clone });
            }

            throw new Exception($"Failed to duplicate {id.Building}!");
        }

        public override Instance Clone(InstanceState instanceState, ref Matrix4x4 matrix4x, float deltaHeight, float deltaAngle, Vector3 center, bool followTerrain, Dictionary<ushort, ushort> clonedNodes, Action action)
        {
            BuildingState state = instanceState as BuildingState;

            Vector3 newPosition = matrix4x.MultiplyPoint(state.position - center);
            newPosition.y = state.position.y + deltaHeight;

            float terrainHeight = TerrainManager.instance.SampleOriginalRawHeightSmooth(newPosition);

            if (followTerrain)
            {
                newPosition.y = newPosition.y + terrainHeight - state.terrainHeight;
            }
            MoveableBuilding cloneInstance = null;
            BuildingInfo info = state.Info.Prefab as BuildingInfo;

            float newAngle = state.angle + deltaAngle;
            if (BuildingManager.instance.CreateBuilding(out ushort clone, ref SimulationManager.instance.m_randomizer,
                info, newPosition, newAngle,
                state.length, SimulationManager.instance.m_currentBuildIndex))
            {
                SimulationManager.instance.m_currentBuildIndex++;

                cloneInstance = new MoveableBuilding(new InstanceID() { Building = clone });

                if ((state.flags & Building.Flags.Completed) != Building.Flags.None)
                {
                    buildingBuffer[clone].m_flags = buildingBuffer[clone].m_flags | Building.Flags.Completed;
                }
                if ((state.flags & Building.Flags.FixedHeight) != Building.Flags.None)
                {
                    buildingBuffer[clone].m_flags = buildingBuffer[clone].m_flags | Building.Flags.FixedHeight;
                }
                if ((state.flags & Building.Flags.Historical) != Building.Flags.None)
                {
                    buildingBuffer[clone].m_flags = buildingBuffer[clone].m_flags | Building.Flags.Historical;
                }
                if ((state.flags & Building.Flags.Hidden) != Building.Flags.None)
                {
                    buildingBuffer[clone].m_flags = buildingBuffer[clone].m_flags | Building.Flags.Hidden;
                }

                if (Mathf.Abs(terrainHeight - newPosition.y) > 0.01f)
                {
                    AddFixedHeightFlag(clone);
                }
                else
                {
                    RemoveFixedHeightFlag(clone);
                }

                if (info.m_subBuildings != null && info.m_subBuildings.Length != 0)
                {
                    Matrix4x4 subMatrix4x = default;
                    subMatrix4x.SetTRS(newPosition, Quaternion.AngleAxis(newAngle * Mathf.Rad2Deg, Vector3.down), Vector3.one);
                    for (int i = 0; i < info.m_subBuildings.Length; i++)
                    {
                        BuildingInfo subInfo = info.m_subBuildings[i].m_buildingInfo;
                        Vector3 subPosition = subMatrix4x.MultiplyPoint(info.m_subBuildings[i].m_position);
                        float subAngle = info.m_subBuildings[i].m_angle * 0.0174532924f + newAngle;

                        if (BuildingManager.instance.CreateBuilding(out ushort subClone, ref SimulationManager.instance.m_randomizer,
                            subInfo, subPosition, subAngle, 0, SimulationManager.instance.m_currentBuildIndex))
                        {
                            SimulationManager.instance.m_currentBuildIndex++;
                            if (info.m_subBuildings[i].m_fixedHeight)
                            {
                                buildingBuffer[subClone].m_flags = buildingBuffer[subClone].m_flags | Building.Flags.FixedHeight;
                            }
                        }
                        if (clone != 0 && subClone != 0)
                        {
                            buildingBuffer[clone].m_subBuilding = subClone;
                            buildingBuffer[subClone].m_parentBuilding = clone;
                            buildingBuffer[subClone].m_flags = buildingBuffer[subClone].m_flags | Building.Flags.Untouchable;
                            clone = subClone;
                        }
                    }
                }
                cloneInstance.ResetSubInstances();
                cloneInstance.m_assetEditorSubBuilding.Create(clone, cloneInstance.angle, (BuildingInfo)cloneInstance.Info.Prefab, (Mathf.Abs(terrainHeight - newPosition.y) > 0.01f), newPosition);
            }

            return cloneInstance;
        }

        // For Deletion Undo
        public override Instance Clone(InstanceState instanceState, Dictionary<ushort, ushort> clonedNodes)
        {
            BuildingState state = instanceState as BuildingState;

            MoveableBuilding cloneInstance = null;
            BuildingInfo info = state.Info.Prefab as BuildingInfo;

            if (BuildingManager.instance.CreateBuilding(out ushort clone, ref SimulationManager.instance.m_randomizer,
                info, state.position, state.angle,
                state.length, SimulationManager.instance.m_currentBuildIndex))
            {
                SimulationManager.instance.m_currentBuildIndex++;

                cloneInstance = new MoveableBuilding(new InstanceID() { Building = clone });

                buildingBuffer[clone].m_flags = state.flags;

                if (info.m_subBuildings != null && info.m_subBuildings.Length != 0)
                {
                    Matrix4x4 subMatrix4x = default;
                    subMatrix4x.SetTRS(state.position, Quaternion.AngleAxis(state.angle * Mathf.Rad2Deg, Vector3.down), Vector3.one);
                    for (int i = 0; i < info.m_subBuildings.Length; i++)
                    {
                        BuildingInfo subInfo = info.m_subBuildings[i].m_buildingInfo;
                        Vector3 subPosition = subMatrix4x.MultiplyPoint(info.m_subBuildings[i].m_position);
                        float subAngle = info.m_subBuildings[i].m_angle * 0.0174532924f + state.angle;

                        if (BuildingManager.instance.CreateBuilding(out ushort subClone, ref SimulationManager.instance.m_randomizer,
                            subInfo, subPosition, subAngle, 0, SimulationManager.instance.m_currentBuildIndex))
                        {
                            SimulationManager.instance.m_currentBuildIndex++;
                            if (info.m_subBuildings[i].m_fixedHeight)
                            {
                                buildingBuffer[subClone].m_flags = buildingBuffer[subClone].m_flags | Building.Flags.FixedHeight;
                            }
                        }
                        if (clone != 0 && subClone != 0)
                        {
                            buildingBuffer[clone].m_subBuilding = subClone;
                            buildingBuffer[subClone].m_parentBuilding = clone;
                            buildingBuffer[subClone].m_flags = buildingBuffer[subClone].m_flags | Building.Flags.Untouchable;
                            clone = subClone;
                        }
                    }
                }
                cloneInstance.ResetSubInstances();
            }

            return cloneInstance;
        }

        public override void Delete()
        {
            if (isValid)
            {
                SimulationManager.instance.AddAction(() =>
                {
                    BuildingManager.instance.ReleaseBuilding(id.Building);
                });
            }
        }

        public void AddFixedHeightFlag(ushort building)
        {
            buildingBuffer[building].m_flags = buildingBuffer[building].m_flags | Building.Flags.FixedHeight;
            m_assetEditorSubBuilding.SetFixedHeight(true);
        }

        public void RemoveFixedHeightFlag(ushort building)
        {
            buildingBuffer[building].m_flags = buildingBuffer[building].m_flags & ~Building.Flags.FixedHeight;
            m_assetEditorSubBuilding.SetFixedHeight(false);
        }

        public bool GetFixedHeightFlag(ushort building)
        {
            return (buildingBuffer[building].m_flags & Building.Flags.FixedHeight) == Building.Flags.FixedHeight;
        }
        
        internal void ResetSubInstances()
        {
            List<Instance> instances = new List<Instance>();
            int count = 0;

            if (!isSubInstance)
            {
                ushort building = buildingBuffer[id.Building].m_subBuilding;
                while (building != 0)
                {
                    InstanceID buildingID = default;
                    buildingID.Building = building;

                    instances.Add(new MoveableBuilding(buildingID, true));
                    building = buildingBuffer[building].m_subBuilding;

                    if (++count > 49152)
                    {
                        CODebugBase<LogChannel>.Error(LogChannel.Core, "Buildings: Invalid list detected!\n" + Environment.StackTrace);
                        break;
                    }
                }
            }

            ushort node = buildingBuffer[id.Building].m_netNode;
            count = 0;
            while (node != 0)
            {
                ItemClass.Layer layer = nodeBuffer[node].Info.m_class.m_layer;
                if (layer != ItemClass.Layer.PublicTransport)
                {
                    InstanceID nodeID = default;
                    nodeID.NetNode = node;
                    instances.Add(new MoveableNode(nodeID));
                }

                node = nodeBuffer[node].m_nextBuildingNode;
                if ((nodeBuffer[node].m_flags & NetNode.Flags.Created) != NetNode.Flags.Created)
                {
                    node = 0;
                }

                if (++count > 32768)
                {
                    CODebugBase<LogChannel>.Error(LogChannel.Core, "Nodes: Invalid list detected!\n" + Environment.StackTrace);
                    break;
                }
            }

            subInstances = instances;
        }

        public override Bounds GetBounds(bool ignoreSegments = true)
        {
            BuildingInfo info = buildingBuffer[id.Building].Info;

            float radius = Mathf.Max(info.m_cellWidth * 4f, info.m_cellLength * 4f);
            Bounds bounds = new Bounds(OverlayPosition, new Vector3(radius, 0, radius));

            foreach (Instance subInstance in subInstances)
            {
                if (subInstance is MoveableBuilding mb)
                {
                    bounds.Encapsulate(mb.GetBounds(ignoreSegments));
                }
                else
                {
                    bounds.Encapsulate(subInstance.GetBounds(ignoreSegments));
                }
            }

            return bounds;
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo, Color toolColor, Color despawnColor)
        {
            if (!isValid) return;
            if (MoveItTool.m_isLowSensitivity) return;

            ushort building = id.Building;
            BuildingInfo buildingInfo = buildingBuffer[building].Info;

            if (toolColor != MoveItTool.Colors.Align && WillBuildingDespawn(building))
            {
                toolColor = despawnColor;
            }

            float alpha = 1f;
            BuildingTool.CheckOverlayAlpha(buildingInfo, ref alpha);
            toolColor.a *= alpha;
            toolColor = toolColor.Adjusted();

            int length = buildingBuffer[building].Length;
            BuildingTool.RenderOverlay(cameraInfo, buildingInfo, length, OverlayPosition, OverlayAngle, toolColor, false);
;
            foreach (Instance subInstance in subInstances)
            {
                if (subInstance is MoveableNode mn)
                {
                    ushort node = mn.id.NetNode;
                    for (int k = 0; k < 8; k++)
                    {
                        ushort segment = netManager.m_nodes.m_buffer[node].GetSegment(k);
                        if (segment != 0 && netManager.m_segments.m_buffer[segment].m_startNode == node && (netManager.m_segments.m_buffer[segment].m_flags & NetSegment.Flags.Untouchable) != NetSegment.Flags.None)
                        {
                            NetTool.RenderOverlay(cameraInfo, ref netManager.m_segments.m_buffer[segment], toolColor, toolColor);
                        }
                    }
                }
                else if (subInstance is MoveableBuilding mb)
                {
                    Building b = buildingBuffer[mb.id.Building];
                    BuildingTool.RenderOverlay(cameraInfo, (BuildingInfo)mb.Info.Prefab, b.Length, mb.OverlayPosition, mb.OverlayAngle, toolColor, false);
                }
            }
        }

        public override void RenderCloneOverlay(InstanceState instanceState, ref Matrix4x4 matrix4x, Vector3 deltaPosition, float deltaAngle, Vector3 center, bool followTerrain, RenderManager.CameraInfo cameraInfo, Color toolColor)
        {
            if (MoveItTool.m_isLowSensitivity) return;

            BuildingState state = instanceState as BuildingState;

            BuildingInfo buildingInfo = state.Info.Prefab as BuildingInfo;

            Vector3 newPosition = matrix4x.MultiplyPoint(state.position - center);
            newPosition.y = state.position.y + deltaPosition.y;

            if (followTerrain)
            {
                newPosition.y = newPosition.y - state.terrainHeight + TerrainManager.instance.SampleOriginalRawHeightSmooth(newPosition);
            }

            float newAngle = state.angle + deltaAngle;

            buildingInfo.m_buildingAI.RenderBuildOverlay(cameraInfo, toolColor, newPosition, newAngle, default);
            BuildingTool.RenderOverlay(cameraInfo, buildingInfo, state.length, newPosition, newAngle, toolColor, false);
            if (buildingInfo.m_subBuildings != null && buildingInfo.m_subBuildings.Length != 0)
            {
                Matrix4x4 subMatrix4x = default;
                subMatrix4x.SetTRS(newPosition, Quaternion.AngleAxis(newAngle * Mathf.Rad2Deg, Vector3.down), Vector3.one);
                for (int i = 0; i < buildingInfo.m_subBuildings.Length; i++)
                {
                    BuildingInfo buildingInfo2 = buildingInfo.m_subBuildings[i].m_buildingInfo;
                    Vector3 position = subMatrix4x.MultiplyPoint(buildingInfo.m_subBuildings[i].m_position);
                    float angle = buildingInfo.m_subBuildings[i].m_angle * 0.0174532924f + newAngle;
                    buildingInfo2.m_buildingAI.RenderBuildOverlay(cameraInfo, toolColor, position, angle, default);
                    BuildingTool.RenderOverlay(cameraInfo, buildingInfo2, 0, position, angle, toolColor, true);
                }
            }
        }

        public override void RenderCloneGeometry(InstanceState instanceState, ref Matrix4x4 matrix4x, Vector3 deltaPosition, float deltaAngle, Vector3 center, bool followTerrain, RenderManager.CameraInfo cameraInfo, Color toolColor)
        {
            RenderCloneGeometryImplementation(instanceState, ref matrix4x, deltaPosition, deltaAngle, center, followTerrain, cameraInfo);
        }

        public static void RenderCloneGeometryImplementation(InstanceState instanceState, ref Matrix4x4 matrix4x, Vector3 deltaPosition, float deltaAngle, Vector3 center, bool followTerrain, RenderManager.CameraInfo cameraInfo)
        {
            BuildingInfo info = instanceState.Info.Prefab as BuildingInfo;
            Color color = GetColor(instanceState.instance.id.Building, info);

            Vector3 newPosition = matrix4x.MultiplyPoint(instanceState.position - center);
            newPosition.y = instanceState.position.y + deltaPosition.y;

            if (followTerrain)
            {
                newPosition.y = newPosition.y - instanceState.terrainHeight + TerrainManager.instance.SampleOriginalRawHeightSmooth(newPosition);
            }

            float newAngle = instanceState.angle + deltaAngle;

            info.m_buildingAI.RenderBuildGeometry(cameraInfo, newPosition, newAngle, 0);
            BuildingTool.RenderGeometry(cameraInfo, info, info.GetLength(), newPosition, newAngle, false, color);
            if (info.m_subBuildings != null && info.m_subBuildings.Length != 0)
            {
                Matrix4x4 subMatrix4x = default;
                subMatrix4x.SetTRS(newPosition, Quaternion.AngleAxis(newAngle * Mathf.Rad2Deg, Vector3.down), Vector3.one);
                for (int i = 0; i < info.m_subBuildings.Length; i++)
                {
                    BuildingInfo buildingInfo2 = info.m_subBuildings[i].m_buildingInfo;
                    Vector3 position = subMatrix4x.MultiplyPoint(info.m_subBuildings[i].m_position);
                    float angle = info.m_subBuildings[i].m_angle * Mathf.Deg2Rad + newAngle;
                    buildingInfo2.m_buildingAI.RenderBuildGeometry(cameraInfo, position, angle, 0);
                    BuildingTool.RenderGeometry(cameraInfo, buildingInfo2, 0, position, angle, true, color);
                }
            }
        }

        public override void RenderGeometry(RenderManager.CameraInfo cameraInfo, Color toolColor)
        {
            if (isHidden) return;
            if (!Virtual) return;
            BuildingInfo buildingInfo = Info.Prefab as BuildingInfo;
            Color color = GetColor(id.Building, buildingInfo);

            buildingInfo.m_buildingAI.RenderBuildGeometry(cameraInfo, OverlayPosition, OverlayAngle, 0);
            BuildingTool.RenderGeometry(cameraInfo, buildingInfo, Length, OverlayPosition, OverlayAngle, false, color);

            foreach (Instance subInstance in subInstances)
            {
                if (subInstance is MoveableBuilding msb)
                {
                    msb.RenderGeometry(cameraInfo, toolColor);
                }
            }
        }

        internal static Color GetColor(ushort buildingID, BuildingInfo info)
        {
            if (!info.m_useColorVariations)
            {
                return info.m_color0;
            }
            Randomizer randomizer = new Randomizer((int)buildingID);
            switch (randomizer.Int32(4u))
            {
                case 0:
                    return info.m_color0;
                case 1:
                    return info.m_color1;
                case 2:
                    return info.m_color2;
                case 3:
                    return info.m_color3;
                default:
                    return info.m_color0;
            }
        }

        private bool WillBuildingDespawn(ushort building)
        {
            BuildingInfo info = buildingBuffer[building].Info;

            ItemClass.Zone zone1 = info.m_class.GetZone();
            ItemClass.Zone zone2 = info.m_class.GetSecondaryZone();

            if (info.m_placementStyle != ItemClass.Placement.Automatic || zone1 == ItemClass.Zone.None)
            {
                return false;
            }

            info.m_buildingAI.CheckRoadAccess(building, ref buildingBuffer[building]);
            if ((buildingBuffer[building].m_problems & Notification.Problem1.RoadNotConnected) == Notification.Problem1.RoadNotConnected ||
                !buildingBuffer[building].CheckZoning(zone1, zone2, true))
            {
                return true;
            }

            return false;
        }

        private void RelocateBuilding(ushort building, ref Building data, Vector3 position, float angle)
        {
            RemoveFromGrid(building, ref data);

            data.m_position = position;
            data.m_angle = (angle + Mathf.PI * 2) % (Mathf.PI * 2);

            AddToGrid(building, ref data);
            data.CalculateBuilding(building);
            Singleton<SimulationManager>.instance.AddAction(() => BuildingManager.instance.UpdateBuildingRenderer(building, true));
        }

        private static void AddToGrid(ushort building, ref Building data)
        {
            int num = Mathf.Clamp((int)(data.m_position.x / 64f + 135f), 0, 269);
            int num2 = Mathf.Clamp((int)(data.m_position.z / 64f + 135f), 0, 269);
            int num3 = num2 * 270 + num;
            while (!Monitor.TryEnter(BuildingManager.instance.m_buildingGrid, SimulationManager.SYNCHRONIZE_TIMEOUT))
            {
            }
            try
            {
                buildingBuffer[building].m_nextGridBuilding = BuildingManager.instance.m_buildingGrid[num3];
                BuildingManager.instance.m_buildingGrid[num3] = building;
                if (buildingBuffer[building].m_nextGridBuilding == building)
                {
                    Log.Error($"Building #{building} has self as nextGirdBuilding", "[M49]");
                    buildingBuffer[building].m_nextGridBuilding = 0;
                }
            }
            finally
            {
                Monitor.Exit(BuildingManager.instance.m_buildingGrid);
            }
        }

        private static void RemoveFromGrid(ushort building, ref Building data)
        {
            BuildingManager buildingManager = BuildingManager.instance;

            BuildingInfo info = data.Info;
            int num = Mathf.Clamp((int)(data.m_position.x / 64f + 135f), 0, 269);
            int num2 = Mathf.Clamp((int)(data.m_position.z / 64f + 135f), 0, 269);
            int num3 = num2 * 270 + num;
            while (!Monitor.TryEnter(buildingManager.m_buildingGrid, SimulationManager.SYNCHRONIZE_TIMEOUT))
            {
            }
            try
            {
                ushort loopPreviousId = 0;
                ushort loopNextId = buildingManager.m_buildingGrid[num3];
                int count = 0;

                // Loop through buildings in this grid square
                while (loopNextId != 0)
                {
                    if (loopNextId == building)
                    { // found building being moved
                        if (loopPreviousId == 0)
                        {
                            buildingManager.m_buildingGrid[num3] = data.m_nextGridBuilding;
                        }
                        else
                        {
                            buildingBuffer[(int)loopPreviousId].m_nextGridBuilding = data.m_nextGridBuilding;
                        }
                        break;
                    }
                    loopPreviousId = loopNextId;
                    loopNextId = buildingBuffer[(int)loopNextId].m_nextGridBuilding;
                    if (++count > 49152)
                    {
                        CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                        break;
                    }
                }
                data.m_nextGridBuilding = 0;
            }
            finally
            {
                Monitor.Exit(buildingManager.m_buildingGrid);
            }
            if (info != null)
            {
                Singleton<RenderManager>.instance.UpdateGroup(num * 45 / 270, num2 * 45 / 270, info.m_prefabDataLayer);
            }
        }

        internal List<MoveableSegment> GetAttachedSegments()
        {
            List<MoveableSegment> segments = new List<MoveableSegment>();
            try
            {
                HashSet<ushort> allSegments = new HashSet<ushort>();
                HashSet<ushort> attachedSegments = new HashSet<ushort>();

                foreach (Instance sub in subInstances)
                {
                    if (sub is MoveableNode mn)
                    {
                        NetNode node = (NetNode)mn.data;
                        if (node.Info.m_class.name == "Transport Connection") continue;

                        for (int i = 0; i < 8; i++)
                        {
                            ushort s = node.GetSegment(i);
                            if (s > 0)
                            {
                                if (allSegments.Contains(s))
                                { // If it's already in the list, both nodes are part of this building
                                    attachedSegments.Add(s);
                                }
                                else
                                {
                                    allSegments.Add(s);
                                }
                            }
                        }
                    }
                }

                foreach (ushort s in attachedSegments)
                {
                    NetSegment seg = segmentBuffer[s];
                    if ((seg.m_flags & NetSegment.Flags.Created) != NetSegment.Flags.Created)
                    {
                        throw new Exception($"Found attached segment #{s} but it is not created.");
                    }
                    if ((seg.m_flags & NetSegment.Flags.Untouchable) != NetSegment.Flags.Untouchable)
                    {
                        throw new Exception($"Found attached segment #{s} but it is not untouchable.");
                    }

                    segments.Add(new MoveableSegment(new InstanceID() { NetSegment = s }));
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "[M81]");
            }

            return segments;
        }
    }
}
