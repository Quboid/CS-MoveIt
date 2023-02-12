using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MoveIt
{
    class AlignSlopeAction : Action
    {
        public enum Modes
        {
            Quick, Auto, Full
        }
        public Modes mode = Modes.Full;

        public HashSet<InstanceState> m_states = new HashSet<InstanceState>();

        private Instance[] keyInstance = new Instance[2];

        public Instance PointA
        {
            get
            {
                return keyInstance[0];
            }
            set
            {
                keyInstance[0] = value;
            }
        }
        public Instance PointB
        {
            get
            {
                return keyInstance[1];
            }
            set
            {
                keyInstance[1] = value;
            }
        }

        public bool followTerrain;

        public AlignSlopeAction() : base()
        {
            foreach (Instance instance in selection)
            {
                if (instance.isValid)
                {
                    m_states.Add(instance.SaveToState(false));
                }
            }
        }

        public override void Do()
        {
            float angleDelta;
            float heightDelta;
            float distance;
            Matrix4x4 matrix = default;

            if (mode == Modes.Quick)
            {
                if (selection.Count != 1) return;
                Instance instance = selection.First();

                if (!instance.isValid || !(instance is MoveableNode nodeInstance)) return;

                NetNode node = nodeBuffer[nodeInstance.id.NetNode];

                int c = 0;
                for (int i = 0; i < 8; i++)
                {
                    ushort segId;
                    if ((segId = node.GetSegment(i)) > 0)
                    {
                        if (c > 1) return; // More than 2 segments found

                        NetSegment segment = segmentBuffer[segId];
                        InstanceID instanceID = default;
                        if (segment.m_startNode == nodeInstance.id.NetNode)
                        {
                            instanceID.NetNode = segment.m_endNode;
                        }
                        else
                        {
                            instanceID.NetNode = segment.m_startNode;
                        }
                        keyInstance[c] = new MoveableNode(instanceID);
                        c++;
                    }
                }
            }
            else if (mode == Modes.Auto)
            {
                if (selection.Count < 2) return;
                GetExtremeObjects(out keyInstance[0], out keyInstance[1]);
            }

            angleDelta = 0 - (float)Math.Atan2(PointB.position.z - PointA.position.z, PointB.position.x - PointA.position.x);
            heightDelta = PointB.position.y - PointA.position.y;
            distance = (float)Math.Sqrt(Math.Pow(PointB.position.z - PointA.position.z, 2) + Math.Pow(PointB.position.x - PointA.position.x, 2));

            foreach (InstanceState state in m_states)
            {
                float distanceOffset, heightOffset;
                matrix.SetTRS(PointA.position, Quaternion.AngleAxis(angleDelta * Mathf.Rad2Deg, Vector3.down), Vector3.one);
                distanceOffset = (matrix.MultiplyPoint(state.position - PointA.position) - PointA.position).x;
                heightOffset = distanceOffset / distance * heightDelta;

                state.instance.SetHeight(Mathf.Clamp(PointA.position.y + heightOffset, 0f, 1000f));
            }
        }

        public override void Undo()
        {
            foreach (InstanceState state in m_states)
            {
                state.instance.LoadFromState(state);
            }

            UpdateArea(GetTotalBounds(false));
        }

        public override void ReplaceInstances(List<CloneData> toReplace)
        {
            // Update this action's state instances with the updated instances
            foreach (InstanceState state in m_states)
            {
                CloneData data = CloneData.GetFromOriginal(toReplace, state.instance);
                if (data != null)
                {
                    Log.Debug($"AlignSlopeAction Replacing: {state.instance.id.Debug()}/{data.OriginalIId.Debug()} -> {data.CloneIId.Debug()}", "[M78.3]");
                    state.ReplaceInstance(data.Clone);
                }
            }
        }
    }
}
