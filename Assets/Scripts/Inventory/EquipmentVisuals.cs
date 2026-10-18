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
        [SerializeField] private Material originalLeftSleeve, originalRightSleeve;
        [SerializeField] private Mesh originalLegMesh;
        private Mesh shortCoveredLeg, longCoveredLeg;
        private readonly Dictionary<SkinnedMeshRenderer, Material[]> sleeves = new Dictionary<SkinnedMeshRenderer, Material[]>();

        /// <summary>The worn great weapon (greatsword, greataxe, maul), held in both hands; null otherwise. Its +Y runs from the grip to the head.</summary>
        public Transform TwoHandedWeapon { get; private set; }

        /// <summary>Which of this model's hands hold something.</summary>
        public void HeldHands(out bool left, out bool right) => SlotRules.HeldHands(equipment, out left, out right);

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
            ReleaseMesh(shortCoveredLeg);
            ReleaseMesh(longCoveredLeg);
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
            CollectClothing();
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
            TwoHandedWeapon = null;

            foreach (BaseGear g in GetComponentsInChildren<BaseGear>(true))
                g.gameObject.SetActive(true);
            foreach (var anatomy in GetComponentsInChildren<ProceduralAnatomy>(true))
                anatomy.SetEquipment(null);
            UpdateClothing(null);
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

            if (slot == EquipSlot.MainHand)
                TwoHandedWeapon = null;

            if (item != null)
            {
                string model = ItemGenerator.ModelFor(item, out Color modelTint);
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
                        // Roll melee weapons around their grip axis. This turns the head down
                        // without moving the grip; bows and casting staves keep their existing roll.
                        bool meleeWeapon = item.Type == ItemType.Weapon &&
                            (item.WeaponType == WeaponType.Sword || item.WeaponType == WeaponType.Axe ||
                             item.WeaponType == WeaponType.Mace || item.WeaponType == WeaponType.Dagger ||
                             item.WeaponType == WeaponType.Greatsword || item.WeaponType == WeaponType.Greataxe ||
                             item.WeaponType == WeaponType.Maul || item.WeaponType == WeaponType.Sceptre);
                        float gripTwist = meleeWeapon ? 270f : 180f;
                        bool heldInHand = slot == EquipSlot.MainHand && child.name == "Socket_MainHand";
                        // One-handed weapons are built pointing straight ahead out of the fist, end-on
                        // to the portrait camera. Turn them out from the body about the forearm, so
                        // the blade shows as a diagonal (and reads better from the game camera too).
                        bool oneHanded = item.Type == ItemType.Weapon && !SlotRules.IsTwoHanded(item.WeaponType);
                        Quaternion splay = heldInHand && oneHanded ? Quaternion.Euler(0f, OneHandedSplay, 0f) : Quaternion.identity;
                        instance.transform.localRotation = heldInHand
                            ? splay * child.localRotation * Quaternion.Euler(0f, gripTwist, 0f)
                            : child.localRotation;
                        instance.transform.localScale = child.localScale;
                        // Legacy weapons placed the socket near the pommel. With actual fingers,
                        // seat the one-handed melee grip in the palm while keeping the rig's sockets.
                        if (slot == EquipSlot.MainHand && meleeWeapon && !SlotRules.IsTwoHandedMelee(item.WeaponType) && GetComponent<ProceduralAnatomy>() != null)
                            SeatGrip(instance.transform, socket);
                        if (child.name == "Socket_OffHand" && instance.transform.Find("Grip") != null && GetComponent<ProceduralAnatomy>() != null)
                            SeatGrip(instance.transform, socket, true);
                        instance.AddComponent<EquipmentVisualInstance>();
                        WornTexture.Anchor(instance);
                        if (tint != Color.white)
                            Tint(instance, tint);
                        if (slot == EquipSlot.Boots && socket.GetComponentInParent<PoeClone.Inventory.AnatomicalAnkle>() is PoeClone.Inventory.AnatomicalAnkle ankle)
                            ankle.FitFootwear(instance);
                        if (slot == EquipSlot.BodyArmour && child.name == "Socket_Chest" && GetComponent<ProceduralAnatomy>() != null)
                            instance.AddComponent<FittedGarment>().Bind(transform, socket);
                        if (slot == EquipSlot.MainHand && child.name == "Socket_MainHand" && SlotRules.IsTwoHandedMelee(item.WeaponType))
                            TwoHandedWeapon = instance.transform;
                        list.Add(instance);
                    }
                }
            }

            UpdateBaseGear();
        }

        // Degrees a one-handed main-hand weapon turns outward from straight ahead.
        private const float OneHandedSplay = 28f;

        private static void SeatGrip(Transform weapon, Transform socket, bool left = false)
        {
            // The fingers close around the palm; the legacy socket was below and ahead of it.
            Vector3 palm = socket.parent.TransformPoint(HandPoint(new Vector3(left ? .14f : -.14f, -.32f, 0f)));
            Transform grip = weapon.Find("Grip") ?? weapon.Find("Wrap");
            if (grip != null)
            {
                weapon.position += palm - grip.position;
                return;
            }
            Transform shaft = weapon.Find("Handle") ?? weapon.Find("Haft") ?? weapon.Find("Shaft");
            if (shaft == null) return;
            // Long unwrapped handles are held near the butt, rather than at their centre.
            Vector3 point = shaft.localPosition;
            point.y = shaft.localPosition.y - shaft.localScale.y * .5f + .10f;
            weapon.position += palm - weapon.TransformPoint(point);
        }

        // Matches the baked hand's scale and 1.2x growth about its wrist, in elbow space.
        private static Vector3 HandPoint(Vector3 point) => new Vector3(
            point.x * .135f * 1.2f,
            -.39f + (.3125f + (point.y - .3125f) * 1.2f) * .16f,
            point.z * .16f * 1.2f);

        private void FitRings()
        {
            if (GetComponent<ProceduralAnatomy>() == null) return;
            HeldHands(out bool left, out bool right);
            foreach (EquipSlot slot in new[] { EquipSlot.Ring1, EquipSlot.Ring2 })
            {
                if (!spawned.TryGetValue(slot, out var rings)) continue;
                bool isLeft = slot == EquipSlot.Ring2;
                float mirror = isLeft ? -1f : 1f;
                bool grip = isLeft ? left : right;
                // Centre of the ring finger's proximal phalanx, following its rest or grip arc.
                const float along = .065f;
                float angle = grip ? along / .51f * 2.65f : 22f * Mathf.Deg2Rad;
                Vector3 finger = grip
                    ? new Vector3(-mirror * .51f * (1f - Mathf.Cos(angle)) / 2.65f,
                        -.18f - .51f * Mathf.Sin(angle) / 2.65f, -.0675f)
                    : new Vector3(-mirror * Mathf.Sin(angle) * along,
                        -.18f - Mathf.Cos(angle) * along, -.0675f);
                foreach (GameObject ring in rings)
                {
                    if (ring == null) continue;
                    Transform socket = ring.transform.parent;
                    ring.transform.position = socket.parent.TransformPoint(HandPoint(finger));
                    ring.transform.rotation = socket.parent.rotation *
                        Quaternion.Euler(0f, 0f, -mirror * angle * Mathf.Rad2Deg) *
                        Quaternion.Euler(0f, mirror * 90f, 0f);
                    ring.transform.localScale = Vector3.one;
                    Transform band = ring.transform.Find("Band"), gem = ring.transform.Find("Gem");
                    if (band != null)
                    {
                        band.localPosition = Vector3.zero;
                        band.localScale = new Vector3(.025f, .009f, .022f);
                    }
                    if (gem != null)
                    {
                        gem.localPosition = new Vector3(0f, 0f, .012f);
                        gem.localScale = new Vector3(.012f, .010f, .008f);
                    }
                }
            }
        }

        /// <summary>The equipped hammer's head in its current animated pose.</summary>
        public bool TryGetMainHandHeadPosition(out Vector3 position)
        {
            if (spawned.TryGetValue(EquipSlot.MainHand, out List<GameObject> gear))
            {
                foreach (GameObject instance in gear)
                {
                    Transform head = instance != null ? instance.transform.Find("Head") : null;
                    if (head == null) continue;
                    position = head.position;
                    return true;
                }
            }
            position = Vector3.zero;
            return false;
        }

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
            // Rings share a prefab; the slot decides which hand wears it.
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
            foreach (var anatomy in GetComponentsInChildren<ProceduralAnatomy>(true))
                anatomy.SetEquipment(equipment);
            UpdateClothing(equipment);
            FitRings();
        }

        private void CollectClothing()
        {
            // These are replacement garments, not an extra shell over the starting tunic.
            Transform upper = transform.Find("UpperBody/UpperOffset");
            if (upper == null || GetComponent<ProceduralAnatomy>() == null) return;
            foreach (string name in new[] { "Torso", "TunicSkirt", "Undercollar", "Belt", "BeltBuckle", "Pouch" })
            {
                Transform part = upper.Find(name);
                if (part == null) continue;
                var marker = part.GetComponent<BaseGear>() ?? part.gameObject.AddComponent<BaseGear>();
                marker.HiddenBy = EquipSlot.BodyArmour;
            }
            foreach (string side in new[] { "L", "R" })
            {
                Transform arm = upper.Find("Arm" + side + "/ContinuousArm");
                if (arm != null && arm.TryGetComponent<SkinnedMeshRenderer>(out var skin) && !sleeves.ContainsKey(skin))
                {
                    Material[] materials = skin.sharedMaterials;
                    Material original = side == "L" ? originalLeftSleeve : originalRightSleeve;
                    if (original == null && materials.Length > 0) original = materials[0];
                    if (side == "L") originalLeftSleeve = original; else originalRightSleeve = original;
                    if (materials.Length > 0) materials[0] = original;
                    sleeves.Add(skin, materials);
                }
            }
            Transform leg = transform.Find("LegL/ContinuousLeg");
            if (originalLegMesh == null && leg != null)
                originalLegMesh = leg.GetComponent<SkinnedMeshRenderer>().sharedMesh;
        }

        private void UpdateClothing(EquipmentSet set)
        {
            ItemData armour = set?.Get(EquipSlot.BodyArmour);
            bool replaced = armour != null && spawned.TryGetValue(EquipSlot.BodyArmour, out var pieces) && pieces.Count > 0;
            Material sleeve = null;
            if (replaced)
            {
                GameObject prefab = Resources.Load<GameObject>("Equipment/" + ItemGenerator.ModelFor(armour, out _));
                Transform body = prefab != null ? prefab.transform.Find("Socket_Chest/Body") : null;
                if (body != null) sleeve = body.GetComponent<Renderer>().sharedMaterial;
            }
            foreach (var arm in sleeves)
            {
                if (arm.Key == null) continue;
                Material[] materials = (Material[])arm.Value.Clone();
                if (sleeve != null && materials.Length > 0) materials[0] = sleeve;
                arm.Key.sharedMaterials = materials;
                var block = new MaterialPropertyBlock();
                if (sleeve != null && sleeve.HasProperty("_BaseColor"))
                {
                    ItemGenerator.ModelFor(armour, out Color tint);
                    block.SetColor("_BaseColor", sleeve.GetColor("_BaseColor") * armour.ArtTint * tint);
                }
                arm.Key.SetPropertyBlock(sleeve != null ? block : null, 0);
            }
            if (originalLegMesh != null)
            {
                Mesh legs = originalLegMesh;
                if (replaced)
                {
                    bool robe = ItemGenerator.ModelFor(armour, out _) == "silk_robe";
                    if (robe && longCoveredLeg == null) longCoveredLeg = CoveredLeg(-.42f);
                    if (!robe && shortCoveredLeg == null) shortCoveredLeg = CoveredLeg(-.10f);
                    legs = robe ? longCoveredLeg : shortCoveredLeg;
                }
                foreach (string side in new[] { "L", "R" })
                {
                    Transform leg = transform.Find("Leg" + side + "/ContinuousLeg");
                    if (leg == null) continue;
                    var skin = leg.GetComponent<SkinnedMeshRenderer>();
                    if (skin.sharedMesh == legs) continue;
                    bool shown = skin.enabled;
                    skin.enabled = false;
                    skin.sharedMesh = legs;
                    skin.enabled = shown;
                }
            }
        }

        private Mesh CoveredLeg(float top)
        {
            Mesh mesh = Instantiate(originalLegMesh);
            mesh.name = "ArmourCoveredLeg";
            Vector3[] vertices = mesh.vertices;
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                int[] triangles = mesh.GetTriangles(submesh);
                var visible = new List<int>(triangles.Length);
                for (int i = 0; i < triangles.Length; i += 3)
                    if (vertices[triangles[i]].y <= top && vertices[triangles[i + 1]].y <= top && vertices[triangles[i + 2]].y <= top)
                    {
                        visible.Add(triangles[i]);
                        visible.Add(triangles[i + 1]);
                        visible.Add(triangles[i + 2]);
                    }
                mesh.SetTriangles(visible, submesh, false);
            }
            return mesh;
        }

        private static void ReleaseMesh(Mesh mesh)
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
        }

        private static void Kill(GameObject go)
        {
            // Destroy is deferred in Play mode. Hide old equipment immediately, including clones
            // copied into the portrait, so it cannot overlap the replacement for a frame.
            go.SetActive(false);
            if (Application.isPlaying)
                Destroy(go);
            else
                DestroyImmediate(go);
        }
    }
}
