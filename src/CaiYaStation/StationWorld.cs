using System;
using System.Collections.Generic;
using System.Linq;
using CaiYaStation.Domain;
using Duckov.MiniMaps;
using Duckov.Scenes;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Random = System.Random;

namespace CaiYaStation
{
    internal sealed class StationWorld : IDisposable
    {
        private readonly ModConfig _config;
        private readonly StationLayout _layout;
        private readonly ShipmentRuntime _runtime;
        private readonly StateStore _store;
        private readonly Action<string> _log;
        private readonly StationVisuals _visuals;
        private readonly Dictionary<string, GameObject> _stations = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, GameObject> _markers = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, StationView> _views = new Dictionary<string, StationView>();
        private readonly HashSet<string> _announced = new HashSet<string>();
        private readonly Queue<Notice> _notices = new Queue<Notice>();
        private readonly Random _random = new Random(unchecked(Environment.TickCount * 397 ^ Guid.NewGuid().GetHashCode()));
        private Notice? _activeNotice;
        private float _noticeStart;
        private float _noticeUntil;
        private ShipmentPanel? _panel;
        private string _mapId = "";
        private string _sceneName = "";
        private bool _initialized;
        private bool _observedRaid;
        private bool _baseNoticeShown;
        private int _lastReceiptCountNotified = -1;
        private bool _extractionConstraintLogged;
        private bool _placementFailureNotified;
        private float _nextTick;
        private float _nextPlacementRetry;
        private bool _disposed;
        private bool _inputGuardAvailable = true;

        internal enum NoticeKind
        {
            Info,
            Success,
            Warning,
            Danger
        }

        private sealed class Notice
        {
            internal string Title = "菜鸭驿站";
            internal string Body = "";
            internal string Hint = "";
            internal string Tag = "";
            internal NoticeKind Kind = NoticeKind.Info;
            internal float Duration = 4.5f;
        }

        internal StationWorld(ModConfig config, StationLayout layout, ShipmentRuntime runtime, StateStore store, Action<string> log)
        {
            _config = config;
            _layout = layout;
            _runtime = runtime;
            _store = store;
            _log = log;
            _visuals = new StationVisuals(log);
            _lastReceiptCountNotified = store.Receipts.Count;
        }

        internal ModConfig Config => _config;
        internal StateStore Store => _store;
        internal ShipmentRuntime Runtime => _runtime;
        internal StationVisuals Visuals => _visuals;

        internal void AttachPanel(ShipmentPanel panel) => _panel = panel;

        internal void SetInputGuardAvailable(bool available) => _inputGuardAvailable = available;

        internal bool Ready
        {
            get
            {
                try
                {
                    return !_disposed
                           && LevelManager.Instance != null
                           && LevelManager.LevelInited
                           && (MultiSceneCore.Instance == null || !MultiSceneCore.Instance.IsLoading);
                }
                catch { return false; }
            }
        }

        private static CharacterMainControl? Actor => CharacterMainControl.Main;

        private static bool Alive
        {
            get
            {
                try
                {
                    var actor = Actor;
                    return actor != null && actor.Health != null && !actor.Health.IsDead;
                }
                catch { return false; }
            }
        }

        internal void Tick()
        {
            if (_disposed || !_config.Enabled) return;
            try
            {
                if (Time.unscaledTime < _nextTick) return;
                _nextTick = Time.unscaledTime + 0.5f;
                var manager = LevelManager.Instance;
                if (manager == null) return;
                var isBase = manager.IsBaseLevel;
                var isRaid = manager.IsRaidMap;
                if (!_initialized)
                {
                    _initialized = true;
                    _observedRaid = isRaid;
                    if (isRaid && (!_store.HasRaid || !_store.RaidActive))
                    {
                        _store.BeginRaid(DateTimeOffset.Now);
                        _log("Mod activated inside a raid without an active session; started a fresh one");
                    }
                }
                else
                {
                    if (isRaid && !_observedRaid)
                    {
                        _store.BeginRaid(DateTimeOffset.Now);
                        ClearVisuals();
                        _announced.Clear();
                        ClearNotices();
                        _log("Raid start detected, placing new stations");
                    }
                    else if (isBase && _observedRaid)
                    {
                        _store.EndRaid(DateTimeOffset.Now);
                        ClearVisuals();
                        ClearNotices();
                        _panel?.Close();
                    }
                    if (isRaid) _observedRaid = true;
                    else if (isBase) _observedRaid = false;
                }

                if (isBase)
                {
                    _panel?.Close();
                    var hasNewReceipt = _store.Receipts.Count > 0 && _store.Receipts.Count > _lastReceiptCountNotified;
                    if (!_baseNoticeShown)
                    {
                        _baseNoticeShown = true;
                        if (!hasNewReceipt)
                            Notify("马蜂自提点可领取送达货物；零号区暂未开通驿站。", false,
                                "菜鸭驿站 · 营业说明", NoticeKind.Info,
                                "进入仓库区或农场镇后打开地图查看标记", "说明");
                    }
                    if (hasNewReceipt)
                    {
                        _lastReceiptCountNotified = _store.Receipts.Count;
                        Notify("马蜂自提点有菜鸭驿站送达的货物，请查看。", false,
                            "菜鸭驿站 · 货已送达", NoticeKind.Success,
                            "到马蜂自提点领取", "到货");
                    }
                    return;
                }
                if (!isRaid || !Ready || !Alive) return;
                SyncScene();
            }
            catch (Exception exception)
            {
                _log("Station tick failed: " + exception);
                _nextTick = Time.unscaledTime + 2f;
            }
        }

        private void SyncScene()
        {
            Scene scene;
            Scene mainScene;
            var hasMainScene = false;
            string mapId;
            try
            {
                var main = MultiSceneCore.MainScene;
                hasMainScene = main.HasValue;
                mainScene = main.GetValueOrDefault();
                // Gameplay geometry lives in the active sub-scene (Level_Farm_01,
                // Level_HiddenWarehouse). MainScene (Level_Farm_Main) is the persistent
                // system scene; putting world objects or POIs there makes them invisible
                // to the map and often to the camera as well.
                var active = MultiSceneCore.ActiveSubScene;
                scene = active.HasValue
                    ? active.Value
                    : hasMainScene ? mainScene : SceneManager.GetActiveScene();
                mapId = MultiSceneCore.MainSceneID ?? "";
            }
            catch (Exception exception)
            {
                _log("Scene read failed: " + exception.Message);
                return;
            }
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(mapId)) return;
            PruneDestroyedVisuals();
            if (mapId != _mapId || scene.name != _sceneName)
            {
                ClearVisuals();
                ClearNotices();
                _placementFailureNotified = false;
                _mapId = mapId;
                _sceneName = scene.name;
                _log("Station scene map=" + mapId + " scene=" + scene.name
                     + " activeSubScene=" + (MultiSceneCore.ActiveSubSceneID ?? "none"));
            }
            var site = _layout.For(mapId, scene.name) ?? _layout.ForMap(mapId);
            if (site == null) return;

            if (!_store.Loaded)
            {
                _store.Load();
            }
            if (!_store.HasRaid || !_store.RaidActive)
            {
                _store.BeginRaid(DateTimeOffset.Now);
                _announced.Clear();
            }

            var existing = _store.Session.ForMap(mapId).ToList();
            // 0.1.0 stored scene names from MainScene; migrate those records to the active
            // sub-scene so CanOpen/ClearOfStations and the POI scene id agree with the map.
            if (hasMainScene && mainScene.IsValid()
                && !string.Equals(mainScene.name, scene.name, StringComparison.Ordinal))
            {
                var migrated = false;
                foreach (var station in existing)
                {
                    if (string.IsNullOrEmpty(station.SceneName)
                        || string.Equals(station.SceneName, mainScene.name, StringComparison.Ordinal))
                    {
                        station.SceneName = scene.name;
                        migrated = true;
                    }
                }
                if (migrated)
                {
                    _store.SaveStations();
                    _log("Migrated station scene names to " + scene.name);
                }
            }
            var wantedStations = Math.Max(0, site.StationsPerRaid);
            if (existing.Count < wantedStations && Time.unscaledTime >= _nextPlacementRetry)
            {
                _nextPlacementRetry = Time.unscaledTime + 10f;
                var existingIds = new HashSet<string>(existing.Select(station => station.Id), StringComparer.Ordinal);
                var additions = PlanStations(site, scene, existingIds, wantedStations - existing.Count);
                if (additions.Count > 0)
                {
                    _store.Session.AddStations(additions);
                    _store.SaveStations();
                    existing.AddRange(additions);
                    _placementFailureNotified = false;
                    _log("Planned " + additions.Count + " station(s) for " + mapId
                         + " total=" + existing.Count + "/" + wantedStations);
                }
                else if (existing.Count == 0)
                {
                    _log("No station point found for " + mapId + "; retrying in 10s");
                    if (!_placementFailureNotified)
                    {
                        _placementFailureNotified = true;
                        Notify("本区域驿站生成失败，请查看 F8 日志。", false,
                            "菜鸭驿站 · 生成失败", NoticeKind.Danger,
                            "把日志末尾发给作者；10 秒后会重试", "错误");
                    }
                }
                else
                {
                    _log("Station count below target for " + mapId + ": " + existing.Count + "/" + wantedStations
                         + "; retrying in 10s");
                }
            }

            foreach (var station in existing)
            {
                if (!string.Equals(station.SceneName, scene.name, StringComparison.Ordinal)) continue;
                if (!_stations.ContainsKey(station.Id))
                    CreateStationObject(station, scene);
                // If the entity exists but the POI failed (transient scene/UI init), retry it
                // on the next tick instead of leaving the station permanently unmarked.
                if (_stations.ContainsKey(station.Id) && !_markers.ContainsKey(station.Id))
                    AddMarker(station, scene);
            }

            var key = _store.Session.Token + "|" + mapId;
            if (!_announced.Contains(key) && existing.Count > 0)
            {
                _announced.Add(key);
                var availableCount = existing.Count(station => !station.Used);
                var available = availableCount > 0;
                if (available)
                {
                    Notify("地图已标记 · 本局 " + existing.Count + " 个驿站，" + availableCount + " 个可用",
                        false, "菜鸭驿站 · 本区域营业", NoticeKind.Success,
                        "靠近后按交互键寄送 · 每站每局一次", "新区域");
                }
                else
                {
                    Notify("本局驿站已全部停业，下一局再来。", false,
                        "菜鸭驿站 · 本区域停业", NoticeKind.Warning,
                        "每站每局只能发货一次", "停业");
                }
                _log("Announcement shown map=" + mapId + " stations=" + existing.Count + " available=" + availableCount);
            }
        }

        private List<StationRecord> PlanStations(StationMap site, Scene scene,
            ICollection<string>? excludedIds = null, int? wantedCount = null)
        {
            var wanted = wantedCount ?? site.StationsPerRaid;
            if (wanted <= 0) return new List<StationRecord>();
            var excluded = excludedIds ?? Array.Empty<string>();
            var candidates = site.Candidates
                .Where(anchor => !string.IsNullOrWhiteSpace(anchor.Id) && !excluded.Contains(anchor.Id))
                .ToList();
            var domainCandidates = candidates.Select(site.ToCandidate).ToList();
            var chosen = StationPlanning.Choose(domainCandidates, wanted,
                Math.Max(_layout.MinimumSeparation, _config.Placement.MinimumSeparation), _random);
            var placed = new List<StationRecord>();
            var attempted = new HashSet<string>(StringComparer.Ordinal);
            var teleporters = CollectTeleporterPositions();
            var extractions = CollectExtractionPositions();
            foreach (var station in chosen)
            {
                attempted.Add(station.Id);
                if (!TryPlaceStation(station, scene, placed, teleporters, extractions, out var snapped))
                {
                    _log("Station anchor rejected id=" + station.Id);
                    continue;
                }
                placed.Add(snapped);
            }
            foreach (var candidate in domainCandidates)
            {
                if (placed.Count >= wanted) break;
                if (attempted.Contains(candidate.Id)) continue;
                if (!TryPlaceStation(new StationRecord
                    {
                        Id = candidate.Id, Name = candidate.Name, MapId = candidate.MapId, SceneName = candidate.SceneName,
                        X = candidate.X, Y = candidate.Y, Z = candidate.Z, Yaw = candidate.Yaw
                    }, scene, placed, teleporters, extractions, out var snapped))
                {
                    continue;
                }
                placed.Add(snapped);
            }
            return placed;
        }

        private bool TryPlaceStation(StationRecord station, Scene scene, List<StationRecord> alreadyPlaced,
            List<Vector3> teleporters, List<Vector3> extractions, out StationRecord snapped)
        {
            snapped = station;
            var actor = Actor;
            var anchor = new Vector3((float)station.X, (float)station.Y, (float)station.Z);
            if (NavMesh.SamplePosition(anchor, out var nav, 10, NavMesh.AllAreas)) anchor = nav.position;

            var rings = _config.Placement.SearchRings.Count > 0
                ? _config.Placement.SearchRings
                : new List<double> { 8, 12, 16, 22 };
            var angles = Math.Max(4, _config.Placement.SearchAngles);
            var passes = new[] { 0, 1, 2 };
            foreach (var pass in passes)
            {
                foreach (var ring in rings)
                {
                    for (var index = 0; index < angles; index++)
                    {
                        var angle = station.Yaw + index * (360.0 / angles);
                        var offset = Quaternion.Euler(0, (float)angle, 0) * Vector3.forward * (float)ring;
                        var candidate = anchor + offset;
                        if (!SafeGround.Find(anchor, candidate, actor, (float)_config.Placement.MaximumSlopeDegrees, out var point))
                            continue;
                        if (!ClearOfStations(point, station.MapId, station.SceneName, alreadyPlaced)) continue;
                        if (!ClearOfTeleporters(point, teleporters)) continue;
                        if (!ClearOfExtraction(point, pass, extractions)) continue;
                        var rotation = SafeGround.GroundRotation(point, (float)station.Yaw, actor);
                        snapped = new StationRecord
                        {
                            Id = station.Id,
                            Name = station.Name,
                            MapId = station.MapId,
                            SceneName = scene.name,
                            X = point.x,
                            Y = point.y,
                            Z = point.z,
                            Yaw = rotation.eulerAngles.y,
                            Used = false
                        };
                        _log("Station placed id=" + station.Id + " at " + point.x.ToString("0.0") + ","
                             + point.y.ToString("0.0") + "," + point.z.ToString("0.0") + " pass=" + pass);
                        return true;
                    }
                }
                if (pass == 0)
                    _log("No station point satisfied the 40-80m extraction band for id=" + station.Id + "; relaxing");
            }
            return false;
        }

        private bool ClearOfStations(Vector3 point, string mapId, string sceneName, List<StationRecord> placed)
        {
            foreach (var other in _store.Session.ForMap(mapId))
            {
                if (other.SceneName != sceneName) continue;
                if (Vector3.Distance(point, new Vector3((float)other.X, (float)other.Y, (float)other.Z))
                    < _config.Placement.MinimumSeparation) return false;
            }
            foreach (var other in placed)
            {
                if (Vector3.Distance(point, new Vector3((float)other.X, (float)other.Y, (float)other.Z))
                    < _config.Placement.MinimumSeparation) return false;
            }
            return true;
        }

        private bool ClearOfTeleporters(Vector3 point, List<Vector3> teleporters)
        {
            var minimum = (float)_config.Placement.MinimumTeleporterDistance;
            foreach (var teleporter in teleporters)
                if (Vector3.Distance(teleporter, point) < minimum) return false;
            return true;
        }

        private List<Vector3> CollectTeleporterPositions()
        {
            var result = new List<Vector3>();
            try
            {
                foreach (var teleporter in UnityEngine.Object.FindObjectsOfType<MultiSceneTeleporter>())
                    if (teleporter != null && teleporter.gameObject.scene.isLoaded)
                        result.Add(teleporter.transform.position);
                foreach (var teleporter in UnityEngine.Object.FindObjectsOfType<SimpleTeleporter>())
                    if (teleporter != null && teleporter.gameObject.scene.isLoaded)
                        result.Add(teleporter.transform.position);
            }
            catch { }
            return result;
        }

        private bool ClearOfExtraction(Vector3 point, int pass, List<Vector3> extractions)
        {
            if (pass >= 2) return true;
            if (extractions.Count == 0)
            {
                if (!_extractionConstraintLogged)
                {
                    _extractionConstraintLogged = true;
                    _log("No CountDownArea in loaded scenes; extraction distance check skipped");
                }
                return true;
            }
            var minimum = (float)_config.Placement.MinimumExtractionDistance;
            var maximum = (float)_config.Placement.MaximumExtractionDistance;
            // A map usually has several extraction points. Requiring the 40-80m band against
            // every one of them is unsatisfiable; use the nearest extraction instead, while
            // still rejecting points that sit too close to any extraction.
            var nearest = float.MaxValue;
            foreach (var area in extractions)
            {
                var distance = Vector3.Distance(area, point);
                if (distance < nearest) nearest = distance;
            }
            if (nearest < minimum) return false;
            if (pass == 0 && nearest > maximum) return false;
            return true;
        }

        private List<Vector3> CollectExtractionPositions()
        {
            var result = new List<Vector3>();
            try
            {
                foreach (var area in UnityEngine.Object.FindObjectsOfType<CountDownArea>())
                    if (area != null && area.gameObject.scene.isLoaded)
                        result.Add(area.transform.position);
            }
            catch { }
            return result;
        }

        private void PruneDestroyedVisuals()
        {
            // A save/scene reload destroys scene objects without telling this dictionary. Remove
            // dead entries so SyncScene recreates the station instead of skipping it forever.
            foreach (var id in _stations.Where(pair => pair.Value == null).Select(pair => pair.Key).ToList())
                _stations.Remove(id);
            foreach (var id in _markers.Where(pair => pair.Value == null).Select(pair => pair.Key).ToList())
                _markers.Remove(id);
            foreach (var id in _views.Where(pair => pair.Value == null).Select(pair => pair.Key).ToList())
                _views.Remove(id);
        }

        private void CreateStationObject(StationRecord station, Scene scene)
        {
            try
            {
                var position = new Vector3((float)station.X, (float)station.Y, (float)station.Z);
                var root = _visuals.CreateStation(position, (float)station.Yaw, station.Used);
                SceneManager.MoveGameObjectToScene(root, scene);
                var renderers = root.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0)
                {
                    _log("Station visual produced no renderers id=" + station.Id + "; retrying");
                    UnityEngine.Object.Destroy(root);
                    return;
                }
                var view = root.GetComponent<StationView>();
                if (view != null) _views[station.Id] = view;
                AddInteraction(root, station.Id);
                AddMarker(station, scene);
                _stations[station.Id] = root;
                _log("Station visual created id=" + station.Id + " used=" + station.Used
                     + " layer=" + LayerMask.LayerToName(root.layer)
                     + " renderers=" + renderers.Length);
            }
            catch (Exception exception)
            {
                _log("Station visual failed id=" + station.Id + ": " + exception);
            }
        }

        private void AddInteraction(GameObject root, string stationId)
        {
            var point = new GameObject("InteractPoint");
            point.transform.SetParent(root.transform, false);
            point.transform.localPosition = Vector3.up * 0.95f;
            var layer = LayerMask.NameToLayer("Interactable");
            if (layer >= 0) point.layer = layer;
            var collider = point.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.center = Vector3.zero;
            collider.size = new Vector3(1.5f, 1.9f, 1.5f);
            var interactable = point.AddComponent<StationInteractable>();
            interactable.World = this;
            interactable.StationId = stationId;
        }

        private void AddMarker(StationRecord station, Scene scene)
        {
            SimplePointOfInterest? marker = null;
            try
            {
                // One marker per station: a scene reload can destroy the entity while the
                // MainScene POI survives, so replace any stale marker instead of stacking.
                if (_markers.TryGetValue(station.Id, out var previous))
                {
                    if (previous != null) UnityEngine.Object.Destroy(previous);
                    _markers.Remove(station.Id);
                }
                // The minimap filters POIs by override scene id. MainScene's id makes the
                // marker invisible while the player is in the active sub-scene, so prefer
                // ActiveSubSceneID and only fall back to the scene's build index.
                var sceneId = "";
                try { sceneId = MultiSceneCore.ActiveSubSceneID ?? ""; } catch { }
                if (string.IsNullOrEmpty(sceneId))
                    sceneId = SceneInfoCollection.GetSceneID(scene.buildIndex);
                if (string.IsNullOrEmpty(sceneId))
                    sceneId = scene.name;
                marker = SimplePointOfInterest.Create(
                    new Vector3((float)station.X, (float)station.Y, (float)station.Z),
                    sceneId, station.Name, _visuals.Icon, false);
                // Create() registers in OnEnable before its fields are assigned; re-apply the
                // scene/name/icon so the minimap definitely sees the right values.
                marker.Setup(_visuals.Icon, station.Name, false, sceneId);
                // Bright, saturated colours plus a bigger scale: the old marker was dim and
                // easy to miss on the dark minimap, especially at night or in a storm.
                marker.Color = station.Used ? new Color(0.55f, 0.55f, 0.55f) : new Color(0.30f, 1f, 0.55f);
                marker.ScaleFactor = 1.35f;
                _markers[station.Id] = marker.gameObject;
                _log("Station marker id=" + station.Id + " sceneId=" + sceneId
                     + " overrideScene=" + marker.OverrideScene);
            }
            catch (Exception exception)
            {
                if (marker != null) UnityEngine.Object.Destroy(marker.gameObject);
                _markers.Remove(station.Id);
                _log("Station marker failed id=" + station.Id + ": " + exception.Message);
            }
        }

        internal void MarkStationUsed(string stationId)
        {
            if (_views.TryGetValue(stationId, out var view)) view.SetUsed(true);
            if (_markers.TryGetValue(stationId, out var marker))
            {
                var point = marker.GetComponent<SimplePointOfInterest>();
                if (point != null) point.Color = new Color(0.55f, 0.55f, 0.55f);
            }
        }

        internal StationRecord? FindStation(string stationId) => _store.Session.Find(stationId);

        internal bool CanOpen(string stationId)
        {
            if (!Ready || !Alive || _panel == null || _panel.IsOpen) return false;
            var station = _store.Session.Find(stationId);
            if (station == null || station.Used) return false;
            if (!string.Equals(station.MapId, _mapId, StringComparison.Ordinal)
                || !string.Equals(station.SceneName, _sceneName, StringComparison.Ordinal)) return false;
            var actor = Actor;
            if (actor == null) return false;
            if (Vector3.Distance(actor.transform.position,
                    new Vector3((float)station.X, (float)station.Y, (float)station.Z)) > 4.5f) return false;
            return !NearNativeTravel(actor.transform.position, 3.5f);
        }

        internal void OpenStation(string stationId)
        {
            if (LevelManager.Instance == null) return;
            var station = _store.Session.Find(stationId);
            if (station == null) return;
            if (!string.Equals(station.MapId, _mapId, StringComparison.Ordinal)
                || !string.Equals(station.SceneName, _sceneName, StringComparison.Ordinal))
            {
                Notify("驿站不在当前地图。", false, "菜鸭驿站 · 无法打开", NoticeKind.Warning,
                    "请回到驿站所在区域", "注意");
                return;
            }
            if (station.Used)
            {
                Notify("这个驿站本局已经发过货了，下一局再来。", false, "菜鸭驿站 · 已停业",
                    NoticeKind.Warning, "每站每局只能发货一次", "停业");
                return;
            }
            var actor = Actor;
            if (actor == null) return;
            if (Vector3.Distance(actor.transform.position,
                    new Vector3((float)station.X, (float)station.Y, (float)station.Z)) > 5f)
            {
                Notify("请先靠近菜鸭驿站。", false, "菜鸭驿站 · 距离太远", NoticeKind.Info,
                    "走近柜台后再按交互键", "提示");
                return;
            }
            if (_config.Safety.RequireInputGuard && !_inputGuardAvailable)
            {
                Notify("输入保护未启用，暂时不能打开驿站面板。请查看 F8 日志。", false,
                    "菜鸭驿站 · 输入保护异常", NoticeKind.Danger,
                    "关闭会误触发射击/使用；请检查日志", "警告");
                return;
            }
            if (!_inputGuardAvailable)
                Notify("输入保护未启用，面板点击可能误触发射击/使用，请谨慎。", false,
                    "菜鸭驿站 · 输入保护降级", NoticeKind.Warning,
                    "建议关闭面板前不要点击射击", "注意");
            if (_panel == null)
            {
                _log("Station panel not attached; cannot open " + stationId);
                return;
            }
            try
            {
                actor.SetAdsInput(false);
                actor.SetRunInput(false);
                actor.Trigger(false, false, true);
            }
            catch { }
            _log("Opening station panel id=" + stationId
                 + " distance=" + Vector3.Distance(actor.transform.position,
                     new Vector3((float)station.X, (float)station.Y, (float)station.Z)).ToString("0.0"));
            _panel.Open(stationId);
        }

        private static bool NearNativeTravel(Vector3 position, float radius)
        {
            try
            {
                foreach (var teleporter in UnityEngine.Object.FindObjectsOfType<MultiSceneTeleporter>())
                {
                    if (teleporter == null || !teleporter.gameObject.scene.isLoaded) continue;
                    var point = teleporter.interactCollider != null
                        ? teleporter.interactCollider.ClosestPoint(position)
                        : teleporter.transform.position;
                    if (Vector3.Distance(point, position) <= radius) return true;
                }
                foreach (var teleporter in UnityEngine.Object.FindObjectsOfType<SimpleTeleporter>())
                {
                    if (teleporter == null || !teleporter.gameObject.scene.isLoaded) continue;
                    if (Vector3.Distance(teleporter.transform.position, position) <= radius) return true;
                }
            }
            catch { }
            return false;
        }

        internal void Notify(string text, bool native = false, string? title = null,
            NoticeKind kind = NoticeKind.Info, string? hint = null, string? tag = null)
        {
            var notice = new Notice
            {
                Title = string.IsNullOrWhiteSpace(title) ? "菜鸭驿站" : title!,
                Body = text,
                Hint = hint ?? "",
                Tag = tag ?? "",
                Kind = kind,
                Duration = native ? 6.5f : 4.6f
            };
            if (_notices.Count >= 4) _notices.Dequeue();
            _notices.Enqueue(notice);
            if (_activeNotice == null) StartNextNotice();
            if (native)
            {
                try { Duckov.UI.NotificationText.Push(text); }
                catch (Exception exception) { _log("Native notification failed: " + exception.Message); }
            }
            _log("Notify: " + (string.IsNullOrWhiteSpace(title) ? "" : title + " | ") + text);
        }

        private void StartNextNotice()
        {
            if (_notices.Count == 0)
            {
                _activeNotice = null;
                return;
            }
            _activeNotice = _notices.Dequeue();
            _noticeStart = Time.unscaledTime;
            _noticeUntil = _noticeStart + _activeNotice.Duration;
        }

        private void ClearNotices()
        {
            _notices.Clear();
            _activeNotice = null;
            _noticeStart = 0f;
            _noticeUntil = 0f;
        }

        internal void DrawWorldLabels(UiTheme? theme, GUIStyle fallbackStyle, float scale, float logicalWidth, float logicalHeight)
        {
            try
            {
                if (_config.Ui.ShowWorldLabels)
                {
                    var camera = Camera.main;
                    if (camera != null)
                    {
                        var actor = Actor;
                        if (actor != null)
                        {
                            foreach (var station in _store.Session.ForMap(_mapId))
                            {
                                var position = new Vector3((float)station.X, (float)station.Y + 2.9f, (float)station.Z);
                                var distance = Vector3.Distance(actor.transform.position, position);
                                if (distance > _config.Ui.LabelDistance) continue;
                                var screen = camera.WorldToScreenPoint(position);
                                var screenPoint = new Vector2(screen.x / scale, logicalHeight - screen.y / scale);
                                var text = station.Used
                                    ? station.Name + "（已停业）"
                                    : station.Name + "  " + Mathf.RoundToInt(distance) + "米";
                                var labelStyle = theme?.PillLabel ?? fallbackStyle;
                                var content = new GUIContent(text);
                                var size = labelStyle.CalcSize(content);
                                var pill = new Rect(0, 0, size.x + 26, 28);
                                const float margin = 52f;
                                var onScreen = screen.z > 0
                                               && screenPoint.x > margin && screenPoint.x < logicalWidth - margin
                                               && screenPoint.y > margin && screenPoint.y < logicalHeight - margin;
                                if (onScreen)
                                {
                                    pill.x = screenPoint.x - pill.width * 0.5f;
                                    pill.y = screenPoint.y - pill.height - 6;
                                }
                                else
                                {
                                    var center = new Vector2(logicalWidth * 0.5f, logicalHeight * 0.5f);
                                    var direction = screenPoint - center;
                                    if (screen.z <= 0) direction = -direction;
                                    if (direction.sqrMagnitude < 1f) direction = Vector2.up;
                                    direction.Normalize();
                                    var scaleX = direction.x > 0
                                        ? (logicalWidth - margin - center.x) / direction.x
                                        : direction.x < 0 ? (margin - center.x) / direction.x : float.MaxValue;
                                    var scaleY = direction.y > 0
                                        ? (logicalHeight - margin - center.y) / direction.y
                                        : direction.y < 0 ? (margin - center.y) / direction.y : float.MaxValue;
                                    var edge = center + direction * Mathf.Max(0, Mathf.Min(scaleX, scaleY));
                                    var arrow = Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
                                        ? (direction.x > 0 ? "▶" : "◀")
                                        : (direction.y > 0 ? "▼" : "▲");
                                    var arrowText = arrow + " " + station.Name + " " + Mathf.RoundToInt(distance) + "米";
                                    content = new GUIContent(arrowText);
                                    size = labelStyle.CalcSize(content);
                                    pill.width = size.x + 24;
                                    pill.height = 26;
                                    pill.x = Mathf.Clamp(edge.x - pill.width * 0.5f, 4, logicalWidth - pill.width - 4);
                                    pill.y = Mathf.Clamp(edge.y - pill.height * 0.5f, 4, logicalHeight - pill.height - 4);
                                }
                                if (theme != null) theme.DrawPill(pill);
                                var previous = GUI.contentColor;
                                GUI.contentColor = station.Used
                                    ? new Color(0.78f, 0.78f, 0.78f, 0.98f)
                                    : new Color(0.72f, 1f, 0.82f, 1f);
                                GUI.Label(pill, content.text, labelStyle);
                                GUI.contentColor = previous;
                            }
                        }
                    }
                }
                DrawNotice(theme, fallbackStyle, logicalWidth);
            }
            catch { }
        }

        private void DrawNotice(UiTheme? theme, GUIStyle fallbackStyle, float logicalWidth)
        {
            if (_activeNotice == null) return;
            var now = Time.unscaledTime;
            if (now >= _noticeUntil)
            {
                StartNextNotice();
                if (_activeNotice == null) return;
            }
            var notice = _activeNotice;
            var elapsed = now - _noticeStart;
            var remaining = _noticeUntil - now;
            var enter = Mathf.Clamp01(elapsed / 0.32f);
            var ease = 1f - Mathf.Pow(1f - enter, 3f);
            var alpha = Mathf.Clamp01(enter * 1.4f) * Mathf.Clamp01(remaining / 0.45f);
            var width = Mathf.Min(760f, logicalWidth - 40f);
            if (width < 300f) width = Mathf.Max(200f, logicalWidth - 20f);
            const float height = 118f;
            var y = Mathf.Lerp(-height - 16f, 22f, ease);
            var rect = new Rect((logicalWidth - width) * 0.5f, y, width, height);
            var accent = NoticeAccent(notice.Kind, theme);
            var previousColor = GUI.color;
            var previousContent = GUI.contentColor;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            // The banner must survive night/storm scenes: solid dark backing (not translucent),
            // a bright accent frame and a full-width top strip.
            if (theme != null)
            {
                theme.DrawSolid(new Rect(rect.x - 3, rect.y - 3, rect.width + 6, rect.height + 6),
                    new Color(accent.r, accent.g, accent.b, 0.95f * alpha));
                theme.DrawPanel(rect);
                theme.DrawSolid(new Rect(rect.x, rect.y, rect.width, 5),
                    new Color(accent.r, accent.g, accent.b, alpha));
            }
            else GUI.Box(rect, GUIContent.none);
            if (theme != null)
            {
                theme.DrawSolid(new Rect(rect.x + 10, rect.y + 22, 5, rect.height - 44),
                    new Color(accent.r, accent.g, accent.b, alpha));
                var iconRect = new Rect(rect.x + 28, rect.y + 28, 60, 60);
                theme.DrawPill(iconRect);
                DrawNoticeIcon(iconRect, theme, alpha);
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.contentColor = new Color(1f, 1f, 1f, alpha);
                GUI.Label(new Rect(rect.x + 104, rect.y + 16, rect.width - 230, 30), notice.Title, theme.NoticeTitle);
                GUI.Label(new Rect(rect.x + 104, rect.y + 50, rect.width - 130, 24), notice.Body, theme.NoticeBody);
                if (!string.IsNullOrEmpty(notice.Hint))
                    GUI.Label(new Rect(rect.x + 104, rect.y + 78, rect.width - 130, 22), notice.Hint, theme.NoticeHint);
                var badge = new Rect(rect.xMax - 116, rect.y + 22, 92, 26);
                theme.DrawPill(badge);
                GUI.contentColor = new Color(accent.r, accent.g, accent.b, alpha);
                GUI.Label(badge, NoticeTag(notice), theme.Badge);
            }
            else
            {
                GUI.Label(new Rect(rect.x + 20, rect.y + 12, rect.width - 40, 28), notice.Title, fallbackStyle);
                GUI.Label(new Rect(rect.x + 20, rect.y + 44, rect.width - 40, 26), notice.Body, fallbackStyle);
                if (!string.IsNullOrEmpty(notice.Hint))
                    GUI.Label(new Rect(rect.x + 20, rect.y + 76, rect.width - 40, 22), notice.Hint, fallbackStyle);
            }
            GUI.color = previousColor;
            GUI.contentColor = previousContent;
        }

        private void DrawNoticeIcon(Rect rect, UiTheme theme, float alpha)
        {
            var icon = _visuals.Icon;
            if (icon != null && icon.texture != null)
            {
                var texture = icon.texture;
                var textureRect = icon.textureRect;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTextureWithTexCoords(new Rect(rect.x + 4, rect.y + 4, rect.width - 8, rect.height - 8),
                    texture,
                    new Rect(textureRect.x / texture.width, textureRect.y / texture.height,
                        textureRect.width / texture.width, textureRect.height / texture.height), true);
                return;
            }
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(rect, "驿", theme.Label);
        }

        private static Color NoticeAccent(NoticeKind kind, UiTheme? theme)
        {
            if (theme == null)
                return kind == NoticeKind.Danger ? new Color(1f, 0.48f, 0.42f, 1f) : new Color(0.32f, 0.90f, 0.58f, 1f);
            return kind switch
            {
                NoticeKind.Success => theme.Good,
                NoticeKind.Warning => theme.Warning,
                NoticeKind.Danger => theme.Danger,
                _ => theme.Accent
            };
        }

        private static string NoticeTag(Notice notice)
        {
            if (!string.IsNullOrEmpty(notice.Tag)) return notice.Tag;
            return notice.Kind switch
            {
                NoticeKind.Success => "完成",
                NoticeKind.Warning => "注意",
                NoticeKind.Danger => "警告",
                _ => "提示"
            };
        }

        internal string StationStatusText()
        {
            try
            {
                var mapStations = _store.Session.ForMap(_mapId).ToList();
                var available = mapStations.Count(station => !station.Used);
                return "实体 " + _stations.Count + " / 标记 " + _markers.Count
                       + " / 营业 " + available + "（本图共 " + mapStations.Count + "）";
            }
            catch (Exception exception)
            {
                return "读取失败：" + exception.Message;
            }
        }

        internal string NearestStationText()
        {
            try
            {
                var actor = Actor;
                if (actor == null) return "未进入存档";
                var stations = _store.Session.ForMap(_mapId).ToList();
                if (stations.Count == 0) return "当前地图无站点";
                StationRecord? nearest = null;
                var best = float.MaxValue;
                foreach (var station in stations)
                {
                    var distance = Vector3.Distance(actor.transform.position,
                        new Vector3((float)station.X, (float)station.Y, (float)station.Z));
                    if (distance >= best) continue;
                    best = distance;
                    nearest = station;
                }
                if (nearest == null) return "当前地图无站点";
                return nearest.Name + " " + Mathf.RoundToInt(best) + "米（"
                       + (nearest.Used ? "停业" : "营业") + "，X"
                       + nearest.X.ToString("0") + " Z" + nearest.Z.ToString("0") + "）";
            }
            catch (Exception exception)
            {
                return "读取失败：" + exception.Message;
            }
        }

        internal void ClosePanel() => _panel?.Close();

        internal void ResetForSave()
        {
            ClearVisuals();
            _announced.Clear();
            _initialized = false;
            _observedRaid = false;
            _baseNoticeShown = false;
            _lastReceiptCountNotified = _store.Receipts.Count;
            _nextPlacementRetry = 0;
            _placementFailureNotified = false;
            ClearNotices();
            _panel?.Close();
        }

        internal void ClearVisuals()
        {
            foreach (var station in _stations.Values)
                if (station != null) UnityEngine.Object.Destroy(station);
            foreach (var marker in _markers.Values)
                if (marker != null) UnityEngine.Object.Destroy(marker);
            _stations.Clear();
            _markers.Clear();
            _views.Clear();
            _mapId = "";
            _sceneName = "";
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ClearVisuals();
            ClearNotices();
            _visuals.Dispose();
        }
    }
}
