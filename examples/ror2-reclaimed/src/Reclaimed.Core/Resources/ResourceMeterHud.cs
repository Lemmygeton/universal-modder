using System.Collections.Generic;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Reclaimed.Core.Resources
{
    /// <summary>
    /// Draws one bar per <see cref="ResourceMeter"/> (with showOnHud) of the body the HUD is following, just
    /// under the vanilla health bar. Built from plain UnityEngine.UI at runtime (no asset bundle); the label
    /// reuses the health bar's TextMeshPro font when one is found. Rebuilds itself when the target changes.
    /// </summary>
    public sealed class ResourceMeterHud : MonoBehaviour
    {
        private const float BarHeight = 10f;
        private const float BarGap = 4f;

        private static Sprite whiteSprite;

        private HUD hud;
        private GameObject trackedBody;
        private readonly List<Bar> bars = new List<Bar>();

        private sealed class Bar
        {
            public ResourceMeter Meter;
            public GameObject Root;
            public Image Fill;
            public Image Background;
            public TextMeshProUGUI Label;
            public Text FallbackLabel;
            public float Shown = -1f;
        }

        internal static void Install()
        {
            On.RoR2.UI.HUD.Awake += (orig, self) =>
            {
                orig(self);
                if (self.GetComponent<ResourceMeterHud>() == null) self.gameObject.AddComponent<ResourceMeterHud>().hud = self;
            };
        }

        private void LateUpdate()
        {
            if (hud == null) hud = GetComponent<HUD>();
            if (hud == null) return;
            GameObject target = hud.targetBodyObject;
            if (target != trackedBody || BarsStale())
            {
                trackedBody = target;
                Rebuild();
            }
            for (int i = 0; i < bars.Count; i++) Refresh(bars[i]);
        }

        private bool BarsStale()
        {
            for (int i = 0; i < bars.Count; i++)
                if (bars[i].Meter == null || bars[i].Root == null) return true;
            return false;
        }

        private void Rebuild()
        {
            for (int i = 0; i < bars.Count; i++)
                if (bars[i].Root != null) Destroy(bars[i].Root);
            bars.Clear();
            if (trackedBody == null || HealthBarOf(hud) == null) return;

            ResourceMeter[] meters = trackedBody.GetComponents<ResourceMeter>();
            int shown = 0;
            for (int i = 0; i < meters.Length; i++)
            {
                if (!meters[i].showOnHud) continue;
                Bar bar = CreateBar(meters[i], shown++);
                if (bar != null) bars.Add(bar);
            }
        }

        private Bar CreateBar(ResourceMeter meter, int index)
        {
            RectTransform health = HealthBarOf(hud).transform as RectTransform;
            if (health == null || health.parent == null) return null;

            var root = new GameObject("ReclaimedMeter_" + meter.MeterIndex, typeof(RectTransform));
            var rt = (RectTransform)root.transform;
            rt.SetParent(health.parent, false);

            LayoutGroup group = health.parent.GetComponent<LayoutGroup>();
            if (group != null)
            {
                // The vanilla bar sits in a layout group: become the next element in it.
                rt.SetSiblingIndex(health.GetSiblingIndex() + 1 + index);
                LayoutElement layout = root.AddComponent<LayoutElement>();
                layout.preferredHeight = BarHeight;
                layout.minHeight = BarHeight;
                layout.flexibleWidth = 1f;
            }
            else
            {
                rt.anchorMin = health.anchorMin;
                rt.anchorMax = health.anchorMax;
                rt.pivot = new Vector2(health.pivot.x, 1f);
                float healthBottom = health.anchoredPosition.y - health.rect.height * health.pivot.y;
                rt.anchoredPosition = new Vector2(health.anchoredPosition.x, healthBottom - BarGap - index * (BarHeight + BarGap));
                rt.sizeDelta = new Vector2(health.sizeDelta.x, BarHeight);
            }

            var bar = new Bar { Meter = meter, Root = root };
            bar.Background = AddImage(rt, "Background", new Color(0f, 0f, 0f, 0.6f), false);
            bar.Fill = AddImage(rt, "Fill", meter.color, true);
            AddSegments(rt, meter.segments);
            AddLabel(rt, bar);
            Refresh(bar);
            return bar;
        }

        // The player's own bar: HUD.healthBar when the field exists, else the bar in the bottom-left cluster.
        private HealthBar cachedHealthBar;

        private HealthBar HealthBarOf(HUD target)
        {
            if (cachedHealthBar != null) return cachedHealthBar;
            cachedHealthBar = Reflect.GetField<HealthBar>(target, "healthBar");
            if (cachedHealthBar == null)
            {
                foreach (HealthBar bar in target.GetComponentsInChildren<HealthBar>(true))
                {
                    if (bar.transform.parent != null && bar.GetComponentInParent<AllyCardController>() == null)
                    {
                        cachedHealthBar = bar;
                        break;
                    }
                }
            }
            return cachedHealthBar;
        }

        private static Image AddImage(RectTransform parent, string name, Color color, bool filled)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Stretch(rt);
            Image image = go.AddComponent<Image>();
            image.sprite = WhiteSprite;
            image.color = color;
            image.raycastTarget = false;
            if (filled)
            {
                image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Horizontal;
                image.fillOrigin = (int)Image.OriginHorizontal.Left;
            }
            return image;
        }

        private static void AddSegments(RectTransform parent, int segments)
        {
            for (int i = 1; i < segments; i++)
            {
                var go = new GameObject("Segment" + i, typeof(RectTransform));
                var rt = (RectTransform)go.transform;
                rt.SetParent(parent, false);
                float x = (float)i / segments;
                rt.anchorMin = new Vector2(x, 0f);
                rt.anchorMax = new Vector2(x, 1f);
                rt.sizeDelta = new Vector2(2f, 0f);
                rt.anchoredPosition = Vector2.zero;
                Image image = go.AddComponent<Image>();
                image.sprite = WhiteSprite;
                image.color = new Color(0f, 0f, 0f, 0.75f);
                image.raycastTarget = false;
            }
        }

        private void AddLabel(RectTransform parent, Bar bar)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Stretch(rt);
            rt.offsetMin = new Vector2(4f, -2f);
            rt.offsetMax = new Vector2(-4f, 2f);

            TextMeshProUGUI template = HealthBarOf(hud).GetComponentInChildren<TextMeshProUGUI>(true);
            if (template != null)
            {
                TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
                label.font = template.font;
                label.fontSharedMaterial = template.fontSharedMaterial;
                label.fontSize = BarHeight;
                label.alignment = TextAlignmentOptions.MidlineRight;
                label.raycastTarget = false;
                label.enableWordWrapping = false;
                bar.Label = label;
            }
            else
            {
                Text label = go.AddComponent<Text>();
                label.font = UnityEngine.Resources.GetBuiltinResource<Font>("Arial.ttf");
                label.fontSize = 9;
                label.alignment = TextAnchor.MiddleRight;
                label.raycastTarget = false;
                bar.FallbackLabel = label;
            }
        }

        private static void Refresh(Bar bar)
        {
            if (bar.Meter == null || bar.Fill == null) return;
            float fraction = bar.Meter.Fraction;
            bar.Fill.fillAmount = fraction;
            bar.Fill.color = bar.Meter.color;
            float value = Mathf.Floor(bar.Meter.Current);
            if (Mathf.Approximately(value, bar.Shown)) return;
            bar.Shown = value;
            string name = string.IsNullOrEmpty(bar.Meter.displayNameToken) ? "" : Language.GetString(bar.Meter.displayNameToken) + "  ";
            string text = name + value.ToString("0") + " / " + Mathf.Floor(bar.Meter.Max).ToString("0");
            if (bar.Label != null) bar.Label.text = text;
            else if (bar.FallbackLabel != null) bar.FallbackLabel.text = text;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Sprite WhiteSprite
        {
            get
            {
                if (whiteSprite == null)
                {
                    Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                    var pixels = new Color32[16];
                    for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
                    tex.SetPixels32(pixels);
                    tex.Apply();
                    whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
                }
                return whiteSprite;
            }
        }
    }
}
