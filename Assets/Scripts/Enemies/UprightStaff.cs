using UnityEngine;

namespace PoeClone.Enemies
{
    /// <summary>
    /// Keeps something held in one hand upright (leaning a little forward) whatever the arm is
    /// doing: the Shepherd's crook and lantern. Runs after the walk and attack animators have
    /// posed the arm. (Staves are held in both hands instead, see <see cref="Visuals.HeldStaff"/>.)
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class UprightStaff : MonoBehaviour
    {
        private Transform body;

        // The direction it points from the hand, for a character facing body's forward.
        private static Vector3 Up(Transform body)
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
            // The shaft is the local +Y; its +Z goes at right angles to it, still facing forward.
            Vector3 up = Up(body);
            transform.rotation = Quaternion.LookRotation(Vector3.Cross(body.right, up), up);
        }
    }
}
