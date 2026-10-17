using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using PoeClone.Combat;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.Player;
using PoeClone.World;

namespace PoeClone.Network
{
    /// <summary>
    /// Starts the chosen character locally. Chat and spectator streaming connect in the
    /// background; server availability never controls local gameplay.
    /// </summary>
    public class GameSessionController : MonoBehaviour
    {
        public static GameSessionController Instance { get; private set; }

        public SessionRole Role { get; private set; } = SessionRole.Player;
        public bool Connected { get; private set; }
        /// <summary>A character was selected for local play, independently of the server.</summary>
        public bool PlayGranted { get; private set; }
        public bool GameplayReady { get; private set; }
        public bool RemotePlayerActive { get; private set; }
        public int SpectatorCount { get; private set; }
        public int MaxPlayers { get; private set; }
        /// <summary>Everyone playing right now, oldest first (from the server's status).</summary>
        public System.Collections.Generic.IReadOnlyList<PlayerInfo> Players => players;
        /// <summary>Spectators: the id of the player being watched (0 = nobody).</summary>
        public int WatchingId { get; private set; }

        private PlayerInfo[] players = new PlayerInfo[0];
        public string DenyReason { get; private set; }
        public string DisconnectReason { get; private set; }
        public bool Reconnecting { get; private set; }
        /// <summary>Display name sent to the server; empty lets the server pick a default.</summary>
        public string PlayerName { get; private set; } = string.Empty;

        public event Action StateChanged;
        public event Action<ChatEnvelope> ChatReceived;

        private const string StatePrefix = "{\"type\":\"state\"";
        private const string GearPrefix = "{\"type\":\"gear\"";
        private const string NamePrefKey = "PoeClone.PlayerName";
        // Unsent bytes above which snapshots are skipped (the next one supersedes them), so chat
        // and events don't queue behind seconds of them on a slow upload.
        private const int MaxSnapshotBacklogBytes = 64 * 1024;

        private WebSocketClient client;
        private PlayerStateBroadcaster stateBroadcaster;
        private SpectatorReplica replica;

        // Co-op: what the player picked in the co-op menu. Joining lasts until the game starts;
        // a host plays at once and stays hosting (listed while it has no guest) for the session.
        private enum CoopIntent { None, Join }
        private CoopIntent coopIntent;
        private bool hosting;
        private bool hostAnnounced;
        private CoopSession coop;
        private PlayerInfo[] lobby = new PlayerInfo[0];

        /// <summary>Shown on the start menu after a co-op game ended (the scene reloads for it).</summary>
        private static string startNotice;

        // Editor Play (the dev build) skips the menus once per Play, so Return to Main Menu still reaches them.
        private static bool devStartUsed;

        /// <summary>An area that replaces the saved destination for this start (the dev build's Greenwood).</summary>
        public int StartAreaOverride { get; private set; } = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            startNotice = null;
            devStartUsed = false;
        }
        private NamePromptUI namePrompt;
        private AudioListener menuAudioListener;
        private GameObject scenePlayer;
        private readonly System.Collections.Generic.List<GameObject> suspendedRoots = new System.Collections.Generic.List<GameObject>();
        private bool gameplayCreated;

        public SpectatorReplica Replica => replica;
        public CoopSession Coop => coop;
        private string serverUrl;
        private float reconnectDelay = 2f;
        private Coroutine reconnectRoutine;
        private bool returningToMainMenu;
        private bool serverPlayGranted;

        // Only the update handoff carries a location. Normal visits retain their spawn rules.
        [Serializable]
        private class UpdateResume
        {
            public int version = 1;
            public string profile;
            public int area;
            public Vector3 position;
            public float facing;
            public bool hosting;
            public string party;
            public bool host;
            public string partner;
            public System.Collections.Generic.List<LootDrop.UpdateRecord> drops;
            public System.Collections.Generic.List<EnemySpawner.UpdateState> populations;
        }
        private UpdateResume updateResume;
        private bool updatingForReload;
        private bool worldActive;
        private bool notifyAfterUpdate;
        private System.Collections.Generic.List<LootDrop.UpdateRecord> updateDrops;
        private System.Collections.Generic.List<EnemySpawner.UpdateState> updatePopulations;
        public bool ResumingUpdate => notifyAfterUpdate;

        public EnemySpawner.UpdateState TakeUpdatePopulation(int area)
        {
            var state = updatePopulations?.Find(p => p.area == area);
            if (state != null) updatePopulations.Remove(state);
            return state;
        }

        private System.Collections.Generic.List<EnemySpawner.UpdateState> CaptureUpdatePopulations()
        {
            var states = new System.Collections.Generic.List<EnemySpawner.UpdateState>();
            // Unvisited areas can still carry a handoff from an earlier deployment.
            if (updatePopulations != null) states.AddRange(updatePopulations);
            for (int area = 0; area < WorldBuilder.AreaNames.Length; area++)
            {
                var spawner = WorldBuilder.Instance?.SpawnerFor(area);
                if (spawner != null) states.Add(spawner.CaptureForUpdate());
            }
            return states;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void UpdateBridge_ReadResume();
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void UpdateBridge_SaveResume(string json);
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void UpdateBridge_RequestUpdate();
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void UpdateBridge_Completed();
#endif

        /// <summary>Browser update entry point: freeze, save, then acknowledge durable storage.</summary>
        public void BeginUpdate()
        {
            if (updatingForReload) return;
            updatingForReload = true;
            SetWorldActive(false);
            if (coop != null)
                SendCoop(JsonUtility.ToJson(new CoopEvent { k = CoopProtocol.Update }));
            SaveSystem.SaveCharacterNow();
            var areas = AreaManager.Instance;
            string json = "";
            if (Role == SessionRole.Player && PlayGranted && GameplayReady &&
                SaveSystem.CharacterLoaded && scenePlayer != null && areas != null)
            {
                json = JsonUtility.ToJson(new UpdateResume
                {
                    profile = SaveSystem.ActiveProfileId, area = areas.CurrentAreaIndex,
                    position = scenePlayer.transform.position,
                    facing = scenePlayer.transform.eulerAngles.y, hosting = hosting,
                    party = coop?.PartyId, host = coop != null && coop.IsHost,
                    partner = coop?.PartnerName,
                    drops = LootDrop.CaptureForUpdate(), populations = CaptureUpdatePopulations()
                });
            }
            coop?.SetLinked(false);
#if UNITY_WEBGL && !UNITY_EDITOR
            UpdateBridge_SaveResume(json);
#endif
        }

        public void RequestPartnerUpdate()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            UpdateBridge_RequestUpdate();
#endif
        }

        /// <summary>Consumes a tab-local, one-use update handoff before showing any menus.</summary>
        public void ResumeAfterUpdate(string json)
        {
            if (PlayGranted || Role != SessionRole.Player) return;
            UpdateResume resume = null;
            try { if (!string.IsNullOrEmpty(json)) resume = JsonUtility.FromJson<UpdateResume>(json); }
            catch (Exception e) { Debug.LogWarning("Update resume was unreadable: " + e.Message); }
            if (resume != null && resume.version == 1 &&
                SaveSystem.Profiles().Exists(p => p.id == resume.profile))
            {
                SaveSystem.SelectProfile(resume.profile);
                notifyAfterUpdate = true;
                updatePopulations = resume.populations;
                updateDrops = resume.drops;
                // A removed area or invalid position falls back to the normal saved destination.
                if (resume.area >= 0 && resume.area < WorldBuilder.AreaNames.Length &&
                    float.IsFinite(resume.position.x) && float.IsFinite(resume.position.y) &&
                    float.IsFinite(resume.position.z) && float.IsFinite(resume.facing))
                    updateResume = resume;
                PlayerName = SaveSystem.ActiveCharacterName;
                hosting = resume.hosting;
                if (!string.IsNullOrEmpty(resume.party))
                {
                    coop = gameObject.AddComponent<CoopSession>();
                    coop.Begin(resume.host, resume.partner, resume.party, stateBroadcaster, replica, preservePosition: true);
                    coop.SetLinked(false);
                }
                StartGame(coopGame: hosting || coop != null);
                return;
            }
            if (Application.isEditor && TryDevStart()) return;
            ShowStartMenu();
        }

        // The dev build plays the first character alone at Greenwood's arrival, which packs keep clear.
        // DevTest sessions (DevQuery set) pick their temporary character through the menus instead.
        private bool TryDevStart()
        {
            var first = SaveSystem.Profiles().Find(p => p != null);
            if (devStartUsed || first == null || PlayerPrefs.HasKey("PoeClone.DevQuery")) return false;
            devStartUsed = true;
            SaveSystem.SelectProfile(first.id);
            PlayerName = SaveSystem.ActiveCharacterName;
            StartAreaOverride = WorldBuilder.Greenwood;
            StartGame(coopGame: false);
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
#if UNITY_EDITOR
            if (UnityEditor.SessionState.GetBool("PoeClone.LootSimulator.Active", false)) return;
#endif
            if (MinimalCombatMode.Enabled) return;
            if (Instance != null) return;
            var go = new GameObject("GameSessionController");
            go.AddComponent<GameSessionController>();
            SceneManager.sceneLoaded += SuspendStartupScene;
        }

        private static void SuspendStartupScene(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= SuspendStartupScene;
            Instance?.SuspendScene(scene);
        }

        private void SuspendScene(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root == gameObject || !root.activeSelf || root.GetComponent<UnityEngine.EventSystems.EventSystem>() != null) continue;
                suspendedRoots.Add(root);
                root.SetActive(false);
            }
        }

        private void PrepareGameplay()
        {
            IEnumerator prepare = PrepareGameplayRoutine();
            while (prepare.MoveNext()) { }
        }

        // A step per frame, so the loading gate keeps animating while the world is built.
        private IEnumerator PrepareGameplayRoutine()
        {
            if (gameplayCreated) yield break;
            gameplayCreated = true;
            if (menuAudioListener != null) menuAudioListener.enabled = false;
            foreach (GameObject root in suspendedRoots)
                if (root != null) root.SetActive(true);
            suspendedRoots.Clear();
            // Restoring scene roots must not start Greenwood's packs before its first visit.
            foreach (var spawner in FindObjectsByType<PoeClone.Enemies.EnemySpawner>())
                spawner.enabled = false;
            var player = FindAnyObjectByType<PlayerStats>();
            scenePlayer = player != null ? player.gameObject : null;
            yield return null;

            IEnumerator build = WorldBuilder.BuildRoutine(updateResume != null ? updateResume.area : SaveSystem.StartingArea);
            while (build.MoveNext())
                yield return null;

            PlaceDevStart();
            // A fresh streamed area initially prepares its spawn. An update can resume
            // several chunks away: finish that destination's collision before loading
            // the character can unpause movement and gravity.
            if (updateResume != null && WorldBuilder.Instance != null)
            {
                PoeClone.UI.LoadingStatus.Set("Restoring update location");
                yield return WorldBuilder.Instance.EnsureArea(updateResume.area, focus: updateResume.position);
                Physics.SyncTransforms();
            }

            PoeClone.UI.LoadingStatus.Set("Game interface");
            yield return null;
            IEnumerator systems = CreateGameplaySystems();
            while (systems.MoveNext())
                yield return null;

            // Prepare the actual starting destination, without loading an unrelated hub.
            if (WorldBuilder.Instance != null && AreaManager.Instance != null)
            {
                PoeClone.UI.LoadingStatus.Set("Minimap");
                yield return null;
                IEnumerator map = PoeClone.UI.MinimapTerrain.Prepare(AreaManager.Instance.CurrentAreaIndex);
                while (map.MoveNext())
                    yield return null;
            }
            PoeClone.UI.LoadingStatus.Clear();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            menuAudioListener = gameObject.AddComponent<AudioListener>();
            gameObject.AddComponent<PoeClone.Audio.MusicPlayer>().Play("Dark Quest");

            // Pause while choosing and loading the local character.
            SetWorldActive(false);

            var clientGO = new GameObject("NetworkClient");
            clientGO.transform.SetParent(transform, false);
            client = clientGO.AddComponent<WebSocketClient>();
            client.OnOpen += HandleOpen;
            client.OnMessage += HandleMessage;
            client.OnClose += HandleClose;
            client.OnError += HandleError;
            client.OnPageClosing += SendCoopLeave;

            gameObject.AddComponent<SessionGateUI>();
            gameObject.AddComponent<PoeClone.Player.SaveSystem>();
            namePrompt = gameObject.AddComponent<NamePromptUI>();
            gameObject.AddComponent<PoeClone.UI.PatchNotesUI>();
            gameObject.AddComponent<PoeClone.UI.EscapeMenuUI>();

            stateBroadcaster = gameObject.AddComponent<PlayerStateBroadcaster>();
            stateBroadcaster.enabled = false;
            replica = gameObject.AddComponent<SpectatorReplica>();
        }

        private IEnumerator CreateGameplaySystems()
        {
            gameObject.AddComponent<SpectatorView>();
            gameObject.AddComponent<ChatUI>();
            gameObject.AddComponent<PoeClone.UI.TouchControlsUI>();
            gameObject.AddComponent<PoeClone.UI.SkillBarUI>();
            yield return null;
            gameObject.AddComponent<PoeClone.UI.DialogueUI>();
            gameObject.AddComponent<PoeClone.UI.MinimapUI>();
            gameObject.AddComponent<PoeClone.UI.QuestTrackerUI>();
            gameObject.AddComponent<PoeClone.UI.BossBarUI>();
            yield return null;
            gameObject.AddComponent<PoeClone.UI.PassiveTreeUI>();
            gameObject.AddComponent<PoeClone.UI.WeaponSkillTreeUI>();
            gameObject.AddComponent<PoeClone.UI.CoopPartnerUI>();
        }

        private IEnumerator Start()
        {
            yield return null;

            client.RequestLocationSearch(search =>
            {
                Role = ParseRole(search);
                // ?touch=1 forces the phone controls (testing them in a desktop browser).
                if (HasFlag(search, "touch"))
                    TouchMode.SetForced(true);
                string overrideUrl = ParseServerOverride(search);
                if (!string.IsNullOrEmpty(overrideUrl))
                    serverUrl = overrideUrl;

                if (Role == SessionRole.Spectator)
                {
                    PrepareGameplay();
                    if (scenePlayer != null) scenePlayer.SetActive(true);
                    // Spectators run the world (so puppets animate and sounds play) but with every
                    // gameplay system switched off by the replica.
                    replica.Enter();
                    SetWorldActive(true);
                }
                StateChanged?.Invoke();

                string confirmLabel = Role == SessionRole.Spectator ? "Watch" : "Play";
                if (Role == SessionRole.Player)
                {
                    PoeClone.Player.SaveSystem.Profiles();
#if UNITY_WEBGL && !UNITY_EDITOR
                    UpdateBridge_ReadResume();
#else
                    ResumeAfterUpdate("");
#endif
                    return;
                }
                namePrompt.Show(PlayerPrefs.GetString(NamePrefKey, string.Empty), confirmLabel, name =>
                {
                    PlayerName = name;
                    PlayerPrefs.SetString(NamePrefKey, name);
                    PlayerPrefs.Save();
                    StartCoroutine(ConnectToServer());
                });
            });
        }

        // ------------------------------------------------------------------ start menu and co-op

        private void ShowStartMenu()
        {
            string notice = startNotice;
            startNotice = null;
            coopIntent = CoopIntent.None;
            namePrompt.ShowStartMenu(() => ChooseCharacter(false), () => ChooseCharacter(true), notice);
        }

        private void ShowCoopMenu()
        {
            LeaveCoopLobby();
            namePrompt.ShowCoopMenu(HostGame, EnterCoopLobby, () => ChooseCharacter(true));
        }

        private void ChooseCharacter(bool coopGame)
        {
            LeaveCoopLobby();
            namePrompt.ShowCharacters(() =>
            {
                PlayerName = SaveSystem.ActiveCharacterName;
                if (coopGame)
                    ShowCoopMenu();
                else
                    StartGame(coopGame: false);
            }, ShowStartMenu);
        }

        private void HostGame()
        {
            hosting = true;
            StartGame(coopGame: true);
            if (Connected && serverPlayGranted)
                SendHost();
        }

        private void StartGame(bool coopGame)
        {
            // Monsters already spawned behind the menus take on the new life scale too.
            float life = Party.MonsterLifeScale;
            Party.CoopGame = coopGame;
            EnemyHealth.RescaleLife(Party.MonsterLifeScale / life);

            namePrompt.HideScreens();
            PlayGranted = true;
            StateChanged?.Invoke();
            StartCoroutine(LoadChosenCharacter());
            if (!Connected)
                StartCoroutine(ConnectToServer());
        }

        // Joining needs the server: connect first if the background connection isn't up yet.
        private void EnterCoopLobby()
        {
            coopIntent = CoopIntent.Join;
            namePrompt.ShowCoopStatus("Connecting...", null, ShowCoopMenu);
            if (Connected && serverPlayGranted)
                RequestLobby();
            else if (!Connected && reconnectRoutine == null)
                StartCoroutine(ConnectToServer());
        }

        private void LeaveCoopLobby()
        {
            if (coopIntent != CoopIntent.None && Connected && coop == null)
                client.Send(JsonUtility.ToJson(new CoopLeaveMessage()));
            coopIntent = CoopIntent.None;
        }

        private void RequestLobby()
        {
            client.Send(JsonUtility.ToJson(new CoopListMessage()));
            ShowLobby();
        }

        // Lists this game for others to join (again after a reconnect: the server forgets it).
        private void SendHost()
        {
            client.Send(JsonUtility.ToJson(new CoopHostMessage { name = PlayerName }));
        }

        private void SystemChat(string text)
        {
            ChatReceived?.Invoke(new ChatEnvelope("", "system", text, 0));
        }

        private void ShowLobby()
        {
            namePrompt.ShowCoopLobby(lobby, JoinGame, ShowCoopMenu);
        }

        private void JoinGame(PlayerInfo host)
        {
            client.Send(JsonUtility.ToJson(new CoopJoinMessage { id = host.id, name = PlayerName }));
            namePrompt.ShowCoopStatus("Joining " + host.name + "'s game...", null, ShowCoopMenu);
        }

        private void HandleCoopMessage(ServerMessage msg)
        {
            switch (msg.state)
            {
                case "hosting":
                    if (!hostAnnounced)
                        SystemChat("Your game is open: others can join it from Co-op > Join Game.");
                    hostAnnounced = true;
                    break;

                case "failed":
                    if (coopIntent == CoopIntent.Join)
                        ShowLobby();
                    break;

                case "started":
                    bool asHost = msg.coopRole == "host";
                    if (asHost && coop != null && !coop.Linked)
                    {
                        // Someone joined while we waited for a dropped partner (usually that partner,
                        // back through the lobby): the old party is over.
                        DestroyImmediate(coop);
                        coop = null;
                    }
                    if (coop != null || (asHost ? !hosting : coopIntent != CoopIntent.Join))
                        return;
                    if (asHost)
                        SystemChat(msg.partnerName + " joined your game.");
                    else
                    {
                        coopIntent = CoopIntent.None;
                        StartGame(coopGame: true);
                    }
                    coop = gameObject.AddComponent<CoopSession>();
                    coop.Begin(asHost, msg.partnerName, msg.party, stateBroadcaster, replica);
                    break;

                case "waiting":
                    // Either the partner dropped, or this game reconnected before they did.
                    if (coop == null)
                        return;
                    if (coop.Linked)
                        SystemChat(coop.PartnerName + " lost connection. Waiting for them to reconnect...");
                    coop.SetLinked(false);
                    break;

                case "resumed":
                    if (coop == null)
                        return;
                    coop.SetLinked(true);
                    stateBroadcaster.RequestGearUpdate();
                    SystemChat("Co-op with " + coop.PartnerName + " resumed.");
                    break;

                case "ended":
                    if (coop == null)
                        return;
                    bool wasHost = coop.IsHost;
                    string partner = coop.PartnerName;
                    // Still unlinked when the party ends: the partner never reconnected.
                    bool lost = !coop.Linked;
                    Destroy(coop);
                    coop = null;
                    if (wasHost)
                    {
                        // The world was ours all along: carry on alone, still open for them to rejoin.
                        SystemChat(partner + (lost ? " didn't reconnect." : " left the game.") + " They can rejoin from Co-op > Join Game.");
                    }
                    else
                    {
                        // Our enemies were the host's: back to the menu, character and progress saved.
                        startNotice = partner + (lost ? " didn't reconnect." : " ended the co-op game.") + " Your character's progress is saved.";
                        ReturnToMainMenu();
                    }
                    break;
            }
        }

        /// <summary>Co-op: one already-serialized snapshot or event for the partner.</summary>
        public void SendCoop(string json)
        {
            if (!Connected || coop == null) return;
            if (client.BufferedAmount > MaxSnapshotBacklogBytes && json.StartsWith(CoopProtocol.SnapshotPrefix, StringComparison.Ordinal))
                return;
            client.Send(json);
        }

        public void SendChat(string text)
        {
            if (!Connected || string.IsNullOrWhiteSpace(text)) return;
            client.Send(JsonUtility.ToJson(new ChatOutMessage { text = text }));
        }

        private IEnumerator LoadChosenCharacter()
        {
            // Give the loading gate a rendered frame before activating scene roots and
            // building the world. Two yields also cover starts from coroutines.
            PoeClone.UI.LoadingStatus.Set("Scene objects");
            yield return null;
            yield return null;
            yield return PrepareGameplayRoutine();

            // Allow the reactivated scene's Start methods to bind stats, gear and skills
            // before applying the character's saved values and bindings.
            PoeClone.UI.LoadingStatus.Set("Character save");
            yield return null;
            GameplayReady = true;
            SaveSystem.LoadSelectedProfile();
            PoeClone.UI.LoadingStatus.Clear();
        }

        /// <summary>
        /// Spectators: switch to the next (+1) or previous (-1) player in the list, wrapping round.
        /// Shown at once locally; the server confirms with a status and the new player's latest snapshot.
        /// </summary>
        public void WatchNext(int step)
        {
            if (!Connected || Role != SessionRole.Spectator || players.Length < 2) return;

            int index = Array.FindIndex(players, p => p.id == WatchingId);
            if (index < 0) index = 0;
            int next = ((index + step) % players.Length + players.Length) % players.Length;
            if (players[next].id == WatchingId) return;

            WatchingId = players[next].id;
            replica.ResetReplica();
            client.Send(JsonUtility.ToJson(new WatchMessage { id = WatchingId }));
            StateChanged?.Invoke();
        }

        /// <summary>Name of the player being watched, or null.</summary>
        public string WatchingName
        {
            get
            {
                foreach (PlayerInfo p in players)
                {
                    if (p.id == WatchingId)
                        return p.name;
                }
                return null;
            }
        }

        /// <summary>Sends one already-serialized gameplay snapshot (see PlayerStateBroadcaster).</summary>
        public void SendState(string json)
        {
            if (!Connected || Role != SessionRole.Player || !serverPlayGranted) return;
            if (client.BufferedAmount > MaxSnapshotBacklogBytes) return;
            client.Send(json);
        }

        /// <summary>Sends the player's menus' contents (see PlayerStateBroadcaster / GearState).</summary>
        public void SendGear(string json)
        {
            if (!Connected || Role != SessionRole.Player || !serverPlayGranted) return;
            client.Send(json);
        }

        private void HandleOpen()
        {
            Connected = true;
            reconnectDelay = 2f;
            DenyReason = null;
            DisconnectReason = null;
            Reconnecting = false;

            var hello = new HelloMessage
            {
                role = Role == SessionRole.Spectator ? "spectator" : "player",
                name = PlayerName,
            };
            client.Send(JsonUtility.ToJson(hello));

            StateChanged?.Invoke();
        }

        private void HandleMessage(string json)
        {
            if (updatingForReload) return;
            // Fast path for the 10Hz gameplay stream: skip the generic parse below, the replica
            // parses it into its own type. The server always writes "type" first.
            if (json != null && json.StartsWith(StatePrefix, StringComparison.Ordinal))
            {
                if (Role == SessionRole.Spectator)
                    replica.HandleState(json);
                return;
            }

            if (json != null && json.StartsWith(GearPrefix, StringComparison.Ordinal))
            {
                if (Role == SessionRole.Spectator)
                    replica.HandleGear(json);
                else
                    coop?.HandleGear(json);
                return;
            }

            if (json != null && json.StartsWith(CoopProtocol.SnapshotPrefix, StringComparison.Ordinal))
            {
                coop?.HandleSnapshot(json);
                return;
            }

            if (json != null && json.StartsWith(CoopProtocol.EventPrefix, StringComparison.Ordinal))
            {
                coop?.HandleEvent(json);
                return;
            }

            ServerMessage msg;
            try
            {
                msg = JsonUtility.FromJson<ServerMessage>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"GameSessionController: malformed message ignored: {e.Message}");
                return;
            }

            if (msg == null || string.IsNullOrEmpty(msg.type)) return;

            switch (msg.type)
            {
                case "welcome":
                    serverPlayGranted = msg.granted;
                    DenyReason = msg.reason;
                    if (Role == SessionRole.Player)
                    {
                        // Server permission controls spectator streaming only.
                        stateBroadcaster.enabled = msg.granted;
                    }
                    if (coopIntent == CoopIntent.Join)
                    {
                        if (msg.granted)
                            RequestLobby();
                        else
                            namePrompt.ShowCoopStatus("Can't start co-op", msg.reason, ShowCoopMenu);
                    }
                    if (coop != null)
                    {
                        // Back after a dropped connection: pick the party up again.
                        if (msg.granted)
                            client.Send(JsonUtility.ToJson(new CoopResumeMessage
                            {
                                party = coop.PartyId,
                                role = coop.IsHost ? "host" : "guest",
                                name = PlayerName,
                            }));
                    }
                    else if (hosting)
                    {
                        if (msg.granted)
                            SendHost();
                        else
                            SystemChat("Your game can't be listed for co-op: " + msg.reason);
                    }
                    StateChanged?.Invoke();
                    break;

                case "lobby":
                    lobby = msg.hosts ?? new PlayerInfo[0];
                    if (coopIntent == CoopIntent.Join)
                        ShowLobby();
                    break;

                case "coop":
                    HandleCoopMessage(msg);
                    break;

                case "status":
                    if (!msg.playerActive && Role == SessionRole.Spectator)
                        replica.ResetReplica();
                    else if (Role == SessionRole.Spectator && WatchingId != 0 && WatchingId != msg.watching)
                        replica.ResetReplica();
                    RemotePlayerActive = msg.playerActive;
                    SpectatorCount = msg.spectatorCount;
                    MaxPlayers = msg.maxPlayers;
                    players = msg.players ?? new PlayerInfo[0];
                    WatchingId = msg.watching;
                    StateChanged?.Invoke();
                    break;

                case "chat":
                    ChatReceived?.Invoke(new ChatEnvelope(msg.from, msg.role, msg.text, msg.ts));
                    break;
            }
        }

        private void HandleClose(string reason)
        {
            if (returningToMainMenu) return;
            if (!string.IsNullOrWhiteSpace(reason)) DisconnectReason = reason;
            if (string.IsNullOrWhiteSpace(DisconnectReason)) DisconnectReason = "Connection closed unexpectedly.";
            if (coopIntent == CoopIntent.Join)
                namePrompt.ShowCoopStatus("Can't reach the co-op server", "Retrying... You can go back and play alone.", ShowCoopMenu);
            if (coop != null)
            {
                // Both games carry on; once reconnected, the server pairs them up again (or ends
                // the party if the partner doesn't come back in time).
                coop.SetLinked(false);
                SystemChat("Co-op connection lost. Reconnecting...");
            }
            Reconnecting = true;
            Connected = false;
            serverPlayGranted = false;
            DenyReason = null;
            RemotePlayerActive = false;
            players = new PlayerInfo[0];
            WatchingId = 0;
            stateBroadcaster.enabled = false;
            if (Role == SessionRole.Spectator)
                replica.ResetReplica(); // the server resends the latest snapshot on reconnect
            StateChanged?.Invoke();

            // Always reschedule: ScheduleReconnect() itself stops any previous pending attempt
            // first, so this is safe to call after every close, including repeated failed retries
            // (a Coroutine reference doesn't become null just because it already finished).
            ScheduleReconnect();
        }

        private void HandleError(string message)
        {
            Debug.LogWarning($"GameSessionController: socket error: {message}");
            if (!string.IsNullOrWhiteSpace(message) && message != "WebSocket error")
                DisconnectReason = message;
        }

        public void NotifyCharacterLoaded()
        {
            if (Role == SessionRole.Player && PlayGranted)
            {
                RestoreUpdateLocation();
                LootDrop.RestoreAfterUpdate(updateDrops);
                updateDrops = null;
                if (scenePlayer != null) scenePlayer.SetActive(true);
                if (notifyAfterUpdate)
                {
                    notifyAfterUpdate = false;
                    GetComponent<PoeClone.UI.PatchNotesUI>()?.AcknowledgeCurrentRelease();
#if UNITY_WEBGL && !UNITY_EDITOR
                    UpdateBridge_Completed();
#endif
                }
                SetWorldActive(true);
            }
            StateChanged?.Invoke();
        }

        // Greenwood's own spawn sits among its packs: the dev start arrives through Haven's gate, as a fresh visit does.
        private void PlaceDevStart()
        {
            if (StartAreaOverride < 0 || updateResume != null || WorldBuilder.Instance == null) return;
            int area = StartAreaOverride;
            StartAreaOverride = -1;
            Transform arrival = WorldBuilder.Instance.GateArrival(area, WorldBuilder.Haven);
            if (arrival != null)
                updateResume = new UpdateResume { area = area, position = arrival.position, facing = arrival.eulerAngles.y };
        }

        private void RestoreUpdateLocation()
        {
            if (updateResume == null || scenePlayer == null) return;
            var resume = updateResume;
            updateResume = null;
            var areas = AreaManager.Instance;
            if (areas == null || areas.CurrentAreaIndex != resume.area) return;
            var player = scenePlayer.transform;
            var body = scenePlayer.GetComponent<CharacterController>();
            scenePlayer.GetComponent<PlayerController>()?.StopMovement();
            if (body != null) body.enabled = false;
            player.SetPositionAndRotation(resume.position, Quaternion.Euler(0f, resume.facing, 0f));
            Physics.SyncTransforms();
            // New scenery can occupy the old location. Use the fresh area's spawn in that case.
            if (body != null)
            {
                if (WorldBuilder.UsesChunks(resume.area))
                {
                    float groundY = float.NegativeInfinity;
                    foreach (var hit in Physics.RaycastAll(player.position + Vector3.up * 4f, Vector3.down, 12f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                        if (hit.collider.name.StartsWith("Ground_", StringComparison.Ordinal) &&
                            hit.normal.y >= Mathf.Cos(body.slopeLimit * Mathf.Deg2Rad))
                            groundY = Mathf.Max(groundY, hit.point.y);
                    if (float.IsFinite(groundY))
                    {
                        float footOffset = player.position.y - player.TransformPoint(body.center).y +
                            body.height * Mathf.Abs(player.lossyScale.y) * 0.5f;
                        Vector3 grounded = player.position;
                        grounded.y = groundY + footOffset + body.skinWidth;
                        player.position = grounded;
                    }
                    else if (areas.Current?.spawnPoint != null)
                        player.SetPositionAndRotation(areas.Current.spawnPoint.position, areas.Current.spawnPoint.rotation);
                    Physics.SyncTransforms();
                }
                Vector3 center = player.TransformPoint(body.center);
                float radius = body.radius * Mathf.Max(Mathf.Abs(player.lossyScale.x), Mathf.Abs(player.lossyScale.z));
                float height = Mathf.Max(radius * 2f, body.height * Mathf.Abs(player.lossyScale.y));
                float feet = center.y - height * 0.5f;
                Vector3 axis = Vector3.up * (height * 0.5f - radius);
                foreach (Collider obstacle in Physics.OverlapCapsule(center - axis, center + axis, radius,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    if (obstacle.transform.IsChildOf(player) || obstacle is CharacterController ||
                        obstacle.GetComponentInParent<EnemyHealth>() != null || obstacle.bounds.max.y <= feet + 0.15f)
                        continue;
                    Transform spawn = areas.Current?.spawnPoint;
                    if (spawn != null) player.SetPositionAndRotation(spawn.position, spawn.rotation);
                    break;
                }
                body.enabled = true;
            }
            scenePlayer.GetComponentInChildren<PoeClone.Visuals.CharacterWalkAnimator>()?.ResetAnimatorState();
            scenePlayer.GetComponent<PlayerStats>()?.ProtectArrival();
            AreaGate.DisarmAtArrival(resume.area, player.position);
            UnityEngine.Camera.main?.GetComponent<PoeClone.CameraSystem.CameraFollow>()?.SnapToTarget();
        }

        private IEnumerator ConnectToServer()
        {
            if (string.IsNullOrEmpty(serverUrl))
                yield return NetworkConfig.Load(cfg => serverUrl = cfg.serverUrl);
            if (!returningToMainMenu && !string.IsNullOrEmpty(serverUrl))
                client.Connect(serverUrl);
            else if (coopIntent == CoopIntent.Join)
                namePrompt.ShowCoopStatus("Co-op isn't available", "No server is configured for this build.", ShowCoopMenu);
            else if (hosting)
                SystemChat("Co-op isn't available: no server is configured for this build.");
        }

        private void ScheduleReconnect()
        {
            if (reconnectRoutine != null) StopCoroutine(reconnectRoutine);
            reconnectRoutine = StartCoroutine(ReconnectAfterDelay());
        }

        private void OnApplicationFocus(bool focused)
        {
            // A background tab may have suspended its reconnect timer. Resume promptly when the
            // browser gives the game focus again; a healthy socket needs no new connection.
            if (focused && !Connected && reconnectRoutine != null && !returningToMainMenu && !string.IsNullOrEmpty(serverUrl) &&
                !string.IsNullOrEmpty(PlayerName))
            {
                reconnectDelay = 2f;
                ScheduleReconnect();
            }
        }

        // Leaving on purpose (back to the menu, closing the tab or game): end the party now
        // rather than have the partner wait for a reconnect that can't come.
        private void SendCoopLeave()
        {
            if (coop != null && Connected && !updatingForReload)
                client.Send(JsonUtility.ToJson(new CoopLeaveMessage()));
        }

        public void ReturnToMainMenu()
        {
            if (Role != SessionRole.Player || returningToMainMenu) return;
            SaveSystem.SaveBeforeCharacterSwitch();
            returningToMainMenu = true;
            SendCoopLeave();
            if (reconnectRoutine != null) StopCoroutine(reconnectRoutine);
            stateBroadcaster.enabled = false;
            client.Close();
            SetWorldActive(false);
            SceneManager.sceneLoaded += RestartAfterMainMenu;
            Instance = null;
            Destroy(gameObject);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private static void RestartAfterMainMenu(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= RestartAfterMainMenu;
            var session = new GameObject("GameSessionController").AddComponent<GameSessionController>();
            session.SuspendScene(scene);
        }

        private IEnumerator ReconnectAfterDelay()
        {
            yield return new WaitForSecondsRealtime(reconnectDelay);
            reconnectDelay = Mathf.Min(reconnectDelay * 1.5f, 15f);

            if (!string.IsNullOrEmpty(serverUrl))
                client.Connect(serverUrl);
        }

        // Time.timeScale = 0 freezes every Update()-driven system in the scene (movement, enemy
        // AI, spawners, animators) in one shot; AudioListener.pause silences and stops processing
        // audio the same way. Both are undone once the local character has loaded.
        private void SetWorldActive(bool active)
        {
            worldActive = active;
            RefreshWorldPause();
        }

        public void RefreshWorldPause()
        {
            bool pausedByMenu = GetComponent<PoeClone.UI.EscapeMenuUI>()?.IsOpen == true;
            bool paused = !worldActive || pausedByMenu || updatingForReload ||
                PoeClone.UI.PatchNotesUI.IsShowing;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
        }

        private static SessionRole ParseRole(string queryString)
        {
            foreach (var (key, value) in EnumerateQuery(queryString))
            {
                if (key == "role" && value.StartsWith("spectat", StringComparison.OrdinalIgnoreCase))
                    return SessionRole.Spectator;
            }
            return SessionRole.Player;
        }

        private static bool HasFlag(string queryString, string name)
        {
            foreach (var (key, value) in EnumerateQuery(queryString))
            {
                if (key == name && (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
            return false;
        }

        private static string ParseServerOverride(string queryString)
        {
            foreach (var (key, value) in EnumerateQuery(queryString))
            {
                if (key == "server")
                    return UnityWebRequest.UnEscapeURL(value);
            }
            return null;
        }

        private static System.Collections.Generic.IEnumerable<(string key, string value)> EnumerateQuery(string queryString)
        {
            if (string.IsNullOrEmpty(queryString)) yield break;

            string q = queryString.TrimStart('?');
            foreach (var pair in q.Split('&'))
            {
                int eq = pair.IndexOf('=');
                if (eq <= 0) continue;
                yield return (pair.Substring(0, eq), pair.Substring(eq + 1));
            }
        }
    }
}
