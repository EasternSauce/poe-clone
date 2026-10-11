using UnityEngine;

namespace PoeClone.World
{
    /// <summary>Warm desert daylight, restored when travelling back to Act I.</summary>
    public sealed class DesertAtmosphere : MonoBehaviour
    {
        private AreaManager manager;
        private Light sun;
        private Color sunColor, background;
        private float intensity;
        private bool applied;
        private Camera view;

        private void Start()
        {
            manager = AreaManager.Instance;
            sun = RenderSettings.sun;
            if (sun == null)
                foreach (Light candidate in FindObjectsByType<Light>())
                    if (candidate.type == LightType.Directional) { sun = candidate; break; }
            view = Camera.main;
            if (manager != null) { manager.AreaChanged += Enter; Enter(manager.CurrentAreaIndex); }
        }

        private void Enter(int area)
        {
            bool desert = WorldBuilder.IsActTwo(area) && !WorldBuilder.Shape(area).IsCave;
            if (desert == applied) return;
            if (desert)
            {
                if (sun != null)
                {
                    sunColor = sun.color; intensity = sun.intensity;
                    sun.color = new Color(1, 0.89f, 0.72f); sun.intensity = Mathf.Max(1.1f, intensity);
                }
                if (view != null) { background = view.backgroundColor; view.backgroundColor = new Color(0.76f, 0.64f, 0.46f); }
                applied = true;
            }
            else Restore();
        }

        private void Restore()
        {
            if (!applied) return;
            if (sun != null) { sun.color = sunColor; sun.intensity = intensity; }
            if (view != null) view.backgroundColor = background;
            applied = false;
        }
        private void OnDestroy() { if (manager != null) manager.AreaChanged -= Enter; Restore(); }
    }
}
