using System.Collections;
using UnityEngine;
using PoeClone.Quests;
using PoeClone.Skills;

namespace PoeClone.World
{
    /// <summary>A permanent progression unlock, independent of the Shepherd's repeatable fight.</summary>
    public sealed class ActTwoPassage : MonoBehaviour
    {
        public const string UnlockStage = "act2.shepherd_passage";
        public static bool Unlocked => QuestLog.Instance != null && QuestLog.Instance.PropUsed(UnlockStage);
        private GameObject seal;
        private AreaGate gate;
        private bool opening;
        public bool BlocksTeleport => seal != null && seal.activeInHierarchy;

        public void Configure(GameObject wall, AreaGate passage)
        {
            seal = wall;
            gate = passage;
            gate.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (opening || !Unlocked) return;
            opening = true;
            bool inArena = AreaManager.Instance != null && AreaManager.Instance.CurrentAreaIndex == WorldBuilder.ActArena;
            if (inArena) StartCoroutine(Open());
            else Reveal();
        }

        private IEnumerator Open()
        {
            yield return new WaitForSeconds(2f);
            SkillEffects.Shockwave(seal.transform.position, 4f, new Color(0.8f, 0.64f, 0.42f), 0.7f);
            CameraSystem.CameraFollow.Shake(0.2f, 0.5f);
            Vector3 start = seal.transform.position;
            for (float t = 0; t < 0.8f; t += Time.deltaTime)
            {
                seal.transform.position = start + Vector3.down * (t * t * 12f);
                yield return null;
            }
            Reveal();
        }

        private void Reveal()
        {
            seal.SetActive(false);
            gate.gameObject.SetActive(true);
            enabled = false;
        }
    }
}
