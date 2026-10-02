using UnityEngine;
using UnityEngine.InputSystem;
using PoeClone.Inventory;
using PoeClone.UI;
using PoeClone.World;

namespace PoeClone.Player
{
    /// <summary>
    /// T (or the TOWN button on touch): a short channel, then home to Haven's waystone - to sell,
    /// buy potions and hand quests in, and take the waystone back out again. Moving, dashing or
    /// getting hit cancels it. Self-added by <see cref="PlayerController"/>.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class TownPortal : MonoBehaviour
    {
        public const float ChannelSeconds = 1.5f;

        private static readonly Color PortalColor = new Color(0.45f, 0.75f, 1f);

        private PlayerController controller;
        private PlayerStats stats;
        private float channelStarted = -1f;
        private float healthAtStart;

        /// <summary>Requested from the touch button (read and cleared every frame).</summary>
        public static bool Pressed;

        public bool IsChanneling => channelStarted >= 0f;
        public float Progress => IsChanneling ? Mathf.Clamp01((Time.time - channelStarted) / ChannelSeconds) : 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Pressed = false;
        }

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            stats = GetComponent<PlayerStats>();
        }

        private void OnDisable()
        {
            channelStarted = -1f;
        }

        private void Update()
        {
            bool pressed = Pressed;
            Pressed = false;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tKey.wasPressedThisFrame && !UiKit.IsTypingInTextField())
                pressed = true;

            if (IsChanneling)
            {
                if (Interrupted())
                {
                    channelStarted = -1f;
                    CombatText.Show(transform.position + Vector3.up * 2.2f, "Portal interrupted", new Color(0.8f, 0.8f, 0.8f), 0.8f);
                    return;
                }

                if (Time.time - channelStarted >= ChannelSeconds)
                {
                    channelStarted = -1f;
                    GoHome();
                }
                return;
            }

            if (pressed)
                TryStart();
        }

        private void TryStart()
        {
            AreaManager areas = AreaManager.Instance;
            if (areas == null || areas.IsSwitching || stats.IsDead)
                return;

            if (areas.CurrentAreaIndex == WorldBuilder.Haven)
            {
                CombatText.Show(transform.position + Vector3.up * 2.2f, "Already in Haven", new Color(0.8f, 0.8f, 0.8f), 0.8f);
                return;
            }

            controller.CancelWalk();
            channelStarted = Time.time;
            healthAtStart = stats.CurrentHealth;
            Skills.SkillEffects.Rise(transform, PortalColor, ChannelSeconds);
            CombatText.Show(transform.position + Vector3.up * 2.2f, "Town portal...", PortalColor, 0.8f);
        }

        private bool Interrupted()
        {
            return stats.IsDead || stats.CurrentHealth < healthAtStart - 0.01f ||
                   controller.InputDirection().sqrMagnitude > 0.01f || controller.IsDashing;
        }

        private void GoHome()
        {
            AreaManager areas = AreaManager.Instance;
            if (areas == null || areas.IsSwitching)
                return;
            Waystone stone = Waystone.In(WorldBuilder.Haven);
            areas.EnterArea(WorldBuilder.Haven, stone != null ? stone.Arrival : null);
        }
    }
}
