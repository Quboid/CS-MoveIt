using ColossalFramework;
using EManagersLib.API;
using UnityEngine;

namespace MoveIt
{
    public interface IProp
    {
        Vector3 Position { get; set; }
        float Angle { get; set; }
        bool FixedHeight { get; set; }
        bool Single { get; set; }
        ushort m_flags { get; set; }
        PropInfo Info { get; }
        uint Index { get; }

        void MoveProp(Vector3 position);
        void UpdatePropRenderer(bool updateGroup);
        void ReleaseProp();
    }

    class PropWrapper : IProp
    {
        private readonly ushort index;
        private readonly PropInstance[] Buffer = PropManager.instance.m_props.m_buffer;

        public void MoveProp(Vector3 position)
        {
            PropManager.instance.MoveProp(index, position);
        }

        public void UpdatePropRenderer(bool updateGroup)
        {
            PropManager.instance.UpdatePropRenderer(index, updateGroup);
        }

        public void ReleaseProp()
        {
            PropManager.instance.ReleaseProp(index);
        }

        public uint Index => index;


        public PropWrapper(ushort i)
        {
            index = i;
        }

        public Vector3 Position
        {
            get => Buffer[index].Position;
            set => Buffer[index].Position = value;
        }

        public float Angle
        {
            get => Buffer[index].Angle;
            set => Buffer[index].Angle = value;
        }

        public bool FixedHeight
        {
            get => Buffer[index].FixedHeight;
            set => Buffer[index].FixedHeight = value;
        }

        public bool Single
        {
            get => Buffer[index].Single;
            set => Buffer[index].Single = value;
        }

        public ushort m_flags
        {
            get => Buffer[index].m_flags;
            set => Buffer[index].m_flags = value;
        }

        public PropInfo Info
        {
            get => Buffer[index].Info;
        }
    }

    // Extended Managers Library support
    class EPropWrapper : IProp
    {
        private readonly uint index;
        public uint Index => index;

        public EPropWrapper(uint i)
        {
            index = i;
        }

        public void MoveProp(Vector3 position)
        {
            PropAPI.Wrapper.MoveProp(index, position);
        }

        public void UpdatePropRenderer(bool updateGroup)
        {
            PropAPI.Wrapper.UpdatePropRenderer(index, updateGroup);
        }

        public void ReleaseProp()
        {
            PropAPI.Wrapper.ReleaseProp(index);
        }

        public Vector3 Position
        {
            get => PropAPI.Wrapper.GetPosition(index);
            set => PropAPI.Wrapper.SetPosition(index, value);
        }

        public float Angle
        {
            get => PropAPI.Wrapper.GetAngle(index);
            set => PropAPI.Wrapper.SetAngle(index, value);
        }

        public bool FixedHeight
        {
            get => PropAPI.Wrapper.GetFixedHeight(index);
            set => PropAPI.Wrapper.SetFixedHeight(index, value);
        }

        public bool Single
        {
            get => PropAPI.Wrapper.GetSingle(index);
            set => PropAPI.Wrapper.SetSingle(index, value);
        }

        public ushort m_flags
        {
            get => PropAPI.Wrapper.GetFlags(index);
            set => PropAPI.Wrapper.SetFlags(index, value);
        }

        public PropInfo Info
        {
            get => PropAPI.Wrapper.GetInfo(index);
        }
    }
}