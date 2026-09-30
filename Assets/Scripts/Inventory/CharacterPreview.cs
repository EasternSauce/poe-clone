using UnityEngine;

namespace PoeClone.Inventory
{
    /// <summary>
    /// Renders a copy of the player's model, facing the viewer, into a texture for the inventory screen.
    /// The copy lives on a hidden stage far from the level and mirrors the worn equipment,
    /// so changes to gear show up on it immediately.
    /// </summary>
    public class CharacterPreview : MonoBehaviour
    {
        private const int Width = 512;
        private const int Height = 768;
        private static readonly Vector3 StagePosition = new Vector3(4000f, 0f, 4000f);

        private static readonly string[] PoseNames = { "LegL", "LegR", "Knee", "ArmL", "ArmR", "Elbow", "UpperBody" };

        private GameObject stage;
        private Camera previewCamera;
        private RenderTexture texture;

        public RenderTexture Texture
        {
            get { return texture; }
        }

        /// <summary>Copies the source model onto the stage. Returns false if there is nothing to copy.</summary>
        public bool Build(EquipmentVisuals source, EquipmentSet equipment)
        {
            if (source == null)
                return false;

            stage = new GameObject("CharacterPreviewStage");
            stage.transform.position = StagePosition;

            GameObject model = Instantiate(source.gameObject, stage.transform);
            model.name = "PreviewModel";
            model.transform.localPosition = Vector3.zero;
            model.transform.localScale = Vector3.one;
            // Face -z, toward the camera.
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            // The copy only needs to stand there: drop the walking animator and any other scripts.
            foreach (MonoBehaviour mb in model.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb is EquipmentVisuals || mb is BaseGear || mb is EquipmentVisualInstance)
                    continue;
                Destroy(mb);
            }

            // Undo whatever stride the original was caught in.
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                if (System.Array.IndexOf(PoseNames, t.name) >= 0)
                    t.localRotation = Quaternion.identity;
            }

            model.GetComponent<EquipmentVisuals>().Bind(equipment);

            GameObject camGo = new GameObject("PreviewCamera");
            camGo.transform.SetParent(stage.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.2f, -6.6f);
            camGo.transform.localRotation = Quaternion.Euler(2f, 0f, 0f);

            previewCamera = camGo.AddComponent<Camera>();
            previewCamera.fieldOfView = 24f;
            previewCamera.nearClipPlane = 0.3f;
            previewCamera.farClipPlane = 30f;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);

            texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            texture.antiAliasing = 4;
            texture.name = "CharacterPreviewTexture";
            texture.Create();

            previewCamera.targetTexture = texture;
            previewCamera.enabled = false;
            return true;
        }

        /// <summary>The camera only renders while the inventory is open.</summary>
        public void SetActive(bool active)
        {
            if (previewCamera != null)
                previewCamera.enabled = active;
        }

        private void OnDestroy()
        {
            if (stage != null)
                Destroy(stage);

            if (texture != null)
            {
                texture.Release();
                Destroy(texture);
            }
        }
    }
}
