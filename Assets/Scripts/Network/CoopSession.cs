using System;
using System.Collections.Generic;
using UnityEngine;
using PoeClone.Combat;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.Network.Replication;
using PoeClone.Player;
using PoeClone.UI;
using PoeClone.World;

namespace PoeClone.Network
{
    /// <summary>
    /// A running co-op game, on either side. The host runs shared-area enemies and loot; a guest
    /// alone runs its own area. Twenty times a second each side sends the other a snapshot - the
    /// host its character and area enemies, the guest its character, both their minions - and
    /// events go both ways as they happen: the guest's hits on enemies (applied by the host),
    /// enemy attacks on the guest and its share of kills, and every drop appearing, leaving or
    /// being claimed. Each player's character, gear and progress stay their own and are saved
    /// on their own machine, so leaving co-op carries on with the same character alone.
    /// </summary>
    public partial class CoopSession : MonoBehaviour
    {
        private const float SendInterval = 0.05f;
        // A kill this close to the partner (in the same area) counts for them too.
        private const float SharedKillRange = 60f;
        // Melee blows are judged on the guest against its copy of the enemy, which runs a little
        // behind the host's: a bit of extra reach so a blow that visibly connects still lands.
        private const float MeleeLatencySlack = 0.6f;

        public bool IsHost { get; private set; }
        public string PartnerName { get; private set; }

        private GameSessionController session;
        private PlayerStateBroadcaster broadcaster;
        private SpectatorReplica replica;
        private CoopPartner partner;
        private PlayerStats stats;
        private PlayerInventory inventory;
        private double nextSendAt;
        private bool placedNearHost;
        private Transform arrival;

        private readonly List<LootDrop> newDrops = new List<LootDrop>();
        private readonly Dictionary<int, LootDrop> claims = new Dictionary<int, LootDrop>();
        private readonly CoopEvent outgoing = new CoopEvent();

        public void Begin(bool host, string partnerName, PlayerStateBroadcaster stateBroadcaster, SpectatorReplica enemyReplica)
        {
            session = GameSessionController.Instance;
            broadcaster = stateBroadcaster;
            replica = enemyReplica;
            IsHost = host;
            PartnerName = partnerName;
            stats = FindAnyObjectByType<PlayerStats>();
            inventory = stats != null ? stats.GetComponent<PlayerInventory>() : null;

            Party.Begin(host);
            partner = gameObject.AddComponent<CoopPartner>();
            if (stats != null)
                partner.Begin(stats.transform, partnerName);

            if (host)
            {
                Party.EnemyAttacked = SendEnemyAttack;
                Party.EnemySkillUsed = SendEnemySkill;
                Party.BossMoveStarted = SendBossMove;
                Party.PartnerDamaged = SendPartnerDamage;
                Party.EffectShown = SendEffect;
                EnemyHealth.Killed += OnEnemyKilled;
                LootDrop.Spawned = drop => newDrops.Add(drop);
                LootDrop.Removed = OnDropRemoved;
                newDrops.AddRange(LootDrop.All); // the ground as it is now
            }
            else
            {
                foreach (LootDrop drop in new List<LootDrop>(LootDrop.All))
                    if (drop != null && !drop.IsShared) Destroy(drop.gameObject);
                replica.EnterCoopGuest();
                EnemyHealth.RemoteHit = SendHit;
                LootDrop.SharedClaim = SendClaim;
                LootDrop.SharedPlace = SendPlace;
            }
        }

        private void OnDestroy()
        {
            EnemyHealth.Killed -= OnEnemyKilled;
            EnemyHealth.RemoteHit = null;
            LootDrop.Spawned = null;
            LootDrop.Removed = null;
            LootDrop.SharedClaim = null;
            LootDrop.SharedPlace = null;
            Party.End();
            if (partner != null)
                Destroy(partner);
            if (arrival != null)
                Destroy(arrival.gameObject);
        }

        // ------------------------------------------------------------------ sending

        private void LateUpdate()
        {
            if (session == null || !session.Connected)
                return;
            UpdateAreaAuthority();

            if (IsHost)
                SendNewDrops();
            else
                PlaceNearHost();

            double now = Time.realtimeSinceStartupAsDouble;
            if (now < nextSendAt)
                return;
            nextSendAt = Math.Max(nextSendAt + SendInterval, now + SendInterval * 0.6);

            StateSnapshot latest = partner != null ? partner.Newest : null;
            Vector3? partnerAt = IsHost && latest != null && latest.p != null
                ? new Vector3(latest.p.x, latest.p.y, latest.p.z) : (Vector3?)null;
            string json = broadcaster.CaptureCoop(withEnemies: IsHost, alsoAround: partnerAt);
            if (json != null)
                session.SendCoop(json);
        }

        private void Send(CoopEvent e)
        {
            session.SendCoop(JsonUtility.ToJson(e));
        }

        private CoopEvent NewEvent(string kind)
        {
            outgoing.k = kind;
            outgoing.ar = AreaManager.Instance != null ? AreaManager.Instance.CurrentAreaIndex : 0;
            outgoing.id = 0;
            outgoing.a = outgoing.ap = outgoing.ep = outgoing.r = 0f;
            outgoing.dt = outgoing.f = outgoing.n = outgoing.ek = outgoing.xp = 0;
            outgoing.x = outgoing.y = outgoing.z = outgoing.tx = outgoing.ty = outgoing.tz = 0f;
            outgoing.it = null;
            outgoing.mv = outgoing.sd = outgoing.lv = 0;
            outgoing.sp = outgoing.dm = 0f;
            outgoing.v = null;
            outgoing.w = null;
            return outgoing;
        }

        private static void SetAt(CoopEvent e, Vector3 at)
        {
            e.x = at.x;
            e.y = at.y;
            e.z = at.z;
        }

        private static void SetAim(CoopEvent e, Vector3 at)
        {
            e.tx = at.x;
            e.ty = at.y;
            e.tz = at.z;
        }

        private CoopEvent EnemyEvent(string kind, Component source, bool atPartner)
        {
            EnemyHealth health = source.GetComponent<EnemyHealth>();
            CoopEvent e = NewEvent(kind);
            e.id = broadcaster.IdOf(health);
            e.ek = health.KindIndex;
            e.lv = health.MonsterLevel;
            e.f = atPartner ? CoopProtocol.FlagAtYou : 0;
            SetAt(e, source.transform.position);
            e.r = source.transform.eulerAngles.y;
            return e;
        }

        private void SendEnemySkill(Component source, bool atPartner, float damage, Vector3 target)
        {
            CoopEvent e = EnemyEvent(CoopProtocol.Skill, source, atPartner);
            e.a = damage;
            SetAim(e, target);
            Send(e);
        }

        private void SendBossMove(Component source, int move, bool atPartner, int seed, float speed, float rage, int level)
        {
            CoopEvent e = EnemyEvent(CoopProtocol.BossMove, source, atPartner);
            e.mv = move;
            e.sd = seed;
            e.sp = speed;
            e.dm = rage;
            e.lv = level;
            Send(e);
        }

        private void SendPartnerDamage(float damage, DamageType type, bool attack, float poison, float seconds)
        {
            CoopEvent e = NewEvent(CoopProtocol.Damage);
            e.a = damage;
            e.dt = (int)type;
            e.f = attack ? CoopProtocol.FlagAttack : 0;
            e.ap = poison;
            e.ep = seconds;
            Send(e);
        }

        private void SendEffect(Party.EffectKind kind, float[] values)
        {
            CoopEvent e = NewEvent(CoopProtocol.Effect);
            e.n = (int)kind;
            e.v = values;
            Send(e);
        }

        private void PlayEffect(CoopEvent e)
        {
            float[] v = e.v;
            if (v == null || v.Length < 3) return;
            Vector3 at = new Vector3(v[0], v[1], v[2]);
            switch ((Party.EffectKind)e.n)
            {
                case Party.EffectKind.Circle when v.Length >= 6:
                    StartCoroutine(GroundTelegraph.Run(at, v[3], v[4], (DamageType)(int)v[5], null));
                    break;
                case Party.EffectKind.Line when v.Length >= 9:
                    StartCoroutine(GroundTelegraph.RunLine(at, new Vector3(v[3], 0f, v[4]), v[5], v[6], v[7], (DamageType)(int)v[8], null));
                    break;
                case Party.EffectKind.Glob when v.Length >= 10:
                    VenomGlob.Lob(at, new Vector3(v[3], v[4], v[5]), v[6], v[7], v[8], v[9], null, null);
                    break;
                case Party.EffectKind.Snake when v.Length >= 7:
                    GroundSnake.Spawn(at, new Vector3(v[3], 0f, v[4]), v[5], v[6]);
                    break;
                case Party.EffectKind.Dive when v.Length >= 9:
                    DivingSerpent.Launch(at, new Vector3(v[3], v[4], v[5]), v[6], v[7], v[8]);
                    break;
            }
        }

        // Guest: a hit on a copy of a host enemy, for the host to apply.
        private void SendHit(EnemyHealth enemy, EnemyHealth.HitClaim hit)
        {
            CoopEvent e = NewEvent(CoopProtocol.Hit);
            e.id = enemy.RemoteId;
            e.a = hit.Amount;
            e.dt = (int)hit.Type;
            e.ap = hit.ArmourPenetration;
            e.ep = hit.ElementalPenetration;
            e.f = (hit.ThroughExposedHead ? CoopProtocol.FlagHead : 0) | (hit.CanEnrage ? CoopProtocol.FlagEnrage : 0) |
                  (hit.Flinch ? CoopProtocol.FlagFlinch : 0) | (hit.HasOrigin ? CoopProtocol.FlagOrigin : 0);
            SetAt(e, hit.Origin);
            Send(e);
        }

        // Host: an enemy attack the guest has to see (or take).
        private void SendEnemyAttack(Component enemy, Party.AttackKind kind, bool atPartner, float damage, float reach, Vector3 from, Vector3 aim)
        {
            var health = enemy.GetComponent<EnemyHealth>();
            if (health == null)
                return;
            CoopEvent e = NewEvent(CoopProtocol.Attack);
            e.id = broadcaster.IdOf(health);
            e.ek = health.KindIndex;
            e.n = (int)kind;
            e.f = atPartner ? CoopProtocol.FlagAtYou : 0;
            e.a = damage;
            e.r = reach;
            SetAt(e, from);
            SetAim(e, aim);
            Send(e);
        }

        // Host: an enemy's special skill, for the guest's copy to play.
        private void SendSkill(Component enemy, bool atPartner, float damage, Vector3 target)
        {
            var health = enemy.GetComponent<EnemyHealth>();
            if (health == null || !PartnerHere)
                return;
            CoopEvent e = NewEvent(CoopProtocol.Skill);
            e.id = broadcaster.IdOf(health);
            e.ek = health.KindIndex;
            e.f = atPartner ? CoopProtocol.FlagAtYou : 0;
            e.a = damage;
            SetAt(e, enemy.transform.position);
            SetAim(e, target);
            Send(e);
        }

        // Host: a blow judged here against the partner's character landed.
        private void SendDamage(float damage, DamageType type, bool attack, float poison, float poisonSeconds)
        {
            CoopEvent e = NewEvent(CoopProtocol.Damage);
            e.a = damage;
            e.dt = (int)type;
            e.f = attack ? CoopProtocol.FlagAttack : 0;
            e.ap = poison;
            e.ep = poisonSeconds;
            Send(e);
        }

        // Host: the partner is in this area (so its enemies' doings concern them).
        private bool PartnerHere
        {
            get
            {
                StateSnapshot latest = partner != null ? partner.Newest : null;
                var areas = AreaManager.Instance;
                return latest != null && areas != null && latest.area == areas.CurrentAreaIndex;
            }
        }

        // Host: the guest gets the experience (and the quest credit) of kills near them.
        private void OnEnemyKilled(EnemyHealth enemy)
        {
            StateSnapshot latest = partner != null ? partner.Newest : null;
            var areas = AreaManager.Instance;
            if (latest == null || latest.p == null || areas == null || latest.area != areas.CurrentAreaIndex)
                return;
            Vector3 at = enemy.transform.position;
            Vector3 offset = new Vector3(latest.p.x - at.x, 0f, latest.p.z - at.z);
            if (offset.sqrMagnitude > SharedKillRange * SharedKillRange)
                return;
            CoopEvent e = NewEvent(CoopProtocol.Kill);
            e.ek = enemy.KindIndex;
            e.r = enemy.MonsterLevel;
            e.xp = enemy.ExperienceReward;
            SetAt(e, at);
            Send(e);
        }

        // Host: drops that appeared since the last frame (sent a frame late, once they know where
        // they're popping out from).
        private void SendNewDrops()
        {
            foreach (LootDrop drop in newDrops)
            {
                if (drop == null || !drop.IsInteractive)
                    continue;
                CoopEvent e = NewEvent(CoopProtocol.Drop);
                e.id = drop.Id;
                e.a = drop.Amount;
                e.it = GearCodec.ToWire(drop.Item);
                SetAt(e, drop.transform.position);
                Vector3? pop = drop.PoppingFrom;
                if (pop.HasValue)
                {
                    e.f = CoopProtocol.FlagPops;
                    SetAim(e, pop.Value);
                }
                Send(e);
            }
            newDrops.Clear();
        }

        private void OnDropRemoved(LootDrop drop)
        {
            newDrops.Remove(drop);
            if (session == null || !session.Connected)
                return;
            CoopEvent e = NewEvent(CoopProtocol.Gone);
            e.id = drop.Id;
            Send(e);
        }

        private void SendClaim(LootDrop drop)
        {
            claims[drop.Id] = drop;
            CoopEvent e = NewEvent(CoopProtocol.Claim);
            e.id = drop.Id;
            Send(e);
        }

        private void SendPlace(ItemData item, Vector3 at)
        {
            CoopEvent e = NewEvent(CoopProtocol.Place);
            e.it = GearCodec.ToWire(item);
            SetAt(e, at);
            Send(e);
        }

        // Guest: starts out next to the host (in the host's area), the first time the host's
        // whereabouts are known.
        private void PlaceNearHost()
        {
            if (placedNearHost || stats == null || !SaveSystem.CharacterLoaded)
                return;
            StateSnapshot host = partner != null ? partner.Newest : null;
            var areas = AreaManager.Instance;
            if (host == null || host.p == null || areas == null || areas.IsSwitching || host.fade != 0)
                return;
            placedNearHost = true;

            Vector3 spot = new Vector3(host.p.x, host.p.y, host.p.z) + Quaternion.Euler(0f, host.p.r, 0f) * new Vector3(1.6f, 0f, -0.8f);
            if (areas.CurrentAreaIndex != host.area)
            {
                if (arrival == null)
                    arrival = new GameObject("CoopArrival").transform;
                arrival.SetPositionAndRotation(spot, Quaternion.Euler(0f, host.p.r, 0f));
                areas.EnterArea(host.area, arrival);
                return;
            }

            var controller = stats.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            stats.transform.position = spot;
            if (controller != null) controller.enabled = !stats.IsDead;
            stats.SetSpawnPoint(spot, stats.transform.rotation);
            var cam = Camera.main != null ? Camera.main.GetComponent<CameraSystem.CameraFollow>() : null;
            if (cam != null)
                cam.SnapToTarget();
        }

        // ------------------------------------------------------------------ receiving

        public void HandleSnapshot(string json)
        {
            StateSnapshot s = SnapshotCodec.Deserialize(json, CoopProtocol.Snapshot);
            if (s == null || partner == null)
                return;
            partner.Feed(s);
            ReceiveWorldSnapshot(s);
            if (!IsHost)
                replica.HandleCoopState(s);
        }

        public void HandleEvent(string json)
        {
            CoopEvent e;
            try
            {
                e = JsonUtility.FromJson<CoopEvent>(json);
            }
            catch (Exception)
            {
                return;
            }
            if (e == null || string.IsNullOrEmpty(e.k))
                return;
            if ((e.k == CoopProtocol.Attack || e.k == CoopProtocol.Skill || e.k == CoopProtocol.BossMove ||
                 e.k == CoopProtocol.Damage || e.k == CoopProtocol.Effect || e.k == CoopProtocol.Kill) &&
                (!Party.SharingArea || AreaManager.Instance == null || e.ar != AreaManager.Instance.CurrentAreaIndex))
                return;

            if (IsHost)
            {
                switch (e.k)
                {
                    case CoopProtocol.Hit: ApplyPartnerHit(e); break;
                    case CoopProtocol.Claim: AnswerClaim(e.id); break;
                    case CoopProtocol.Place: PlaceForPartner(e); break;
                    case CoopProtocol.World: AcceptWorld(e); break;
                }
                return;
            }

            switch (e.k)
            {
                case CoopProtocol.Skill:
                case CoopProtocol.BossMove:
                    replica.PlayCoopMove(e);
                    break;
                case CoopProtocol.Damage:
                    if (stats != null && !stats.IsDead && Party.SharingArea)
                    {
                        if (e.a <= 0f)
                            stats.PoisonFromPool(e.ap, e.ep);
                        else if (stats.TakeHit(e.a, (DamageType)e.dt, (e.f & CoopProtocol.FlagAttack) != 0) && e.ap > 0f && !stats.IsDead)
                            stats.Poison(e.ap, e.ep);
                    }
                    break;
                case CoopProtocol.Effect:
                    if (Party.SharingArea) PlayEffect(e);
                    break;
                case CoopProtocol.Attack: TakeEnemyAttack(e); break;
                case CoopProtocol.Kill:
                    EnemyHealth.GrantKillRewards(e.ek, Mathf.RoundToInt(e.r), e.xp, new Vector3(e.x, e.y, e.z), dropLoot: false);
                    break;
                case CoopProtocol.Drop: ShowDrop(e); break;
                case CoopProtocol.Gone: RemoveDrop(e.id); break;
                case CoopProtocol.Granted: ClaimAnswered(e.id, granted: true); break;
                case CoopProtocol.Denied: ClaimAnswered(e.id, granted: false); break;
            }
        }

        // Host: the guest hit one of our enemies. Applied exactly as if the hit happened here,
        // credited to the partner for the enemy's aggro.
        private void ApplyPartnerHit(CoopEvent e)
        {
            EnemyHealth enemy = broadcaster.EnemyById(e.id);
            if (enemy == null || enemy.IsDead)
                return;
            Party.ApplyingPartnerDamage = true;
            try
            {
                enemy.TakeDamage(e.a, (DamageType)e.dt, e.ap, e.ep,
                    throughExposedHead: (e.f & CoopProtocol.FlagHead) != 0,
                    canEnrage: (e.f & CoopProtocol.FlagEnrage) != 0,
                    flinch: (e.f & CoopProtocol.FlagFlinch) != 0,
                    hitOrigin: (e.f & CoopProtocol.FlagOrigin) != 0 ? new Vector3(e.x, e.y, e.z) : (Vector3?)null);
            }
            finally
            {
                Party.ApplyingPartnerDamage = false;
            }
        }

        // Host: first come, first served - if it's still on the ground, it's theirs.
        private void AnswerClaim(int id)
        {
            LootDrop drop = null;
            foreach (LootDrop each in LootDrop.All)
            {
                if (each != null && each.Id == id && each.IsInteractive)
                    drop = each;
            }
            CoopEvent e = NewEvent(drop != null ? CoopProtocol.Granted : CoopProtocol.Denied);
            e.id = id;
            Send(e);
            if (drop != null)
                Destroy(drop.gameObject); // its "gone" follows the grant
        }

        private void PlaceForPartner(CoopEvent e)
        {
            ItemData item = GearCodec.ToItem(e.it);
            if (item != null)
                LootDrop.Spawn(item, LootDrop.GroundBelow(new Vector3(e.x, e.y, e.z)), interactive: true, id: 0);
        }

        // Guest: an enemy attack. Ones at us are resolved here, where our character really is.
        private void TakeEnemyAttack(CoopEvent e)
        {
            if (stats == null)
                return;
            EnemyKind kind = EnemyKinds.Get(e.ek);
            bool atUs = (e.f & CoopProtocol.FlagAtYou) != 0;
            Vector3 from = new Vector3(e.x, e.y, e.z);
            Vector3 aim = new Vector3(e.tx, e.ty, e.tz);
            switch ((Party.AttackKind)e.n)
            {
                case Party.AttackKind.Melee:
                    if (!atUs || stats.IsDead)
                        return;
                    EnemyHealth copy = replica.PuppetHealth(e.id);
                    Vector3 enemyAt = copy != null ? copy.transform.position : from;
                    Vector3 offset = stats.transform.position - enemyAt;
                    offset.y = 0f;
                    if (offset.magnitude <= e.r + MeleeLatencySlack)
                        stats.TakeHit(e.a, kind.DamageType);
                    break;
                case Party.AttackKind.Bolt:
                    if (atUs)
                        EnemyProjectile.LaunchAt(from, aim, stats, kind, e.a);
                    else
                        EnemyProjectile.LaunchVisual(from, aim, kind);
                    break;
                case Party.AttackKind.Rain:
                    EnemySkills.RainOfArrows(this, kind, aim, atUs ? stats : null, atUs ? e.a : 0f);
                    break;
            }
        }

        // Guest: the host's enemy used its special skill; our copy plays it.
        private void PlaySkill(CoopEvent e)
        {
            EnemyHealth copy = replica.PuppetHealth(e.id);
            EnemySkills skills = copy != null ? copy.GetComponent<EnemySkills>() : null;
            if (skills != null)
                skills.Mirror(new Vector3(e.tx, e.ty, e.tz), e.a, (e.f & CoopProtocol.FlagAtYou) != 0);
        }

        // Guest: the host's boss started a move; our copy plays it.
        private void PlayBossMove(CoopEvent e)
        {
            EnemyHealth copy = replica.PuppetHealth(e.id);
            BossAbilities boss = copy != null ? copy.GetComponent<BossAbilities>() : null;
            if (boss != null)
                boss.PlayMirror(e.mv, (e.f & CoopProtocol.FlagAtYou) != 0, e.sd, e.sp, e.dm, e.lv);
        }

        // Guest: a blow the host's game judged against our character.
        private void TakeDamage(CoopEvent e)
        {
            if (stats == null || stats.IsDead)
                return;
            if (stats.TakeHit(e.a, (DamageType)e.dt, (e.f & CoopProtocol.FlagAttack) != 0) && e.ap > 0f && !stats.IsDead)
                stats.Poison(e.ap, e.ep);
        }

        private void ShowDrop(CoopEvent e)
        {
            if (FindDrop(e.id) != null || claims.ContainsKey(e.id))
                return;
            ItemData item = GearCodec.ToItem(e.it);
            if (item == null)
                return;
            Vector3? pop = (e.f & CoopProtocol.FlagPops) != 0 ? new Vector3(e.tx, e.ty, e.tz) : (Vector3?)null;
            LootDrop.SpawnShared(item, new Vector3(e.x, e.y, e.z), e.id, Mathf.RoundToInt(e.a), pop);
        }

        private void RemoveDrop(int id)
        {
            if (claims.ContainsKey(id))
                return; // ours: the grant or denial says what happens to it
            LootDrop drop = FindDrop(id);
            if (drop != null)
                Destroy(drop.gameObject);
        }

        private void ClaimAnswered(int id, bool granted)
        {
            if (!claims.TryGetValue(id, out LootDrop drop))
                return;
            claims.Remove(id);
            if (drop == null)
                return;
            Vector3 notice = stats != null ? stats.transform.position + Vector3.up * 2f : drop.transform.position;
            if (granted)
            {
                drop.ClaimGranted(inventory, notice);
                return;
            }
            CombatText.Show(notice, PartnerName + " got it first", CombatText.AvoidColor, 0.9f);
            Destroy(drop.gameObject);
        }

        private static LootDrop FindDrop(int id)
        {
            foreach (LootDrop drop in LootDrop.All)
            {
                if (drop != null && drop.Id == id && drop.IsShared)
                    return drop;
            }
            return null;
        }
    }
}
