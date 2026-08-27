using UnityEngine;
using UnityEngine.UI;

namespace AssetGarage.CombatDemo
{
    public sealed class TimingVisualizer : MonoBehaviour
    {
        public TimingViewKind Kind { get; set; }

        private TimingPresentationConfig config;
        private RectTransform cursor, pin;
        private Image normal, great, extreme;
        private TimingGuideGraphic circleGuides, targetRing, movingRing, radialTrack;

        public void Initialize(TimingPresentationConfig presentation)
        {
            config = presentation;
            Kind = config.DefaultTimingView;
            normal = Block("Normal", config.NormalGradeColor, new Vector2(0, .35f), new Vector2(1, .65f));
            great = Block("Great", config.GreatGradeColor, new Vector2(0, .35f), new Vector2(1, .65f));
            extreme = Block("Extreme", config.ExtremeGradeColor, new Vector2(0, .35f), new Vector2(1, .65f));
            cursor = Block("Cursor", Color.white, new Vector2(0, .25f), new Vector2(.008f, .75f)).rectTransform;

            circleGuides = Guide("CircleGradeGuides", TimingGuideGraphic.GuideMode.ConcentricBands, Color.white);
            targetRing = Guide("TargetRing", TimingGuideGraphic.GuideMode.Ring, new Color(1, 1, 1, .65f));
            targetRing.SetRingRadius(config.TargetRadius);
            movingRing = Guide("MovingTimingRing", TimingGuideGraphic.GuideMode.Ring, Color.cyan);
            radialTrack = Guide("RadialGradeTrack", TimingGuideGraphic.GuideMode.RadialTrack, Color.white);

            pin = Block("RadialPin", Color.white, new Vector2(.5f, .5f), new Vector2(.5f, .5f)).rectTransform;
            pin.sizeDelta = new Vector2(config.PinWidth, config.PinLength);
            pin.pivot = new Vector2(.5f, 0);
        }

        public void Render(TimingState state)
        {
            if (!cursor) return;
            bool linear = Kind == TimingViewKind.Linear;
            bool circle = Kind == TimingViewKind.ConvergingCircle;
            bool radial = Kind == TimingViewKind.RadialPin;
            SetActive(linear, normal, great, extreme);
            cursor.gameObject.SetActive(linear);
            circleGuides.gameObject.SetActive(circle);
            targetRing.gameObject.SetActive(circle);
            movingRing.gameObject.SetActive(circle);
            radialTrack.gameObject.SetActive(radial);
            pin.gameObject.SetActive(radial);

            circleGuides.SetThresholds(state.GreatStart, state.ExtremeStart);
            radialTrack.SetThresholds(state.GreatStart, state.ExtremeStart);

            if (linear) RenderLinear(state);
            if (circle) movingRing.SetRingRadius(TimingPresentationMapping.Radius(config.StartRadius, config.TargetRadius, state.NormalizedTime));
            if (radial) pin.localRotation = Quaternion.Euler(0, 0, config.StartAngle - 90f - TimingPresentationMapping.Angle(state.NormalizedTime));
        }

        private void RenderLinear(TimingState state)
        {
            SetAnchors(normal.rectTransform, 0, state.GreatStart);
            SetAnchors(great.rectTransform, state.GreatStart, state.ExtremeStart);
            SetAnchors(extreme.rectTransform, state.ExtremeStart, 1);
            SetAnchors(cursor, state.NormalizedTime, Mathf.Min(1, state.NormalizedTime + .008f), .25f, .75f);
        }

        private TimingGuideGraphic Guide(string name, TimingGuideGraphic.GuideMode mode, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TimingGuideGraphic));
            go.transform.SetParent(transform, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = Vector2.one * (config.StartRadius + config.RingThickness * 2) * 2;
            var graphic = go.GetComponent<TimingGuideGraphic>();
            graphic.color = color;
            graphic.Configure(mode, config);
            return graphic;
        }

        private Image Block(string name, Color color, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void SetActive(bool active, params Component[] components)
        {
            foreach (Component component in components) component.gameObject.SetActive(active);
        }

        private static void SetAnchors(RectTransform rect, float minX, float maxX, float minY = .35f, float maxY = .65f)
        {
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
