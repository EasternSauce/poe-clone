using UnityEngine;
namespace PoeClone.World
{
    public class ActBossDoor : MonoBehaviour
    {
        private void Update() { if (ActBossArena.DoorOpen) gameObject.SetActive(false); }
    }
}
