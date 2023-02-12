﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace MoveIt
{
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

        public static HashSet<Instance> GetClones(List<CloneData> cloneData)
        {
            HashSet<Instance> result = new HashSet<Instance>();
            foreach (CloneData data in cloneData)
            {
                result.Add(data.Clone);
            }
            return result;
        }
    }
}
