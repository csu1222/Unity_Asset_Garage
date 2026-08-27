using UnityEngine;
using UnityEngine.UI;

namespace AssetGarage.CombatDemo
{
    public sealed class TimingGuideGraphic : MaskableGraphic
    {
        public enum GuideMode { ConcentricBands, RadialTrack, Ring }

        private const int Segments = 128;
        private GuideMode mode;
        private float startRadius, targetRadius, greatStart, extremeStart, thickness, startAngle;
        private Color normalColor, greatColor, extremeColor;

        public void Configure(GuideMode guideMode, TimingPresentationConfig config)
        {
            mode = guideMode;
            startRadius = config.StartRadius;
            targetRadius = config.TargetRadius;
            thickness = config.RingThickness;
            startAngle = config.StartAngle;
            normalColor = WithOpacity(config.NormalGradeColor, config.GuideOpacity);
            greatColor = WithOpacity(config.GreatGradeColor, config.GuideOpacity);
            extremeColor = WithOpacity(config.ExtremeGradeColor, config.GuideOpacity);
            raycastTarget = false;
            SetVerticesDirty();
        }

        public void SetThresholds(float great, float extreme)
        {
            greatStart = Mathf.Clamp01(great);
            extremeStart = Mathf.Clamp(Mathf.Clamp01(extreme), greatStart, 1f);
            SetVerticesDirty();
        }

        public void SetRingRadius(float radius)
        {
            startRadius = Mathf.Max(0, radius);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (mode == GuideMode.ConcentricBands)
            {
                float greatRadius = TimingPresentationMapping.Radius(startRadius, targetRadius, greatStart);
                float extremeRadius = TimingPresentationMapping.Radius(startRadius, targetRadius, extremeStart);
                AddArc(vh, 0, 1, greatRadius, startRadius, normalColor, startAngle);
                AddArc(vh, 0, 1, extremeRadius, greatRadius, greatColor, startAngle);
                AddArc(vh, 0, 1, targetRadius, extremeRadius, extremeColor, startAngle);
                AddBoundary(vh, greatRadius);
                AddBoundary(vh, extremeRadius);
                AddBoundary(vh, targetRadius);
                return;
            }

            if (mode == GuideMode.RadialTrack)
            {
                float inner = Mathf.Max(0, startRadius - thickness);
                AddArc(vh, 0, greatStart, inner, startRadius, normalColor, startAngle);
                AddArc(vh, greatStart, extremeStart, inner, startRadius, greatColor, startAngle);
                AddArc(vh, extremeStart, 1, inner, startRadius, extremeColor, startAngle);
                AddRadialBoundary(vh, greatStart, inner - thickness * .35f, startRadius + thickness * .35f);
                AddRadialBoundary(vh, extremeStart, inner - thickness * .35f, startRadius + thickness * .35f);
                AddRadialBoundary(vh, 1, inner - thickness * .35f, startRadius + thickness * .35f);
                return;
            }

            AddArc(vh, 0, 1, Mathf.Max(0, startRadius - thickness), startRadius, color, startAngle);
        }

        private static Color WithOpacity(Color value, float opacity)
        {
            value.a *= Mathf.Clamp01(opacity);
            return value;
        }

        private void AddBoundary(VertexHelper vh, float radius)
        {
            AddArc(vh, 0, 1, Mathf.Max(0, radius - 1f), radius + 1f, new Color(1, 1, 1, .55f), startAngle);
        }

        private void AddRadialBoundary(VertexHelper vh, float normalized, float inner, float outer)
        {
            float halfTick = Mathf.Max(.001f, 1f / Segments);
            AddArc(vh, normalized - halfTick, normalized + halfTick, Mathf.Max(0, inner), outer, new Color(1, 1, 1, .8f), startAngle);
        }

        private static void AddArc(VertexHelper vh, float start, float end, float innerRadius, float outerRadius, Color color, float startAngle)
        {
            if (outerRadius <= innerRadius || end <= start) return;
            int count = Mathf.Max(1, Mathf.CeilToInt((end - start) * Segments));
            int baseIndex = vh.currentVertCount;
            for (int i = 0; i <= count; i++)
            {
                float t = Mathf.Lerp(start, end, i / (float)count);
                float angle = (startAngle - t * 360f) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vh.AddVert(direction * innerRadius, color, Vector2.zero);
                vh.AddVert(direction * outerRadius, color, Vector2.zero);
            }
            for (int i = 0; i < count; i++)
            {
                int index = baseIndex + i * 2;
                vh.AddTriangle(index, index + 1, index + 3);
                vh.AddTriangle(index, index + 3, index + 2);
            }
        }
    }

    public static class TimingPresentationMapping
    {
        public static float Radius(float startRadius, float targetRadius, float normalizedTime)
            => Mathf.Lerp(startRadius, targetRadius, Mathf.Clamp01(normalizedTime));

        public static float Angle(float normalizedTime)
            => Mathf.Clamp01(normalizedTime) * 360f;
    }
}
