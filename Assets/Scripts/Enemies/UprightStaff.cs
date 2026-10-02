using UnityEngine;

namespace PoeClone.Enemies
{
    /// <summary>
    /// Keeps a caster's staff held upright (leaning a little forward), the way a mage carries one,
    /// whatever the arm is doing. The attack swing swings the arm forwards and down, which used to
    /// tip the staff over so the orb - and the bolt fired from it - ended up near the ground.
    /// Runs after the walk and attack animators have posed the arm.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class UprightStaff : MonoBehaviour
    {
        private Transform body;

        /// <summary>The direction the staff points from the hand to the orb, for a character facing <paramref name="body"/>'s forward.</summary>
        public static Vector3 Up(Transform body)
        {
            return (Vector3.up * 0.9f + body.forward * 0.45f).normalized;
        }

        public void Bind(Transform characterRoot)
        {
            body = characterRoot;
        }

        private void LateUpdate()
        {
            if (body == null)
                return;
            // The shaft is the staff's local +Y; its +Z goes at right angles to it, still facing forward.
            Vector3 up = Up(body);
            transform.rotation = Quaternion.LookRotation(Vector3.Cross(body.right, up), up);
        }
    }
}
