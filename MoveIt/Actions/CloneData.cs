using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MoveIt
{
    public class CloneDataPO : CloneData
    {
        internal Vector3 m_position;
        internal float m_angle;
        internal CloneActionBase m_action;
    }

    public class CloneData
    {
        public Instance Original { get => _original ?? throw new NullReferenceException(); set => _original = value; }
        private Instance _original = null;

        public Instance Clone { get => _clone ?? throw new NullReferenceException(); set => _clone = value; }
        private Instance _clone = null;

        public InstanceState CloneState { get => _cloneState ?? throw new NullReferenceException(); set => _cloneState = value; }
        private InstanceState _cloneState = null;

        public InstanceState AdjustedState { get => _adjustedState ?? throw new NullReferenceException(); set => _adjustedState = value; }
        private InstanceState _adjustedState = null;

        public InstanceID OriginalIId => Original.id;
        public InstanceID CloneIId => Clone.id;
        public InstanceID StateIId => CloneState.instance.id;

        public static bool OriginalExists(List<CloneData> cloneData, Instance instance)
        {
            return cloneData.Any(d => d.Original == instance);
        }

        public static CloneData GetFromOriginal(List<CloneData> cloneData, Instance instance)
        {
            foreach (CloneData data in cloneData)
            {
                if (data.Original == instance) return data;
            }
            return null;
        }

        public static CloneData GetFromState(List<CloneData> cloneData, InstanceState state)
        {
            foreach (CloneData data in cloneData)
            {
                if (data.CloneState == state) return data;
            }
            return null;
        }

        internal static HashSet<Instance> GetClones(List<CloneData> cloneData)
        {
            HashSet<Instance> result = new HashSet<Instance>();
            if (cloneData == null) return result;

            foreach (CloneData data in cloneData)
            {
                if (data != null && data.Clone != null) result.Add(data.Clone);
            }
            return result;
        }
    }
}
