using System;
using UnityEngine;

namespace VoyageRecorder
{
    /// <summary>
    /// The marker menu: a small IMGUI window listing the special locations you can record, such as the start and
    /// finish of a race. It exists only while open, frees the cursor like the game's own menus, and closes on a
    /// choice, Cancel or Escape.
    /// </summary>
    internal sealed class MarkerMenu : MonoBehaviour
    {
        // What each button records: the reading's event and the button's label.
        internal static readonly (string Event, string Label)[] Choices =
        {
            ("race-start", "Race start"),
            ("race-checkpoint", "Race checkpoint"),
            ("race-finish", "Race finish"),
            ("note", "Write a note")
        };

        private const float Width = 300f;
        private const float ButtonHeight = 34f;
        private const float Gap = 8f;

        private bool closed;
        private Action<string> choose;
        // The last thing marked, offered for undoing, or null when there's nothing to undo.
        private string undoLabel;
        private Rect window;

        public static MarkerMenu Open(MonoBehaviour owner, string undoLabel, Action<string> choose)
        {
            var menu = new GameObject("Voyage Recorder markers").AddComponent<MarkerMenu>();
            menu.transform.SetParent(owner.transform, false);
            menu.choose = choose;
            menu.undoLabel = undoLabel;
            MouseLook.ToggleMouseLookAndCursor(false);
            return menu;
        }

        public void Close()
        {
            if (closed) return;
            closed = true;
            MouseLook.ToggleMouseLookAndCursor(true);
            Destroy(gameObject);
        }

        // A modal IMGUI window takes keyboard events before the rest of OnGUI sees them, so Escape is read here.
        private void Update()
        {
            if (!closed && Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        private void OnGUI()
        {
            if (closed) return;
            float scale = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float height = 28f + (Choices.Length + (undoLabel == null ? 1 : 2)) * (ButtonHeight + Gap) + Gap;
            if (window.width == 0f) window = new Rect((Screen.width / scale - Width) / 2f, (1080f - height) / 2f, Width, height);
            window = GUI.ModalWindow(GetInstanceID(), window, Draw, "Mark this location");
        }

        private void Draw(int id)
        {
            float y = 28f;
            foreach (var choice in Choices)
            {
                if (GUI.Button(new Rect(12f, y, Width - 24f, ButtonHeight), choice.Label))
                {
                    // Closed first, so a note box opened by the choice keeps the cursor free.
                    Close();
                    choose(choice.Event);
                    return;
                }
                y += ButtonHeight + Gap;
            }
            if (undoLabel != null)
            {
                if (GUI.Button(new Rect(12f, y, Width - 24f, ButtonHeight), "Undo " + undoLabel))
                {
                    Close();
                    choose("undo");
                    return;
                }
                y += ButtonHeight + Gap;
            }
            if (GUI.Button(new Rect(12f, y, Width - 24f, ButtonHeight), "Cancel")) Close();
            GUI.DragWindow(new Rect(0f, 0f, Width, 24f));
        }
    }
}
