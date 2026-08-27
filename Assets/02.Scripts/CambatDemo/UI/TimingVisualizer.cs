using UnityEngine;
using UnityEngine.UI;

namespace AssetGarage.CombatDemo
{
    public sealed class TimingVisualizer : MonoBehaviour
    {
        private const int RingMarkerCount = 48;

        public TimingViewKind Kind { get; set; }

        private TimingPresentationConfig config;
        private RectTransform linearView, circleView, radialView;
        private RectTransform cursor, pin, targetRing, movingRing;
        private Image normal, great, extreme;
        private Image circleNormal, circleGreat, circleExtreme;
        private Image radialNormal, radialGreat, radialExtreme;
        private Sprite circleSprite;

        public void Initialize(TimingPresentationConfig presentation)
        {
            config = presentation;
            Kind = config.DefaultTimingView;
            circleSprite = Resources.Load<Sprite>("TimingGuideCircle");
            if (!circleSprite) Debug.LogError("CombatDemo timing guide circle Sprite is missing. Run Tools/CombatDemo/Generate Timing Guide Circle.");

            linearView = ViewRoot("LinearTimingView");
            circleView = ViewRoot("ConvergingCircleTimingView");
            radialView = ViewRoot("RadialPinTimingView");

            BuildLinearView();
            BuildCircleView();
            BuildRadialView();
        }

        public void Render(TimingState state)
        {
            if (!cursor) return;

            bool linear = Kind == TimingViewKind.Linear;
            bool circle = Kind == TimingViewKind.ConvergingCircle;
            bool radial = Kind == TimingViewKind.RadialPin;
            linearView.gameObject.SetActive(linear);
            circleView.gameObject.SetActive(circle);
            radialView.gameObject.SetActive(radial);

            float greatStart = Mathf.Clamp01(state.GreatStart);
            float extremeStart = Mathf.Clamp(Mathf.Clamp01(state.ExtremeStart), greatStart, 1f);
            RenderCircleGuides(greatStart, extremeStart);
            RenderRadialGuides(greatStart, extremeStart);

            if (linear) RenderLinear(state, greatStart, extremeStart);
            if (circle) SetRingRadius(movingRing, TimingPresentationMapping.Radius(config.StartRadius, config.TargetRadius, state.NormalizedTime));
            if (radial) pin.localRotation = Quaternion.Euler(0, 0, SegmentRotation(state.NormalizedTime));
        }

        private void BuildLinearView()
        {
            normal = Block(linearView, "Normal", config.NormalGradeColor, new Vector2(0, .35f), new Vector2(1, .65f));
            great = Block(linearView, "Great", config.GreatGradeColor, new Vector2(0, .35f), new Vector2(1, .65f));
            extreme = Block(linearView, "Extreme", config.ExtremeGradeColor, new Vector2(0, .35f), new Vector2(1, .65f));
            cursor = Block(linearView, "Cursor", Color.white, new Vector2(0, .25f), new Vector2(.008f, .75f)).rectTransform;
        }

        private void BuildCircleView()
        {
            RectTransform guideRoot = CenteredRoot(circleView, "GradeGuideRoot");
            circleNormal = Circle(guideRoot, "NormalGuideCircle", GuideColor(config.NormalGradeColor));
            circleGreat = Circle(guideRoot, "GreatGuideCircle", GuideColor(config.GreatGradeColor));
            circleExtreme = Circle(guideRoot, "ExtremeGuideCircle", GuideColor(config.ExtremeGradeColor));

            targetRing = MarkerRing(circleView, "TargetRing", new Color(1, 1, 1, .65f));
            SetRingRadius(targetRing, config.TargetRadius);
            movingRing = MarkerRing(circleView, "MovingTimingRing", Color.cyan);
        }

        private void BuildRadialView()
        {
            RectTransform trackRoot = CenteredRoot(radialView, "GradeTrackRoot");
            radialNormal = RadialSegment(trackRoot, "NormalSegment", GuideColor(config.NormalGradeColor));
            radialGreat = RadialSegment(trackRoot, "GreatSegment", GuideColor(config.GreatGradeColor));
            radialExtreme = RadialSegment(trackRoot, "ExtremeSegment", GuideColor(config.ExtremeGradeColor));

            Image cover = Circle(trackRoot, "InnerCover", new Color(0, 0, 0, .82f));
            SetCircleRadius(cover, Mathf.Max(0, config.StartRadius - config.RingThickness * 2f));
            BoundaryTick(trackRoot, "GreatBoundary");
            BoundaryTick(trackRoot, "ExtremeBoundary");
            BoundaryTick(trackRoot, "EndBoundary");

            pin = Block(radialView, "RadialPin", Color.white, new Vector2(.5f, .5f), new Vector2(.5f, .5f)).rectTransform;
            pin.sizeDelta = new Vector2(config.PinWidth, config.PinLength);
            pin.pivot = new Vector2(.5f, 0);
        }

        private void RenderLinear(TimingState state, float greatStart, float extremeStart)
        {
            SetAnchors(normal.rectTransform, 0, greatStart);
            SetAnchors(great.rectTransform, greatStart, extremeStart);
            SetAnchors(extreme.rectTransform, extremeStart, 1);
            SetAnchors(cursor, state.NormalizedTime, Mathf.Min(1, state.NormalizedTime + .008f), .25f, .75f);
        }

        private void RenderCircleGuides(float greatStart, float extremeStart)
        {
            SetCircleRadius(circleNormal, config.StartRadius);
            SetCircleRadius(circleGreat, TimingPresentationMapping.Radius(config.StartRadius, config.TargetRadius, greatStart));
            SetCircleRadius(circleExtreme, TimingPresentationMapping.Radius(config.StartRadius, config.TargetRadius, extremeStart));
        }

        private void RenderRadialGuides(float greatStart, float extremeStart)
        {
            TimingPresentationMapping.RadialFills(greatStart, extremeStart, out float normalFill, out float greatFill, out float extremeFill);
            SetSegment(radialNormal, 0, normalFill);
            SetSegment(radialGreat, greatStart, greatFill);
            SetSegment(radialExtreme, extremeStart, extremeFill);

            SetBoundary("GreatBoundary", greatStart);
            SetBoundary("ExtremeBoundary", extremeStart);
            SetBoundary("EndBoundary", 1f);
        }

        private void SetSegment(Image image, float start, float amount)
        {
            image.fillAmount = amount;
            image.rectTransform.localRotation = Quaternion.Euler(0, 0, SegmentRotation(start));
        }

        private void SetBoundary(string name, float normalized)
        {
            Transform tick = radialView.Find($"GradeTrackRoot/{name}");
            if (tick) tick.localRotation = Quaternion.Euler(0, 0, SegmentRotation(normalized));
        }

        private float SegmentRotation(float normalized)
            => config.StartAngle - 90f - TimingPresentationMapping.Angle(normalized);

        private RectTransform ViewRoot(string name)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static RectTransform CenteredRoot(Transform parent, string name)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            return rect;
        }

        private Image Circle(Transform parent, string name, Color color)
        {
            Image image = Block(parent, name, color, new Vector2(.5f, .5f), new Vector2(.5f, .5f));
            image.sprite = circleSprite;
            image.preserveAspect = true;
            return image;
        }

        private Image RadialSegment(Transform parent, string name, Color color)
        {
            Image image = Circle(parent, name, color);
            SetCircleRadius(image, config.StartRadius);
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Top;
            image.fillClockwise = true;
            return image;
        }

        private RectTransform MarkerRing(Transform parent, string name, Color color)
        {
            RectTransform root = CenteredRoot(parent, name);
            for (int i = 0; i < RingMarkerCount; i++)
            {
                Image marker = Block(root, $"Marker{i:00}", color, new Vector2(.5f, .5f), new Vector2(.5f, .5f));
                marker.rectTransform.sizeDelta = new Vector2(Mathf.Max(2, config.RingThickness * .6f), Mathf.Max(3, config.RingThickness));
            }
            return root;
        }

        private static void SetRingRadius(RectTransform ring, float radius)
        {
            for (int i = 0; i < ring.childCount; i++)
            {
                float angle = i / (float)ring.childCount * Mathf.PI * 2f;
                RectTransform marker = (RectTransform)ring.GetChild(i);
                marker.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                marker.localRotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg - 90f);
            }
        }

        private static void SetCircleRadius(Image image, float radius)
            => image.rectTransform.sizeDelta = Vector2.one * Mathf.Max(0, radius) * 2f;

        private void BoundaryTick(Transform parent, string name)
        {
            Image tick = Block(parent, name, new Color(1, 1, 1, .85f), new Vector2(.5f, .5f), new Vector2(.5f, .5f));
            tick.rectTransform.sizeDelta = new Vector2(2f, config.RingThickness * 3f);
            tick.rectTransform.pivot = new Vector2(.5f, 0);
            tick.rectTransform.anchoredPosition = Vector2.up * (config.StartRadius - config.RingThickness * 2f);
        }

        private Color GuideColor(Color value)
        {
            value.a *= Mathf.Clamp01(config.GuideOpacity);
            return value;
        }

        private static Image Block(Transform parent, string name, Color color, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void SetAnchors(RectTransform rect, float minX, float maxX, float minY = .35f, float maxY = .65f)
        {
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
