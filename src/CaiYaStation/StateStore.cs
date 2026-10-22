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
            Loaded = true;
            try
            {
                _state = Read<StateData>(StateKey) ?? new StateData();
                _world = Read<WorldData>(WorldKey) ?? new WorldData();
                _receipts = Read<ReceiptArchive>(ReceiptsKey) ?? new ReceiptArchive();
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
                     + " pending=" + (_state.Pending == null ? "none" : _state.Pending.Stage.ToString()));
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

        internal void SetPending(ShipmentJournal journal)
        {
            _state.Pending = journal;
            SaveState();
        }

        internal void SavePending()
        {
            _state.Pending = Pending;
            SaveState();
        }

        internal void ClearPending()
        {
            _state.Pending = null;
            SaveState();
        }

        internal void AddReceipt(Receipt receipt)
        {
            _receipts.Receipts.Add(receipt);
            if (_receipts.Receipts.Count > 100)
                _receipts.Receipts.RemoveRange(0, _receipts.Receipts.Count - 100);
            SaveReceipts();
        }

        internal string NextShipmentId()
        {
            var id = "CYS-" + DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss") + "-" + _state.NextShipmentNumber.ToString("D3");
            _state.NextShipmentNumber++;
            SaveState();
            return id;
        }

        internal void SaveState()
        {
            _state.RaidToken = Session.Token;
            _state.RaidActive = Session.Active;
            _state.StartedUtc = Session.StartedUtc;
            _state.EndedUtc = Session.EndedUtc;
            Write(StateKey, _state);
        }

        internal void SaveWorld() => Write(WorldKey, _world);
        internal void SaveReceipts() => Write(ReceiptsKey, _receipts);

        internal void SaveAllAndFlush()
        {
            SaveState();
            SaveWorld();
            SaveReceipts();
            try
            {
                Saves.SavesSystem.SaveFile(false);
            }
            catch (Exception exception)
            {
                WarnOnce("SaveFile failed: " + exception.Message);
            }
        }

        private T? Read<T>(string key) where T : class
        {
            try
            {
                if (!Saves.SavesSystem.KeyExisits(key)) return null;
                var json = Saves.SavesSystem.Load<string>(key);
                if (string.IsNullOrWhiteSpace(json)) return null;
                return JsonConvert.DeserializeObject<T>(json, JsonSettings);
            }
            catch (Exception exception)
            {
                _log("Read " + key + " failed: " + exception.Message);
                return null;
            }
        }

        private void Write<T>(string key, T value)
        {
            try
            {
                var json = JsonConvert.SerializeObject(value, JsonSettings);
                Saves.SavesSystem.Save(key, json);
            }
            catch (Exception exception)
            {
                WarnOnce("Write " + key + " failed: " + exception.Message);
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
