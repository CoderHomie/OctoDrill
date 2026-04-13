using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full-screen blue gradient: fully transparent at the top, darker and more opaque toward the bottom.
/// Builds a child under the same <see cref="Canvas"/> and keeps it behind other UI.
/// </summary>
[DisallowMultipleComponent]
public class ScreenBlueGradientOverlay : MonoBehaviour
{
    [SerializeField] Color topColor = new Color(0.82f, 0.93f, 1f, 0f);
    [SerializeField] Color bottomColor = new Color(0.01f, 0.04f, 0.14f, 0.9f);
    [Tooltip("Vertical resolution of the generated 1-pixel-wide texture.")]
    [SerializeField] int gradientResolution = 256;

    const string ChildName = "BlueGradientOverlay";

    void Awake()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            return;

        var existing = transform.Find(ChildName);
        if (existing != null)
            Destroy(existing.gameObject);

        var go = new GameObject(ChildName);
        go.transform.SetParent(transform, false);
        go.transform.SetAsFirstSibling();

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;

        var raw = go.AddComponent<RawImage>();
        raw.raycastTarget = false;
        raw.texture = BuildGradientTexture();
        raw.color = Color.white;
        raw.uvRect = new Rect(0f, 0f, 1f, 1f);
    }

    Texture2D BuildGradientTexture()
    {
        int h = Mathf.Clamp(gradientResolution, 8, 2048);
        var tex = new Texture2D(1, h, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "BlueGradientOverlayTex",
            hideFlags = HideFlags.DontSave
        };

        for (int y = 0; y < h; y++)
        {
            float t = h <= 1 ? 1f : y / (float)(h - 1);
            tex.SetPixel(0, y, Color.Lerp(bottomColor, topColor, t));
        }

        tex.Apply(false, true);
        return tex;
    }
}
