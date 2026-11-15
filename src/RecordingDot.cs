using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace VoyageRecorder
{
    internal enum DotCorner { TopLeft, TopRight, BottomLeft, BottomRight }

    internal enum DotSize { Small, Large }

    /// <summary>
    /// A flashing red dot in a corner of the screen while recording. It is a Unity UI overlay that Unity draws
    /// itself, and the flashing runs on native waits, so no code of ours runs per frame.
    /// </summary>
    internal sealed class RecordingDot
    {
        // Sizes and margin in pixels on a 1080-pixel-high screen; the canvas scales them to others.
        private const float ReferenceHeight = 1080f;
        private const float Margin = 24f;
        private static readonly Color Red = new Color(0.85f, 0.1f, 0.1f, 0.9f);
        // Seconds shown and hidden in each flash.
        private const float OnSeconds = 1f;
        private const float OffSeconds = 0.5f;

        private readonly MonoBehaviour owner;
        private readonly GameObject canvas;
        private readonly RectTransform dot;
        private readonly Image picture;
        private Coroutine flashing;

        public RecordingDot(MonoBehaviour owner)
        {
            this.owner = owner;
            canvas = new GameObject("Voyage Recorder dot", typeof(Canvas), typeof(CanvasScaler));
            canvas.transform.SetParent(owner.transform, false);
            var overlay = canvas.GetComponent<Canvas>();
            overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            overlay.sortingOrder = short.MaxValue;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceHeight * 16f / 9f, ReferenceHeight);
            scaler.matchWidthOrHeight = 1f;

            var image = new GameObject("dot", typeof(RectTransform), typeof(Image));
            image.transform.SetParent(canvas.transform, false);
            picture = image.GetComponent<Image>();
            picture.sprite = Circle();
            picture.color = Red;
            picture.raycastTarget = false;
            dot = image.GetComponent<RectTransform>();
            canvas.SetActive(false);
        }

        public void Show(DotCorner corner, DotSize size)
        {
            float x = corner == DotCorner.TopLeft || corner == DotCorner.BottomLeft ? 0f : 1f;
            float y = corner == DotCorner.BottomLeft || corner == DotCorner.BottomRight ? 0f : 1f;
            dot.anchorMin = dot.anchorMax = dot.pivot = new Vector2(x, y);
            dot.anchoredPosition = new Vector2(x == 0f ? Margin : -Margin, y == 0f ? Margin : -Margin);
            float diameter = size == DotSize.Large ? 32f : 16f;
            dot.sizeDelta = new Vector2(diameter, diameter);
            canvas.SetActive(true);
            if (flashing != null) owner.StopCoroutine(flashing);
            flashing = owner.StartCoroutine(Flash());
        }

        public void Hide()
        {
            if (flashing != null) owner.StopCoroutine(flashing);
            flashing = null;
            canvas.SetActive(false);
        }

        // The waits run in game time, which sleep speeds up, so each is stretched by the time scale to keep the
        // flashing steady; Show starts it again when the time scale changes. While the game is paused the dot
        // holds still.
        private IEnumerator Flash()
        {
            while (true)
            {
                picture.enabled = true;
                yield return new WaitForSeconds(OnSeconds * Mathf.Max(Time.timeScale, 1f));
                picture.enabled = false;
                yield return new WaitForSeconds(OffSeconds * Mathf.Max(Time.timeScale, 1f));
            }
        }

        // A white disc with a soft one-pixel edge, tinted red by the image.
        private static Sprite Circle()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.ARGB32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float radius = size / 2f;
            for (int row = 0; row < size; row++)
                for (int column = 0; column < size; column++)
                {
                    float distance = new Vector2(column + 0.5f - radius, row + 0.5f - radius).magnitude;
                    byte alpha = (byte)(Mathf.Clamp01(radius - distance) * 255f);
                    pixels[row * size + column] = new Color32(255, 255, 255, alpha);
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
