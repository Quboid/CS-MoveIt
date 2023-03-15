using ColossalFramework;
using ColossalFramework.Math;
using System.Collections.Generic;
using UnityEngine;

namespace MoveIt
{
    public class TreeState : InstanceState
    {
        public bool single;
        public bool fixedHeight;
    }

    public class MoveableTree : Instance
    {
        /// <summary>
        /// As a tree without FixedHeight is moved, track the vertical offset to maintain height if FixedHeight is then enabled
        /// </summary>
        private float yTerrainOffset = 0;

        public override HashSet<ushort> segmentList
        {
            get
            {
                return new HashSet<ushort>();
            }
        }

        public MoveableTree(InstanceID instanceID) : base(instanceID)
        {
            //if (((TreeInstance.Flags)TreeManager.instance.m_trees.m_buffer[instanceID.Tree].m_flags & TreeInstance.Flags.Created) == TreeInstance.Flags.None)
            //{
            //    throw new Exception($"Tree #{instanceID.Tree} not found!");
            //}
            Info = new Info_Prefab(TreeManager.instance.m_trees.m_buffer[instanceID.Tree].Info);
        }

        public override InstanceState SaveToState(bool integrate = true)
        {
            TreeState state = new TreeState
            {
                instance = this,
                isCustomContent = Info.Prefab.m_isCustomContent
            };

            TreeInstance[] trees = Singleton<TreeManager>.instance.m_trees.m_buffer;
            uint tree = id.Tree;
            state.Info = Info;

            state.position = trees[tree].Position;
            state.terrainHeight = TerrainManager.instance.SampleOriginalRawHeightSmooth(state.position);

            state.single = trees[tree].Single;
            state.fixedHeight = trees[tree].FixedHeight;

            state.SaveIntegrations(integrate);

            return state;
        }

        public override void LoadFromState(InstanceState state)
        {
            if (!(state is TreeState treeState)) return;

            TreeInstance[] trees = Singleton<TreeManager>.instance.m_trees.m_buffer;
            uint tree = id.Tree;
            TreeManager.instance.MoveTree(tree, treeState.position);
            TreeManager.instance.UpdateTreeRenderer(tree, true);
            trees[tree].FixedHeight = treeState.fixedHeight;
        }

        public override Vector3 position
        {
            get
            {
                if (id.IsEmpty) return Vector3.zero;
                return TreeManager.instance.m_trees.m_buffer[id.Tree].Position;
            }
            set
            {
                if (id.IsEmpty) TreeManager.instance.m_trees.m_buffer[id.Tree].Position = Vector3.zero;
                else TreeManager.instance.m_trees.m_buffer[id.Tree].Position = value;
            }
        }

        public override float angle
        {
            get { return 0f; }
            set { }
        }

        public override bool isValid
        {
            get
            {
                if (id.IsEmpty) return false;
                return TreeManager.instance.m_trees.m_buffer[id.Tree].m_flags != 0;
            }
        }

        public override void Transform(InstanceState state, ref Matrix4x4 matrix4x, float deltaHeight, float deltaAngle, Vector3 center, bool followTerrain)
        {
            Vector3 newPosition = matrix4x.MultiplyPoint(state.position - center);

            newPosition.y = GetTreeYPos(state, deltaHeight, newPosition, followTerrain);

            Move(newPosition, 0f);
        }

        /// <summary>
        /// Calculate the new Y-Pos for the tree
        /// </summary>
        /// <param name="state">The tree state</param>
        /// <param name="deltaHeight">The height difference before snapping</param>
        /// <param name="newPosition">The new position for the tree</param>
        /// <param name="followTerrain">Should tree follow terrain?</param>
        /// <param name="isClone">Is this the clone ghost rendering?</param>
        /// <returns></returns>
        internal float GetTreeYPos(InstanceState state, float deltaHeight, Vector3 newPosition, bool followTerrain, bool isClone = false)
        {
            TreeInstance[] trees = Singleton<TreeManager>.instance.m_trees.m_buffer;
            uint treeID = id.Tree;
            float y;

            float terrainHeight = Singleton<TerrainManager>.instance.SampleDetailHeight(newPosition);

            if (!MoveItTool.treeSnapping)
            {
                y = terrainHeight;
            }
            else if (trees[treeID].FixedHeight)
            { // If it's already fixed height, handle followTerrain
                // If the state is being cloned, don't use the terrain-height offset
                y = newPosition.y + (isClone ? 0 : yTerrainOffset);
                if (followTerrain)
                {
                    y += terrainHeight - state.terrainHeight;
                }
            }
            else
            { // Snapping is on and it is not fixed height yet
                if (deltaHeight != 0)
                {
                    trees[treeID].FixedHeight = true;
                    y = terrainHeight + deltaHeight;
                    yTerrainOffset = terrainHeight - state.terrainHeight;
                }
                else
                {
                    y = terrainHeight;
                }
            }

            //Log.Debug($"{path}\nstate:{state.terrainHeight} tH-state:{terrainHeight - state.terrainHeight}, yTO:{yTerrainOffset}\n" +
            //    $"ft:{followTerrain}, ts:{MoveItTool.treeSnapping}, fh:{trees[treeID].FixedHeight}, dh:{deltaHeight}\n" +
            //    $"FRAME  - newY:{newPosition.y}, oldY:{position.y}, diff:{newPosition.y - position.y}\n" +
            //    $"ADJUST - adjY:{y}, newY:{newPosition.y}, diff:{y - newPosition.y}\n" +
            //    $"TOTAL  - adjY:{y}, oldY:{position.y}, diff:{y - position.y}\n" +
            //    $"HEIGHT - adjY:{y}, terrainHeight:{terrainHeight}, diff:{y - terrainHeight}", "[M74]");

            return y;
        }

        public override void Move(Vector3 location, float angle)
        {
            if (!isValid) return;

            uint tree = id.Tree;
            NaturalResourceManager.instance.TreesModified(position);
            TreeManager.instance.MoveTree(tree, location);
            TreeManager.instance.UpdateTreeRenderer(tree, true);
        }

        public override void SetHeight(float height)
        {
            Vector3 newPosition = position;
            newPosition.y = height;

            if (MoveItTool.treeSnapping)
            {
                float terrainHeight = Singleton<TerrainManager>.instance.SampleDetailHeight(newPosition);
                if (height > terrainHeight + 0.075f || height < terrainHeight - 0.075f)
                {
                    SetFixedHeight(true);
                }
                else
                {
                    SetFixedHeight(false);
                }
            }

            uint tree = id.Tree;
            TreeManager.instance.MoveTree(tree, newPosition);
            TreeManager.instance.UpdateTreeRenderer(tree, true);
        }

        public override void SetHeight()
        {
            SetHeight(TerrainManager.instance.SampleDetailHeight(position));
        }

        public void SetFixedHeight(bool fixedHeight)
        {
            Singleton<TreeManager>.instance.m_trees.m_buffer[id.Tree].FixedHeight = fixedHeight;
        }

        public override Instance Clone(InstanceState instanceState, ref Matrix4x4 matrix4x, float deltaHeight, float deltaAngle, Vector3 center, bool followTerrain, Dictionary<ushort, ushort> clonedNodes, Action action)
        {
            TreeState state = instanceState as TreeState;

            Vector3 newPosition = matrix4x.MultiplyPoint(state.position - center);
            newPosition.y = state.position.y + deltaHeight;

            if (followTerrain)
            {
                newPosition.y = newPosition.y + TerrainManager.instance.SampleOriginalRawHeightSmooth(newPosition) - state.terrainHeight;
            }

            Instance cloneInstance = null;
            TreeInstance[] buffer = TreeManager.instance.m_trees.m_buffer;

            if (TreeManager.instance.CreateTree(out uint clone, ref SimulationManager.instance.m_randomizer,
                state.Info.Prefab as TreeInfo, newPosition, state.single))
            {
                InstanceID cloneID = new InstanceID() { Tree = clone };
                cloneInstance = new MoveableTree(cloneID);
                buffer[clone].FixedHeight = state.fixedHeight;
            }

            return cloneInstance;
        }

        public override Instance Clone(InstanceState instanceState, Dictionary<ushort, ushort> clonedNodes)
        {
            TreeState state = instanceState as TreeState;

            Instance cloneInstance = null;
            TreeInstance[] buffer = TreeManager.instance.m_trees.m_buffer;

            if (TreeManager.instance.CreateTree(out uint clone, ref SimulationManager.instance.m_randomizer,
                state.Info.Prefab as TreeInfo, state.position, state.single))
            {
                InstanceID cloneID = new InstanceID() { Tree = clone };
                cloneInstance = new MoveableTree(cloneID);
                buffer[clone].FixedHeight = state.fixedHeight;
            }

            return cloneInstance;
        }

        public override void Delete()
        {
            if (isValid) TreeManager.instance.ReleaseTree(id.Tree);
        }

        public override Bounds GetBounds(bool ignoreSegments = true)
        {
            TreeInstance[] buffer = TreeManager.instance.m_trees.m_buffer;
            uint tree = id.Tree;
            TreeInfo info = buffer[tree].Info;

            Randomizer randomizer = new Randomizer(tree);
            float scale = info.m_minScale + (float)randomizer.Int32(10000u) * (info.m_maxScale - info.m_minScale) * 0.0001f;
            float radius = Mathf.Max(info.m_generatedInfo.m_size.x, info.m_generatedInfo.m_size.z) * scale;

            return new Bounds(buffer[tree].Position, new Vector3(radius, 0, radius));
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo, Color toolColor, Color despawnColor)
        {
            if (!isValid) return;
            if (MoveItTool.m_isLowSensitivity) return;
            toolColor = toolColor.Adjusted();

            uint tree = id.Tree;
            TreeManager treeManager = TreeManager.instance;
            TreeInfo treeInfo = treeManager.m_trees.m_buffer[tree].Info;
            Vector3 position = treeManager.m_trees.m_buffer[tree].Position;
            Randomizer randomizer = new Randomizer(tree);
            float scale = treeInfo.m_minScale + (float)randomizer.Int32(10000u) * (treeInfo.m_maxScale - treeInfo.m_minScale) * 0.0001f;
            float alpha = 1f;
            TreeTool.CheckOverlayAlpha(treeInfo, scale, ref alpha);
            toolColor.a *= alpha;
            TreeTool.RenderOverlay(cameraInfo, treeInfo, position, scale, toolColor);
        }

        public override void RenderCloneOverlay(InstanceState instanceState, ref Matrix4x4 matrix4x, Vector3 deltaPosition, float deltaAngle, Vector3 center, bool followTerrain, RenderManager.CameraInfo cameraInfo, Color toolColor)
        {
            if (MoveItTool.m_isLowSensitivity) return;
            toolColor = toolColor.Adjusted();

            TreeState state = instanceState as TreeState;

            TreeInfo info = state.Info.Prefab as TreeInfo;

            Randomizer randomizer = new Randomizer(state.instance.id.Tree);
            float scale = info.m_minScale + (float)randomizer.Int32(10000u) * (info.m_maxScale - info.m_minScale) * 0.0001f;
            //float brightness = info.m_minBrightness + (float)randomizer.Int32(10000u) * (info.m_maxBrightness - info.m_minBrightness) * 0.0001f;

            Vector3 newPosition = matrix4x.MultiplyPoint(state.position - center);
            newPosition.y = state.position.y + deltaPosition.y;

            if (followTerrain)
            {
                newPosition.y = newPosition.y - state.terrainHeight + TerrainManager.instance.SampleOriginalRawHeightSmooth(newPosition);
            }

            TreeTool.RenderOverlay(cameraInfo, info, newPosition, scale, toolColor);
        }

        public override void RenderCloneGeometry(InstanceState instanceState, ref Matrix4x4 matrix4x, Vector3 deltaPosition, float deltaAngle, Vector3 center, bool followTerrain, RenderManager.CameraInfo cameraInfo, Color toolColor)
        {
            TreeState state = instanceState as TreeState;

            TreeInfo info = state.Info.Prefab as TreeInfo;

            Randomizer randomizer = new Randomizer(state.instance.id.Tree);
            float scale = info.m_minScale + (float)randomizer.Int32(10000u) * (info.m_maxScale - info.m_minScale) * 0.0001f;
            float brightness = info.m_minBrightness + (float)randomizer.Int32(10000u) * (info.m_maxBrightness - info.m_minBrightness) * 0.0001f;

            Vector3 newPosition = matrix4x.MultiplyPoint(state.position - center);
            newPosition.y = ((MoveableTree)state.instance).GetTreeYPos(state, deltaPosition.y, newPosition, followTerrain, true);

            TreeInstance.RenderInstance(cameraInfo, info, newPosition, scale, brightness, RenderManager.DefaultColorLocation);
        }
    }
}
