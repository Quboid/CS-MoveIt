using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.Plugins;
using EManagersLib.API;
using QCommonLib.QTasks;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace MoveIt
{
    static class PropLayer
    {
        public static IPropsWrapper Manager;

        internal static bool EML = false;

        public static void Initialise()
        {
            EML = isEMLInstalled();
            Log.Debug($"Move It EML: {EML}");

            if (EML)
            {
                Manager = new EPropsManager();
            }
            else
            {
                Manager = new PropsManager();
            }
        }

        internal static bool isEMLInstalled()
        {
            foreach (PluginManager.PluginInfo pluginInfo in Singleton<PluginManager>.instance.GetPluginsInfo())
            {
                foreach (Assembly assembly in pluginInfo.GetAssemblies())
                {
                    if (assembly.GetName().Name.ToLower().Equals("emanagerslib"))
                    {
                        return pluginInfo.isEnabled;
                    }
                }
            }

            return false;
        }

        internal static string getVersionText()
        {
            if (isEMLInstalled())
            {
                return "Extended Managers Library: Found";
            }

            return "Extended Managers Library: Not Found";
        }
    }

    public interface IPropsWrapper
    {
        IInfo GetInfo(uint id);
        IInfo GetInfo(InstanceID id);
        IProp Buffer(uint id);
        IProp Buffer(InstanceID id);
        uint GetId(InstanceID id);
        float GetScale(InstanceID id, IProp prop);
        InstanceID SetProp(InstanceID id, uint i);
        void UpdateProps(float minX, float minZ, float maxX, float maxZ);
        void TouchProps();
        bool CreateProp(out uint clone, PropInfo info, Vector3 position, float angle, bool single);
        bool GetFixedHeight(InstanceID id);
        void SetFixedHeight(InstanceID id, bool fixedHeight);
        InstanceID StepOver(uint id);
        void RaycastHoverInstance(ref int i, ref int j, ref StepOver stepOver, ref Segment3 ray, ref float smallestDist, ref InstanceID id, ItemClass.Layer itemLayers);
        void GetMarqueeList(ref int i, ref int j, ref InstanceID id, ref Quad3 m_selection, ref HashSet<Instance> list, ItemClass.Layer itemLayers);
        bool GetSnappingState();
    }

    class PropsManager : IPropsWrapper
    {
        private readonly PropInstance[] propBuffer;

        public PropsManager()
        {
            propBuffer = Singleton<PropManager>.instance.m_props.m_buffer;
        }

        public IInfo GetInfo(uint id)
        {
            return new Info_Prefab(PropManager.instance.m_props.m_buffer[(ushort)id].Info);
        }

        public IInfo GetInfo(InstanceID id)
        {
            return new Info_Prefab(PropManager.instance.m_props.m_buffer[id.Prop].Info);
        }

        public IProp Buffer(uint id)
        {
            return new PropWrapper((ushort)id);
        }

        public IProp Buffer(InstanceID id)
        {
            return new PropWrapper(id.Prop);
        }

        public uint GetId(InstanceID id)
        {
            return id.Prop;
        }

        public float GetScale(InstanceID id, IProp prop)
        {
            Randomizer randomizer = new Randomizer(prop.Index);
            return prop.Info.m_minScale + (float)randomizer.Int32(10000u) * (prop.Info.m_maxScale - prop.Info.m_minScale) * 0.0001f;
        }

        public InstanceID SetProp(InstanceID id, uint i)
        {
            id.Prop = (ushort)i;
            return id;
        }

        public void UpdateProps(float minX, float minZ, float maxX, float maxZ)
        {
            Singleton<PropManager>.instance.UpdateProps(minX, minZ, maxX, maxZ);
        }

        public bool CreateProp(out uint clone, PropInfo info, Vector3 position, float angle, bool single)
        {
            bool result = PropManager.instance.CreateProp(out ushort tempId, ref SimulationManager.instance.m_randomizer, info, position, angle, single);
            clone = tempId;
            return result;
        }

        public bool GetFixedHeight(InstanceID id) => propBuffer[id.Prop].FixedHeight;

        public void SetFixedHeight(InstanceID id, bool fixedHeight)
        {
            propBuffer[id.Prop].FixedHeight = fixedHeight;
        }

        public InstanceID StepOver(uint id)
        {
            InstanceID instance = default;
            instance.Prop = (ushort)id;
            return instance;
        }

        public void TouchProps()
        {
            int bufferLen = Math.Min(ushort.MaxValue, Singleton<PropManager>.instance.m_props.m_buffer.Length);

            for (ushort i = 0; i < bufferLen; i++)
            {
                ref PropInstance prop = ref Singleton<PropManager>.instance.m_props.m_buffer[i];

                if (((PropInstance.Flags)prop.m_flags & PropInstance.Flags.Created) == PropInstance.Flags.Created)
                {
                    QTaskManager.QueueOnSimulation(() => { Singleton<PropManager>.instance.UpdateProp(i); });
                }
            }
        }

        public void RaycastHoverInstance(ref int i, ref int j, ref StepOver stepOver, ref Segment3 ray, ref float smallestDist, ref InstanceID id, ItemClass.Layer itemLayers)
        {
            ushort prop = PropManager.instance.m_propGrid[i * 270 + j];
            int count = 0;
            while (prop != 0u)
            {
                if (stepOver.isValidP(prop) && IsPropValid(ref propBuffer[prop], itemLayers) && Filters.Filter(propBuffer[prop].Info))
                {
                    if (propBuffer[prop].RayCast(prop, ray, out float t, out float targetSqr) && t < smallestDist)
                    {
                        id.Prop = prop;
                        smallestDist = t;
                    }
                }

                prop = propBuffer[prop].m_nextGridProp;

                if (++count > 65536)
                {
                    CODebugBase<LogChannel>.Error(LogChannel.Core, "Props: Invalid list detected!\n" + Environment.StackTrace);
                }
            }
        }

        public void GetMarqueeList(ref int i, ref int j, ref InstanceID id, ref Quad3 m_selection, ref HashSet<Instance> list, ItemClass.Layer itemLayers)
        {
            ushort prop = PropManager.instance.m_propGrid[i * 270 + j];
            int count = 0;
            while (prop != 0u)
            {
                if (IsPropValid(ref propBuffer[prop], itemLayers) && Filters.Filter(propBuffer[prop].Info))
                {
                    if (MoveItTool.instance.PointInRectangle(m_selection, propBuffer[prop].Position))
                    {
                        id.Prop = prop;
                        list.Add(id);
                    }
                }

                prop = propBuffer[prop].m_nextGridProp;

                if (++count > 65536)
                {
                    CODebugBase<LogChannel>.Error(LogChannel.Core, "Prop: Invalid list detected!\n" + Environment.StackTrace);
                }
            }
        }

        private bool IsPropValid(ref PropInstance prop, ItemClass.Layer itemLayers)
        {
            if (MoveItTool.superSelect) return true;
            if (((PropInstance.Flags)prop.m_flags & PropInstance.Flags.Created) == PropInstance.Flags.Created)
            {
                return (prop.Info.m_class.m_layer & itemLayers) != ItemClass.Layer.None;
            }

            return false;
        }

        public bool GetSnappingState() => false;
    }

    // Extended Managers Library support
    class EPropsManager : IPropsWrapper
    {
        public EPropsManager()
        {
            PropAPI.Initialize();
        }

        public IInfo GetInfo(uint id)
        {
            return new Info_Prefab(PropAPI.Wrapper.GetInfo(id));
        }

        public IInfo GetInfo(InstanceID id)
        {
            return new Info_Prefab(PropAPI.Wrapper.GetInfo(id.GetProp32()));
        }

        public IProp Buffer(uint id)
        {
            return new EPropWrapper(id);
        }

        public IProp Buffer(InstanceID id)
        {
            return new EPropWrapper(id.GetProp32());
        }

        public uint GetId(InstanceID id)
        {
            return id.GetProp32();
        }

        public float GetScale(InstanceID id, IProp prop)
        {
            return PropAPI.Wrapper.GetScale(id);
        }

        public InstanceID SetProp(InstanceID id, uint i)
        {
            id.SetProp32(i);
            return id;
        }

        public void UpdateProps(float minX, float minZ, float maxX, float maxZ)
        {
            PropAPI.Wrapper.UpdateProps(minX, minZ, maxX, maxZ);
        }

        public bool CreateProp(out uint clone, PropInfo info, Vector3 position, float angle, bool single)
        {
            return PropAPI.Wrapper.CreateProp(out clone, info, position, angle, single);
        }

        public bool GetFixedHeight(InstanceID id) => PropAPI.Wrapper.GetFixedHeight(id);

        public void SetFixedHeight(InstanceID id, bool fixedHeight)
        {
            PropAPI.Wrapper.SetFixedHeight(id, fixedHeight);
        }

        public InstanceID StepOver(uint id)
        {
            InstanceID instance = default;
            instance.SetProp32(id);
            return instance;
        }

        public void TouchProps()
        {
            // Adjusted to accomodate new buffer size and validity of prop
            int bufferLen = PropAPI.PropBufferLen;

            for (uint i = 0; i < bufferLen; i++)
            {
                if (PropAPI.Wrapper.IsValid(i))
                {
                    PropAPI.Wrapper.UpdateProp(i);
                }
            }
        }

        public void RaycastHoverInstance(ref int i, ref int j, ref StepOver stepOver, ref Segment3 ray, ref float smallestDist, ref InstanceID id, ItemClass.Layer itemLayers)
        {
            foreach (uint propID in PropAPI.Wrapper.GetPropGridEnumerable(i, j))
            {
                if (stepOver.isValidP(propID) && IsPropValid(propID, itemLayers) && Filters.Filter(PropAPI.Wrapper.GetInfo(propID)))
                {
                    if (PropAPI.Wrapper.RayCast(propID, ray, out float t, out float targetSqr) && t < smallestDist)
                    {
                        id.SetProp32(propID);
                        smallestDist = t;
                    }
                }
            }
        }

        public void GetMarqueeList(ref int i, ref int j, ref InstanceID id, ref Quad3 m_selection, ref HashSet<Instance> list, ItemClass.Layer itemLayers)
        {
            foreach (uint propID in PropAPI.Wrapper.GetPropGridEnumerable(i, j))
            {
                if (IsPropValid(propID, itemLayers) && Filters.Filter(PropAPI.Wrapper.GetInfo(propID)))
                {
                    if (MoveItTool.instance.PointInRectangle(m_selection, PropAPI.Wrapper.GetPosition(propID)))
                    {
                        id.SetProp32(propID);
                        list.Add(id);
                    }
                }
            }
        }

        private bool IsPropValid(uint propID, ItemClass.Layer itemLayers)
        {
            if (MoveItTool.superSelect) return true;
            if (((PropInstance.Flags)Buffer(propID).m_flags & PropInstance.Flags.Created) == PropInstance.Flags.Created)
            {
                return (PropAPI.Wrapper.GetInfo(propID).m_class.m_layer & itemLayers) != ItemClass.Layer.None;
            }

            return false;
        }

        public bool GetSnappingState() => PropAPI.Wrapper.IsSnappingEnabled;
    }
}
