using UnityEngine;

namespace AssetGarage.CombatDemo
{
    public static class TimingPresentationMapping
    {
        public static float Radius(float startRadius, float targetRadius, float normalizedTime)
            => Mathf.Lerp(startRadius, targetRadius, Mathf.Clamp01(normalizedTime));

        public static float Angle(float normalizedTime)
            => Mathf.Clamp01(normalizedTime) * 360f;

        public static void RadialFills(float greatStart, float extremeStart, out float normal, out float great, out float extreme)
        {
            float safeGreat = Mathf.Clamp01(greatStart);
            float safeExtreme = Mathf.Clamp(Mathf.Clamp01(extremeStart), safeGreat, 1f);
            normal = safeGreat;
            great = safeExtreme - safeGreat;
            extreme = 1f - safeExtreme;
        }
    }
}
