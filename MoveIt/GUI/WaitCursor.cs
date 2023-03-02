using ColossalFramework;
using ColossalFramework.UI;
using QCommonLib;
using UnityEngine;

namespace MoveIt
{
    internal class WaitCursor : MonoBehaviour
    {
        internal static WaitCursor instance;

        internal static WaitCursor Initialise()
        {
            var go = new GameObject("MIT_CursorManager");
            go.AddComponent<WaitCursor>();
            instance = go.GetComponent<WaitCursor>();

            return instance;
        }

        internal static void Close()
        {
            Destroy(instance.gameObject);
            instance = null;
        }

        private bool active = false;
        private long activationMS = 0;
        private QTimer timer;
        private readonly CursorInfo[] cursors = new CursorInfo[8];
        private UITextureAtlas atlas;

        internal void Start()
        {
            atlas = QTextures.CreateTextureAtlas("Cursor", new[] { "icon-wait-00", "icon-wait-01", "icon-wait-02", "icon-wait-03", "icon-wait-04", "icon-wait-05", "icon-wait-06", "icon-wait-07" }, "MoveIt.Icons.Cursors.");
            for (int i = 0; i < cursors.Length; i++)
            {
                cursors[i] = ScriptableObject.CreateInstance<CursorInfo>();
                cursors[i].m_texture = atlas.sprites[i].texture;
            }
            timer = new QTimer();
        }

        internal void Update()
        {
            if (active != MoveItTool.TaskManager.Active)
            {
                active = !active;
                if (active)
                { // Activating
                    activationMS = timer.MS;
                }
                else
                { // Deactivating
                    MoveItTool.instance.SetCursor(null);
                }
            }

            //if (active) Log.Debug($"Active phase:{GetPhase()} [{(float)timer.Seconds % 1}, {timer.MS - activationMS}]");

            if (active && (timer.MS - activationMS) > 100)
            {
                MoveItTool.instance.SetCursor(cursors[GetPhase()]);
            }
        }

        private int GetPhase()
        {
            float fraction = (float)timer.Seconds % 1;

            return Mathf.FloorToInt(fraction * 8);
        }
    }
}
