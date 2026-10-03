using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.Inventory
{
    /// <summary>
    /// Shows worn equipment on the character. Lives on the character model. When gear is equipped
    /// it loads Resources/Equipment/{item id}; each child of that prefab is named after the socket
    /// it belongs on (Socket_Head, Socket_HandL, ...) and is attached there with its own offset.
    /// Starting clothing marked with <see cref="BaseGear"/> is hidden while its slot is filled.
    /// </summary>
    public class EquipmentVisuals : MonoBehaviour
    {
        private readonly Dictionary<string, Transform> sockets = new Dictionary<string, Transform>();
        private readonly Dictionary<EquipSlot, List<GameObject>> spawned = new Dictionary<EquipSlot, List<GameObject>>();
        private BaseGear[] baseGear = new BaseGear[0];
        private EquipmentSet equipment;

        private void Start()
        {
            if (equipment != null)
                return;

            PlayerInventory inventory = GetComponentInParent<PlayerInventory>();
            if (inventory != null)
                Bind(inventory.Equipment);
        }

        private void OnDestroy()
        {
            Unbind();
        }

        /// <summary>Starts mirroring the given equipment on this model.</summary>
        public void Bind(EquipmentSet set)
        {
            Unbind();
            equipment = set;

            // If this model was cloned, drop any gear visuals that came along with it.
            foreach (EquipmentVisualInstance old in GetComponentsInChildren<EquipmentVisualInstance>(true))
                Kill(old.gameObject);
            spawned.Clear();

            CollectSockets();
            baseGear = GetComponentsInChildren<BaseGear>(true);

            if (equipment == null)
            {
                UpdateBaseGear();
                return;
            }

            foreach (EquipSlot slot in SlotRules.AllSlots)
                Apply(slot, equipment.Get(slot));

            equipment.Changed += OnChanged;
        }

        public void Unbind()
        {
            if (equipment != null)
                equipment.Changed -= OnChanged;
            equipment = null;
        }

        /// <summary>Removes all gear visuals and shows the starting clothing again.</summary>
        public void ClearVisuals()
        {
            foreach (EquipmentVisualInstance old in GetComponentsInChildren<EquipmentVisualInstance>(true))
                Kill(old.gameObject);
            spawned.Clear();

            foreach (BaseGear g in GetComponentsInChildren<BaseGear>(true))
                g.gameObject.SetActive(true);
        }

        private void OnChanged(EquipSlot slot, ItemData item)
        {
            Apply(slot, item);
        }

        private void CollectSockets()
        {
            sockets.Clear();
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("Socket_"))
                    continue;
                if (t.GetComponentInParent<EquipmentVisualInstance>() != null)
                    continue;

                sockets[t.name] = t;
            }
        }

        private void Apply(EquipSlot slot, ItemData item)
        {
            List<GameObject> list;
            if (spawned.TryGetValue(slot, out list))
            {
                foreach (GameObject go in list)
                {
                    if (go != null)
                        Kill(go);
                }
                list.Clear();
            }
            else
            {
                list = new List<GameObject>();
                spawned[slot] = list;
            }

            if (item != null)
            {
                string model = ItemGenerator.ModelFor(item.ArtId, out Color modelTint);
                Color tint = item.ArtTint * modelTint;
                GameObject prefab = Resources.Load<GameObject>("Equipment/" + model);
                if (prefab == null)
                {
                    Debug.LogWarning("EquipmentVisuals: no equipment prefab at Resources/Equipment/" + model);
                }
                else
                {
                    foreach (Transform child in prefab.transform)
                    {
                        Transform socket;
                        if (!sockets.TryGetValue(ResolveSocket(child.name, slot), out socket))
                            continue;

                        GameObject instance = Instantiate(child.gameObject, socket);
                        instance.name = "Equip_" + child.name;
                        instance.transform.localPosition = child.localPosition;
                        instance.transform.localRotation = child.localRotation;
                        instance.transform.localScale = child.localScale;
                        instance.AddComponent<EquipmentVisualInstance>();
                        if (tint != Color.white)
                            Tint(instance, tint);
                        list.Add(instance);
                    }
                }
            }

            UpdateBaseGear();
        }

        // Rings are written once ("Socket_Ring"); the slot decides which hand they go on.
        // A higher tier of a shared look: its colours multiplied by the tier's tint (property
        // blocks, so the shared materials stay as they are).
        private static void Tint(GameObject instance, Color tint)
        {
            var block = new MaterialPropertyBlock();
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material m = r.sharedMaterial;
                if (m == null || !m.HasProperty("_BaseColor"))
                    continue;
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", m.GetColor("_BaseColor") * tint);
                r.SetPropertyBlock(block);
            }
        }

        private static string ResolveSocket(string name, EquipSlot slot)
        {
            if (name == "Socket_Ring")
                return slot == EquipSlot.Ring2 ? "Socket_RingL" : "Socket_RingR";
            return name;
        }

private void UpdateBaseGear()
        {
            foreach (BaseGear g in baseGear)
            {
                if (g != null)
                    g.gameObject.SetActive(!BaseGearRules.IsHidden(g.HiddenBy, g.VisibleIfItemHasCape, equipment));
            }
        }

        private static void Kill(GameObject go)
        {
            if (Application.isPlaying)
                Destroy(go);
            else
                DestroyImmediate(go);
        }
    }
}
