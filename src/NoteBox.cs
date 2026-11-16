using System;
using UnityEngine;

namespace VoyageRecorder
{
    /// <summary>
    /// The quick note box: a small IMGUI window with a text area, Save and Cancel. It exists only while open, so
    /// nothing of it runs otherwise. While it's open the cursor is free and the game's mouse look is off, as in the
    /// game's own menus.
    /// </summary>
    internal sealed class NoteBox : MonoBehaviour
    {
        private const string FieldName = "VoyageRecorderNote";
        // Sizes on a 1080-pixel-high screen; scaled to others.
        private const float Width = 520f;
        private const float Height = 300f;

        private string text = "";
        private bool focused;
        private bool closed;
        private Func<string, bool> save;
        private Rect window;

        // save returns false when the note couldn't be written; the box then stays open.
        public static NoteBox Open(MonoBehaviour owner, Func<string, bool> save)
        {
            var box = new GameObject("Voyage Recorder note").AddComponent<NoteBox>();
            box.transform.SetParent(owner.transform, false);
            box.save = save;
            MouseLook.ToggleMouseLookAndCursor(false);
            return box;
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
            float screenWidth = Screen.width / scale;
            if (window.width == 0f) window = new Rect((screenWidth - Width) / 2f, (1080f - Height) / 2f, Width, Height);
            window = GUI.ModalWindow(GetInstanceID(), window, Draw, "Note");
        }

        private void Draw(int id)
        {
            GUI.SetNextControlName(FieldName);
            text = GUI.TextArea(new Rect(12f, 28f, Width - 24f, Height - 80f), text, LogFormat.MaxNoteLength);
            if (!focused)
            {
                GUI.FocusControl(FieldName);
                focused = true;
            }
            if (GUI.Button(new Rect(Width - 212f, Height - 42f, 96f, 30f), "Cancel")) Close();
            else if (GUI.Button(new Rect(Width - 108f, Height - 42f, 96f, 30f), "Save"))
            {
                if (text.Trim().Length == 0 || save(text)) Close();
            }
            GUI.DragWindow(new Rect(0f, 0f, Width, 24f));
        }
    }
}
