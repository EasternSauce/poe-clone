using System.Collections.Generic;
using UnityEngine;
using PoeClone.Enemies;

namespace PoeClone.World
{
    /// <summary>
    /// Ambient-life behaviour. A bird wanders the sky heading in a direction that
    /// drifts and occasionally changes outright, sometimes loops into a circle for
    /// a while, and periodically lands on a prop or a patch of ground to idle for
    /// a bit before taking off again. If a player, townsperson or enemy gets too close
    /// while it's landed, it flees back into the air. Purely cosmetic, no gameplay
    /// interaction. Uses scaled Time.deltaTime so it freezes correctly during
    /// area-transition loading screens, same as everything else.
    /// </summary>
    public class BirdFlight : MonoBehaviour
    {
        [Header("Wander")]
        public float flightSpeed = 5f;
        public float turnSpeed = 1.6f;
        public float cruiseAltitudeMin = 10f;
        public float cruiseAltitudeMax = 16f; // comfortably above the tallest trees (~6-7 units)
        public float altitudeCorrectSpeed = 1.5f;
        public float wanderExtent = 42f; // stays within +/- this of worldCenter on X/Z
        public Vector3 worldCenter = Vector3.zero;
        public Vector2 headingChangeInterval = new Vector2(2.5f, 5f);

        [Header("Circling (an occasional break from straight wandering)")]
        [Range(0f, 1f)] public float circleChance = 0.3f;
        public Vector2 circleDuration = new Vector2(3f, 6f);
        public float circleRadius = 5f;
        public float circleAngularSpeed = 90f; // degrees/sec

        [Header("Landing")]
        [Range(0f, 1f)] public float landChance = 0.32f;
        public float landingSearchRadius = 30f; // only consider spots this close so the flight is quick and visible
        public Transform[] landingSpots; // shared pool; may include ground-level empties
        public Vector2 perchTime = new Vector2(4f, 9f);
        public float groundWalkRadius = 1.3f;
        public Vector2 groundWalkInterval = new Vector2(1.5f, 3f);
        public float groundWalkSpeed = 0.5f;
        public float fleeRadius = 6f;
        public Transform player;

        [Header("Ground spawn (set by the builder for birds that start already landed)")]
        public Transform groundSpawnPoint; // if set, the bird starts perched here and never auto-departs

        [Header("Visuals")]
        public float wingFlapSpeed = 16f;
        public float wingFlapAngle = 45f;
        public float wingFoldSwing = 72f; // degrees the wing swings back (around pivot Y) when folded
        public float wingFoldTuck = 18f;  // degrees the wing tilts up against the body when folded
        public float wingFoldBlendSpeed = 6f; // how fast wings ease between spread and folded
        public Transform leftWing;
        public Transform rightWing;

        private float wingFold; // 0 = spread for flight, 1 = folded against body

        private enum State { Wander, Circle, FlyToLanding, Perched, TakeOff }
        private State state = State.Wander;

        private Vector3 heading; // normalized, y == 0
        private float nextDecisionTime;
        private float circleTimer;
        private Vector3 circlePivot;
        private float circleAngle;
        private Transform landingTarget;
        private float perchTimer;
        private bool isGroundLanding;
        private Vector3 groundWalkTarget;
        private float nextGroundWalkTime;
        private float cruiseAltitude;

        private static List<Transform> threatsCache = new List<Transform>();
        private static float threatsCacheTime = -999f;

private void Start()
        {
            cruiseAltitude = Random.Range(cruiseAltitudeMin, cruiseAltitudeMax);

            if (groundSpawnPoint != null)
            {
                // Starts already landed - stays put until something startles it.
                transform.position = groundSpawnPoint.position;
                landingTarget = groundSpawnPoint;
                isGroundLanding = true;
                state = State.Perched;
                return;
            }

            heading = RandomHeading();
            if (transform.position.y < 0.5f)
                transform.position = new Vector3(transform.position.x, cruiseAltitude, transform.position.z);
            ScheduleNextDecision();
        }

        private void Update()
        {
            switch (state)
            {
                case State.Wander:
                    FlyForward();
                    if (Time.time >= nextDecisionTime)
                        MakeWanderDecision();
                    break;

                case State.Circle:
                    UpdateCircle();
                    circleTimer -= Time.deltaTime;
                    if (circleTimer <= 0f)
                    {
                        heading = RandomHeading();
                        state = State.Wander;
                        ScheduleNextDecision();
                    }
                    break;

                case State.FlyToLanding:
                    FlyTowardLanding();
                    break;

                case State.Perched:
                    FoldWings();
                    IdleBob();
                    if (isGroundLanding)
                    {
                        // Grounded birds (landed or spawned on the ground) stay put
                        // indefinitely - only a threat gets them moving again.
                        if (IsThreatNear())
                            BeginTakeOff();
                    }
                    else
                    {
                        perchTimer -= Time.deltaTime;
                        if (perchTimer <= 0f || IsThreatNear())
                            BeginTakeOff();
                    }
                    break;

                case State.TakeOff:
                    // A startled takeoff climbs hard and fast - it should read as
                    // bolting into the air, not jogging off along the ground.
                    Vector3 climbDir = (heading + Vector3.up * 1.6f).normalized;
                    transform.position += climbDir * flightSpeed * 1.4f * Time.deltaTime;
                    FaceHeading(climbDir);
                    Flap(1.3f);
                    if (transform.position.y >= Mathf.Min(cruiseAltitude, LandingY() + 3f))
                    {
                        state = State.Wander;
                        ScheduleNextDecision();
                    }
                    break;
            }

            KeepWithinWanderArea();
        }

        // ---------------------------------------------------------------- wander

        private void MakeWanderDecision()
        {
            float roll = Random.value;
            if (roll < landChance && landingSpots != null && landingSpots.Length > 0)
            {
                BeginLanding();
            }
            else if (roll < landChance + circleChance)
            {
                BeginCircle();
            }
            else
            {
                // Drift to a new heading rather than snapping, so it still reads as
                // "flying in a direction" rather than picking random points.
                heading = Vector3.Slerp(heading, RandomHeading(), 0.6f).normalized;
                ScheduleNextDecision();
            }
        }

        private void ScheduleNextDecision()
        {
            nextDecisionTime = Time.time + Random.Range(headingChangeInterval.x, headingChangeInterval.y);
        }

        private Vector3 RandomHeading()
        {
            float angle = Random.Range(0f, 360f);
            return new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad));
        }

private void FlyForward()
        {
            transform.position += heading * flightSpeed * Time.deltaTime;
            CorrectAltitude();
            FaceHeading(heading);
            Flap(1f);
        }

        // Gently pulls Y back toward this bird's cruising altitude - well above tree
        // height - so it doesn't linger low after a takeoff or a circle.
        private void CorrectAltitude()
        {
            float y = Mathf.MoveTowards(transform.position.y, cruiseAltitude, altitudeCorrectSpeed * Time.deltaTime);
            transform.position = new Vector3(transform.position.x, y, transform.position.z);
        }

        private void FaceHeading(Vector3 dir)
        {
            if (dir.sqrMagnitude < 0.0001f) return;
            Quaternion look = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * turnSpeed * 3f);
        }

        // Softly steer back if drifting too far from the bird's home area, instead
        // of a hard wall - it just starts curving back in.
        private void KeepWithinWanderArea()
        {
            Vector3 offset = transform.position - worldCenter;
            offset.y = 0f;
            if (offset.magnitude > wanderExtent && (state == State.Wander || state == State.Circle))
            {
                Vector3 inward = -offset.normalized;
                heading = Vector3.Slerp(heading, inward, Time.deltaTime * 1.5f).normalized;
            }
        }

        // ---------------------------------------------------------------- circle

        private void BeginCircle()
        {
            circlePivot = transform.position + heading * circleRadius;
            circleAngle = Vector3.SignedAngle(Vector3.forward, transform.position - circlePivot, Vector3.up);
            circleTimer = Random.Range(circleDuration.x, circleDuration.y);
            state = State.Circle;
        }

        private void UpdateCircle()
        {
            circleAngle += circleAngularSpeed * Time.deltaTime;
            float rad = circleAngle * Mathf.Deg2Rad;
            Vector3 target = circlePivot + new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * circleRadius;
            target.y = cruiseAltitude;
            Vector3 dir = target - transform.position;
            FaceHeading(dir);
            transform.position = Vector3.MoveTowards(transform.position, target, flightSpeed * Time.deltaTime);
            heading = dir.normalized;
            Flap(1f);
        }

        // ---------------------------------------------------------------- landing

        private void BeginLanding()
        {
            // Prefer a spot actually close by so the flight is quick and visible,
            // rather than a random one that could be clear across the map.
            Transform pick = null;
            float bestDist = landingSearchRadius;
            for (int i = 0; i < landingSpots.Length; i++)
            {
                Transform s = landingSpots[i];
                if (s == null) continue;
                float d = Vector3.Distance(transform.position, s.position);
                if (d < bestDist) { bestDist = d; pick = s; }
            }

            if (pick == null)
            {
                // Nothing close enough right now - try again on the next decision tick
                // instead of giving up on landing altogether.
                ScheduleNextDecision();
                return;
            }

            landingTarget = pick;
            isGroundLanding = landingTarget.name.StartsWith("GroundSpot");
            state = State.FlyToLanding;
        }

        private float LandingY()
        {
            return landingTarget != null ? landingTarget.position.y : transform.position.y;
        }

        private void FlyToward(Vector3 target, float speed)
        {
            Vector3 dir = target - transform.position;
            FaceHeading(dir);
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            if (dir.sqrMagnitude > 0.0001f) heading = dir.normalized;
        }

        private void FlyTowardLanding()
        {
            if (landingTarget == null) { state = State.Wander; ScheduleNextDecision(); return; }

            Flap(1f);
            FlyToward(landingTarget.position, flightSpeed * 0.7f);

            if (Vector3.Distance(transform.position, landingTarget.position) < 0.6f)
            {
                if (IsThreatNear())
                {
                    // Occupied by a threat right now - bail and go back to wandering.
                    state = State.Wander;
                    heading = RandomHeading();
                    ScheduleNextDecision();
                    return;
                }
                transform.position = landingTarget.position;
                transform.rotation = landingTarget.rotation;
                perchTimer = Random.Range(perchTime.x, perchTime.y);
                state = State.Perched;
            }
        }

        private void BeginTakeOff()
        {
            // Push off away from whatever spooked it, or straight up if nothing did.
            Transform threat = NearestThreat();
            if (threat != null)
            {
                Vector3 away = transform.position - threat.position;
                away.y = 0f;
                heading = away.sqrMagnitude > 0.01f ? away.normalized : RandomHeading();
            }
            else
            {
                heading = RandomHeading();
            }
            state = State.TakeOff;
        }

private void IdleBob()
        {
            if (landingTarget == null) return;

            if (!isGroundLanding)
            {
                float bob = Mathf.Sin(Time.time * 2f) * 0.03f;
                transform.position = landingTarget.position + Vector3.up * (0.05f + bob);
                return;
            }

            // On the ground: potter about in a small area instead of standing still.
            if (Time.time >= nextGroundWalkTime)
            {
                Vector2 offset = Random.insideUnitCircle * groundWalkRadius;
                groundWalkTarget = landingTarget.position + new Vector3(offset.x, 0f, offset.y);
                nextGroundWalkTime = Time.time + Random.Range(groundWalkInterval.x, groundWalkInterval.y);
            }

            Vector3 toTarget = groundWalkTarget - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.01f)
            {
                FaceHeading(toTarget);
                transform.position = Vector3.MoveTowards(transform.position, groundWalkTarget, groundWalkSpeed * Time.deltaTime);
            }

            float hop = Mathf.Abs(Mathf.Sin(Time.time * 5f)) * 0.02f * Mathf.Clamp01(toTarget.magnitude);
            transform.position = new Vector3(transform.position.x, landingTarget.position.y + 0.05f + hop, transform.position.z);
        }

        // ---------------------------------------------------------------- threats

        private bool IsThreatNear()
        {
            return NearestThreat() != null;
        }

        private Transform NearestThreat()
        {
            RefreshThreatsCache();
            Transform nearest = null;
            float best = fleeRadius * fleeRadius;
            foreach (var t in threatsCache)
            {
                if (t == null || !t.gameObject.activeInHierarchy) continue;
                float d = (t.position - transform.position).sqrMagnitude;
                if (d < best) { best = d; nearest = t; }
            }
            return nearest;
        }

        // Shared across all birds, refreshed at most a few times a second.
        private static void RefreshThreatsCache()
        {
            if (Time.time - threatsCacheTime < 0.5f) return;
            threatsCacheTime = Time.time;
            threatsCache.Clear();

            GameObject playerGo = GameObject.Find("Player");
            if (playerGo != null) threatsCache.Add(playerGo.transform);

            foreach (Npc npc in Npc.All)
                if (npc != null && npc.Role != NpcRole.Waystone && npc.Role != NpcRole.Stash && npc.Role != NpcRole.QuestProp)
                    threatsCache.Add(npc.transform);

            var enemies = Object.FindObjectsByType<EnemyController>();
            foreach (var e in enemies)
                threatsCache.Add(e.transform);
        }

        // ---------------------------------------------------------------- visuals

        private void Flap(float intensity)
        {
            wingFold = Mathf.MoveTowards(wingFold, 0f, wingFoldBlendSpeed * Time.deltaTime);
            float angle = Mathf.Sin(Time.time * wingFlapSpeed) * wingFlapAngle * intensity;
            ApplyWingPose(angle, angle);
        }

        // Simple static "tucked in" pose: swing each wing back and up against the body
        // instead of leaving it spread flat, so a landed/walking bird doesn't look like
        // it's still gliding.
        private void FoldWings()
        {
            wingFold = Mathf.MoveTowards(wingFold, 1f, wingFoldBlendSpeed * Time.deltaTime);
            ApplyWingPose(0f, 0f);
        }

        private void ApplyWingPose(float leftFlapZ, float rightFlapZ)
        {
            float swing = wingFold * wingFoldSwing;
            float tuck = wingFold * wingFoldTuck;
            if (leftWing != null) leftWing.localRotation = Quaternion.Euler(0f, -swing, leftFlapZ + tuck);
            if (rightWing != null) rightWing.localRotation = Quaternion.Euler(0f, swing, -rightFlapZ - tuck);
        }
    }
}
