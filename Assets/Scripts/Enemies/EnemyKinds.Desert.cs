using UnityEngine;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    public static partial class EnemyKinds
    {
        private static void AddDesertWeapon(Transform model, EnemyKind kind)
        {
            Transform hand = FindChild(model, "Socket_MainHand") ?? FindChild(model, "Socket_HandR");
            if (hand == null) return;
            var weapon = new GameObject(kind.Javelin ? "Javelin" : kind.Skill == EnemySkill.Tollkeeper ? "HookedPolearm" : "CurvedBlade");
            weapon.transform.SetParent(hand, false);
            if (kind.Javelin || kind.Skill == EnemySkill.Tollkeeper)
            {
                RuntimePrimitives.BuildArrow(weapon.transform, new Color(0.65f, 0.52f, 0.33f));
                weapon.transform.localRotation = Quaternion.Euler(90, 0, 0);
                weapon.transform.localScale = new Vector3(1.8f, 1.8f, kind.Javelin ? 2.6f : 3.7f);
                if (!kind.Javelin)
                {
                    GameObject hook = RuntimePrimitives.Create(PrimitiveType.Cube, weapon.transform, new Color(0.65f, 0.65f, 0.59f));
                    hook.transform.localPosition = new Vector3(0.12f, 0, 0.31f);
                    hook.transform.localScale = new Vector3(0.3f, 0.045f, 0.12f);
                    hook.transform.localRotation = Quaternion.Euler(0, 25, 0);
                }
            }
            else
            {
                GameObject grip = RuntimePrimitives.Create(PrimitiveType.Cylinder, weapon.transform, new Color(0.25f, 0.16f, 0.09f));
                grip.transform.localScale = new Vector3(0.09f, 0.14f, 0.09f);
                for (int i = 0; i < 6; i++)
                {
                    GameObject blade = RuntimePrimitives.Create(PrimitiveType.Cube, weapon.transform, new Color(0.72f, 0.69f, 0.57f));
                    blade.transform.localPosition = new Vector3(i * i * 0.009f, -0.2f - i * 0.14f, 0);
                    blade.transform.localScale = new Vector3(0.13f - i * 0.012f, 0.18f, 0.035f);
                    blade.transform.localRotation = Quaternion.Euler(0, 0, i * 5);
                }
            }
        }
    }
}
