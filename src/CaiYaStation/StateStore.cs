using System;
using System.Collections.Generic;
using System.Linq;
using CaiYaStation.Domain;
using Newtonsoft.Json;

namespace CaiYaStation
{
    internal sealed class StateData
    {
        public int Version { get; set; } = 1;
        public string RaidToken { get; set; } = "";
        public bool RaidActive { get; set; }
        public string StartedUtc { get; set; } = "";
        public string EndedUtc { get; set; } = "";
        public int NextShipmentNumber { get; set; } = 1;
        public ShipmentJournal? Pending { get; set; }
    }

    internal sealed class WorldData
    {
        public int Version { get; set; } = 1;
        public string RaidToken { get; set; } = "";
        public List<StationRecord> Stations { get; set; } = new List<StationRecord>();
    }

    internal sealed class ReceiptArchive
    {
        public int Version { get; set; } = 1;
        public List<Receipt> Receipts { get; set; } = new List<Receipt>();
    }

    /// <summary>
    /// Three namespaced save keys, never touching DuckovSharedBike.*. Writes are best effort:
    /// a failed save is logged and retried on the next shipment or scene change, and the
    /// in-memory state remains authoritative for the current session.
    /// </summary>
    internal sealed class StateStore
    {
        internal const string StateKey = "CaiYaStation.State.v1";
        internal const string WorldKey = "CaiYaStation.World.v1";
        internal const string ReceiptsKey = "CaiYaStation.Receipts.v1";

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore
        };

        private readonly Action<string> _log;
        private StateData _state = new StateData();
        private WorldData _world = new WorldData();
        private ReceiptArchive _receipts = new ReceiptArchive();
        private bool _loadFailed;
        private string _lastError = "";
        /// <summary>Keys whose persisted payload failed to load; writes to them are blocked
        /// so a corrupted-but-recoverable original is never overwritten by empty data.</summary>
        private readonly HashSet<string> _corruptKeys = new HashSet<string>(StringComparer.Ordinal);

        internal StateStore(Action<string> log)
        {
            _log = log;
            Session = new RaidSession();
        }

        internal RaidSession Session { get; private set; }
        internal ShipmentJournal? Pending => _state.Pending;
        internal IReadOnlyList<Receipt> Receipts => _receipts.Receipts;
        internal bool HasRaid => !string.IsNullOrEmpty(Session.Token);
        internal bool RaidActive => _state.RaidActive;
        internal bool Loaded { get; private set; }

        internal void Load()
        {
            _loadFailed = false;
            _corruptKeys.Clear();
            Loaded = true;
            try
            {
                // Read distinguishes "key absent" (fresh save, null) from "read/deserialize
                // threw" (corrupt payload). Corrupt state must not be silently replaced by a
                // new object: it would erase a pending recovery or the receipt history and
                // the next save would overwrite the original evidence.
                var state = ReadCorruptAware<StateData>(StateKey, out var stateCorrupt);
                var world = ReadCorruptAware<WorldData>(WorldKey, out var worldCorrupt);
                var receipts = ReadCorruptAware<ReceiptArchive>(ReceiptsKey, out var receiptsCorrupt);
                if (stateCorrupt || worldCorrupt || receiptsCorrupt)
                {
                    _loadFailed = true;
                    foreach (var pair in new[] { (StateKey, stateCorrupt), (WorldKey, worldCorrupt), (ReceiptsKey, receiptsCorrupt) })
                        if (pair.Item2) _corruptKeys.Add(pair.Item1);
                    _log("State load FAILED for keys: " + string.Join(", ", _corruptKeys)
                         + "; writes to these keys are blocked until the save is fixed manually.");
                }
                _state = state ?? new StateData();
                _world = world ?? new WorldData();
                _receipts = receipts ?? new ReceiptArchive();
                _receipts.Receipts ??= new List<Receipt>();
                _world.Stations ??= new List<StationRecord>();
                _world.Stations = _world.Stations.Where(station => station != null).ToList();
                if (_state.Pending != null)
                {
                    _state.Pending.Items ??= new List<JournalItem>();
                    _state.Pending.Items = _state.Pending.Items.Where(item => item != null).ToList();
                }
                if (_state.NextShipmentNumber < 1) _state.NextShipmentNumber = 1;

                var token = !string.IsNullOrEmpty(_world.RaidToken) ? _world.RaidToken : _state.RaidToken;
                Session = new RaidSession
                {
                    Token = token,
                    Active = _state.RaidActive,
                    StartedUtc = _state.StartedUtc,
                    EndedUtc = _state.EndedUtc,
                    Stations = _world.Stations.ToList()
                };
                _log("State loaded token=" + (token.Length == 0 ? "none" : token)
                     + " stations=" + Session.Stations.Count + " receipts=" + _receipts.Receipts.Count
                     + " pending=" + (_state.Pending == null ? "none" : _state.Pending.Stage.ToString())
                     + " corrupt=" + (_corruptKeys.Count == 0 ? "none" : string.Join(",", _corruptKeys)));
            }
            catch (Exception exception)
            {
                _loadFailed = true;
                _log("State load failed; starting a clean session: " + exception.Message);
                _state = new StateData();
                _world = new WorldData();
                _receipts = new ReceiptArchive();
                Session = new RaidSession();
            }
        }

        internal void BeginRaid(DateTimeOffset now)
        {
            Session = new RaidSession
            {
                Token = "raid-" + now.UtcDateTime.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6),
                Active = true,
                StartedUtc = now.ToUniversalTime().ToString("O")
            };
            _state.RaidToken = Session.Token;
            _state.RaidActive = true;
            _state.StartedUtc = Session.StartedUtc;
            _state.EndedUtc = "";
            _world.RaidToken = Session.Token;
            _world.Stations = new List<StationRecord>();
            SaveState();
            SaveWorld();
            _log("New raid token=" + Session.Token);
        }

        internal void EndRaid(DateTimeOffset now)
        {
            if (!RaidActive) return;
            Session.Active = false;
            _state.RaidActive = false;
            _state.EndedUtc = now.ToUniversalTime().ToString("O");
            Session.EndedUtc = _state.EndedUtc;
            SaveState();
            _log("Raid ended token=" + Session.Token);
        }

        internal void SaveStations()
        {
            _world.RaidToken = Session.Token;
            _world.Stations = Session.Stations.ToList();
            SaveWorld();
        }

        /// <summary>Writes the pending write-ahead journal. False means the record is NOT on
        /// disk and the shipment must not charge.</summary>
        internal bool SetPending(ShipmentJournal journal)
        {
            _state.Pending = journal;
            return SaveState();
        }

        internal void SavePending()
        {
            _state.Pending = Pending;
            SaveState();
        }

        internal bool ClearPending()
        {
            _state.Pending = null;
            return SaveState();
        }

        internal bool AddReceipt(Receipt receipt)
        {
            // Receipts are handed over at the bunker, not at the station: keep it pending
            // until the player actually reads it there.
            receipt.Viewed = false;
            _receipts.Receipts.Add(receipt);
            if (_receipts.Receipts.Count > 100)
                _receipts.Receipts.RemoveRange(0, _receipts.Receipts.Count - 100);
            return SaveReceipts();
        }

        /// <summary>Oldest receipt the player has not read at the bunker yet, or null.</summary>
        internal Receipt? FirstUnreadReceipt()
        {
            foreach (var receipt in _receipts.Receipts)
                if (receipt.AwaitingView) return receipt;
            return null;
        }

        internal bool MarkReceiptViewed(Receipt receipt)
        {
            receipt.Viewed = true;
            return SaveReceipts();
        }

        internal string NextShipmentId()
        {
            var id = "CYS-" + DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss") + "-" + _state.NextShipmentNumber.ToString("D3");
            _state.NextShipmentNumber++;
            SaveState();
            return id;
        }

        internal bool SaveState()
        {
            _state.RaidToken = Session.Token;
            _state.RaidActive = Session.Active;
            _state.StartedUtc = Session.StartedUtc;
            _state.EndedUtc = Session.EndedUtc;
            return Write(StateKey, _state);
        }

        internal bool SaveWorld()
        {
            _world.RaidToken = Session.Token;
            _world.Stations = Session.Stations.ToList();
            return Write(WorldKey, _world);
        }

        private bool SaveReceipts() => Write(ReceiptsKey, _receipts);

        /// <summary>
        /// Persists every key and flushes the save file. Returns true only when all four
        /// writes succeeded; shipment code treats false as "not durable" and must abort.
        /// </summary>
        internal bool SaveAllAndFlush()
        {
            var ok = SaveState() & SaveWorld() & SaveReceipts();
            try
            {
                Saves.SavesSystem.SaveFile(false);
            }
            catch (Exception exception)
            {
                WarnOnce("SaveFile failed: " + exception.Message);
                ok = false;
            }
            return ok;
        }

        /// <summary>
        /// Load that separates "key absent" (fresh save: null, not corrupt) from a read or
        /// deserialize failure (corrupt: null plus corrupt=true, and the key is write-blocked).
        /// </summary>
        private T? ReadCorruptAware<T>(string key, out bool corrupt) where T : class
        {
            corrupt = false;
            try
            {
                if (!Saves.SavesSystem.KeyExisits(key)) return null;
                var json = Saves.SavesSystem.Load<string>(key);
                if (string.IsNullOrWhiteSpace(json)) return null;
                var parsed = JsonConvert.DeserializeObject<T>(json, JsonSettings);
                if (parsed == null)
                {
                    corrupt = true;
                    _log("Read " + key + " failed: payload deserialized to null");
                }
                return parsed;
            }
            catch (Exception exception)
            {
                corrupt = true;
                _log("Read " + key + " failed: " + exception.Message);
                return null;
            }
        }

        private T? Read<T>(string key) where T : class => ReadCorruptAware<T>(key, out _);

        private bool Write<T>(string key, T value)
        {
            // Never overwrite a key whose original payload we failed to load: the in-memory
            // replacement is empty and would destroy recoverable data / diagnostic evidence.
            if (_corruptKeys.Contains(key))
            {
                _log("Write " + key + " blocked: key was corrupt at load time");
                return false;
            }
            try
            {
                var json = JsonConvert.SerializeObject(value, JsonSettings);
                Saves.SavesSystem.Save(key, json);
                return true;
            }
            catch (Exception exception)
            {
                WarnOnce("Write " + key + " failed: " + exception.Message);
                return false;
            }
        }

        private void WarnOnce(string message)
        {
            if (string.Equals(_lastError, message, StringComparison.Ordinal)) return;
            _lastError = message;
            _log(message);
        }

        internal string Status =>
            (_loadFailed ? "存档读取失败；" : "")
            + "token=" + (Session.Token.Length == 0 ? "无" : Session.Token)
            + " 站点=" + Session.Stations.Count
            + " 回执=" + _receipts.Receipts.Count
            + " 待恢复=" + (Pending == null ? "无" : Pending.Stage.ToString());
    }
}
