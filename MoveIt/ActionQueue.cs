using System.Collections.Generic;
using UnifiedUI.Helpers;
using UnityEngine;

namespace MoveIt
{
    public class ActionQueue
    {
        private Action[] m_actions = new Action[50];
        private int m_current = 0;
        private int m_head = 0;
        private int m_tail = 0;

        public static ActionQueue instance;

        public void UpdateNodeIdInStateHistory(ushort oldId, ushort newId)
        {
            foreach (Action action in _getPreviousAction())
            {
                if (action == null) continue;
                action.UpdateNodeIdInSegmentState(oldId, newId);
            }
        }

        private IEnumerable<Action> _getPreviousAction()
        {
            int tail = (m_tail > m_head) ? m_tail - m_actions.Length : m_tail;
            int curr = (m_current > m_head) ? m_current - m_actions.Length : m_current;

            for (int i = curr - 1; i >= tail; i--)
            {
                if (i < 0)
                {
                    yield return m_actions[i + m_actions.Length];
                }
                else
                {
                    yield return m_actions[i];
                }
            }
        }

        public void Push(Action action)
        {
            m_current = (m_current + 1) % m_actions.Length;
            m_head = m_current;
            if (m_tail == m_head)
            {
                m_tail = (m_tail + 1) % m_actions.Length;
            }

            // Clean up previous actions
            //if (m_actions[m_current] is MonoBehaviour)
            //{
            //    Debug.Log($"Destroying MonoBehaviour action");
            //    m_actions[m_current].Destroy();
            //    m_actions[m_current] = null;
            //}

            m_actions[m_current] = action;

            DebugUtils.Log("ActionQueue Push(" + m_current + ", " + m_tail + "): " + m_actions[m_current].GetType());
        }

        public bool Redo()
        {
            if (m_current == m_head)
            {
                return false;
            }

            m_current = (m_current + 1) % m_actions.Length;
            m_actions[m_current].Do();

            DebugUtils.Log("ActionQueue Redo(" + m_current + "): " + m_actions[m_current].GetType());

            return true;
        }

        public bool Undo()
        {
            if (m_current == m_tail)
            {
                return false;
            }

            m_actions[m_current].Undo();

            DebugUtils.Log("ActionQueue Undo(" + m_current + "): " + m_actions[m_current].GetType());

            if (m_current == 0)
            {
                m_current = m_actions.Length - 1;
            }
            else
            {
                m_current--;
            }
            
            return true;
        }

        public bool Do()
        {
            Log.Debug($"AAA05");
            if (m_current == m_tail)
            {
                return false;
            }

            m_actions[m_current].Do();

            DebugUtils.Log("ActionQueue Do(" + m_current + "): " + m_actions[m_current].GetType());

            return true;
        }

        public void Invalidate()
        {
            if(m_head != m_current)
            {
                DebugUtils.Log("ActionQueue Invalidate(" + m_current + ", " + m_head + ")");
            }
            m_head = m_current;
        }

        public void Clear()
        {
            m_current = 0;
            m_head = 0;
            m_tail = 0;
        }

        public void ReplaceInstancesForward(List<CloneData> toReplace)
        {
            int action = m_current;

            while (action != m_head)
            {
                action = (action + 1) % m_actions.Length;
                m_actions[action].ReplaceInstances(toReplace);
            }
        }

        public void ReplaceInstancesBackward(List<CloneData> toReplace)
        {
            int action = m_current;

            if (action == 0)
            {
                action = m_actions.Length - 1;
            }
            else
            {
                action--;
            }

            while (action != m_tail)
            {
                m_actions[action].ReplaceInstances(toReplace);

                if (action == 0)
                {
                    action = m_actions.Length - 1;
                }
                else
                {
                    action--;
                }
            }
        }

        public Action current
        {
            get
            {
                if (m_current == m_tail)
                {
                    return null;
                }

                return m_actions[m_current];
            }
        }

        public void CleanQueue()
        {
            //for (int i = 0; i < m_actions.Length; i++)
            //{
            //    if (m_actions[i] is MonoBehaviour)
            //    {
            //        m_actions[i].Destroy();
            //        m_actions[i] = null;
            //    }
            //}
        }

        public string DebugQueue()
        {
            string t = (current == null ? "null" : current.GetType().ToString());
            return $"{m_current} ({m_tail}/{m_head}) - <{t}>";
        }
    }
}
