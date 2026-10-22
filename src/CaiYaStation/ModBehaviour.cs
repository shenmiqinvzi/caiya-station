using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CaiYaStation
{
    /// <summary>
    /// Entry point. The game's loader resolves &lt;info.ini name&gt;.ModBehaviour, so the assembly,
    /// namespace and class must all be CaiYaStation.ModBehaviour.
    /// </summary>
    public sealed class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        internal const string Version = "0.1.0";
        private const string LogPrefix = "[CaiYaStation] ";
        private bool _initialized;
        private bool _showDiagnostics;
        private bool _uiFailed;
        private bool _inputFailed;
        private bool _fileLogFailed;
        private bool _recoveryDone;
        private float _nextRecovery;
        private string? _logPath;
        private ModConfig? _config;
        private StationLayout? _layout;
        private StateStore? _store;
        private ShipmentRuntime? _runtime;
        private StationWorld? _world;
        private ShipmentPanel? _panel;
        private NativePatches? _patches;
        private Font? _font;
        private GUIStyle? _boxStyle;
        private GUIStyle? _titleStyle;
        private GUIStyle? _bodyStyle;
        private GUIStyle? _smallStyle;
        private GUIStyle? _labelStyle;
        private string _lastStatus = "尚未初始化";
        private string _scene = "等待场景";
        private string _player = "未进入存档";
        private float _nextRefresh;

        private void Awake()
        {
            // Unity calls Awake before the loader calls Setup. Load the bundled Harmony copy
            // before any patch method is JIT compiled.
            try
            {
                var loaded = Array.Find(AppDomain.CurrentDomain.GetAssemblies(),
                    assembly => assembly.GetName().Name == "0Harmony");
                if (loaded == null)
                {
                    var directory = Path.GetDirectoryName(typeof(ModBehaviour).Assembly.Location);
                    if (directory != null)
                        System.Reflection.Assembly.LoadFrom(Path.Combine(directory, "0Harmony.dll"));
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(LogPrefix + "Harmony preload failed: " + exception.Message);
            }
        }

        protected override void OnAfterSetup()
        {
            if (_initialized) return;
            _initialized = true;
            try
            {
                var folder = Path.Combine(info.path, "logs");
                Directory.CreateDirectory(folder);
                _logPath = Path.Combine(folder, "caiyastation.log");
            }
            catch (Exception exception)
            {
                _fileLogFailed = true;
                Debug.LogWarning(LogPrefix + "File logging unavailable: " + exception.Message);
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            Log("Enabled version=" + Version + " unity=" + Application.unityVersion + " path=" + info.path);
            try
            {
                _config = ModConfig.Load(Path.Combine(info.path, "config.json"), Log);
                _layout = StationLayout.Load(Path.Combine(info.path, "station-layout.json"), Log);
                NativeContract.Validate(Log);
                if (!NativeContract.Passed)
                {
                    _showDiagnostics = true;
                    _lastStatus = "原生契约检查失败，功能已停用";
                    Log("Mod did not start: native contract failed. Update the mod for this game build.");
                    RefreshSnapshot();
                    return;
                }
                _store = new StateStore(Log);
                _store.Load();
                _runtime = new ShipmentRuntime(_config, _store, Log);
                _panel = new ShipmentPanel { ModRuntimeLog = Log };
                _world = new StationWorld(_config, _layout, _runtime, _store, Log);
                _world.AttachPanel(_panel);
                _panel.Attach(_world);
                try
                {
                    _patches = new NativePatches();
                    _patches.Enable(Log);
                }
                catch (Exception exception)
                {
                    _patches = null;
                    Log("Panel input guard unavailable; UI clicks may still reach native actions: " + exception);
                }
                _world.SetInputGuardAvailable(_patches != null);
                if (_patches == null && _config.Safety.RequireInputGuard)
                    Log("Safety.RequireInputGuard=true: station panel will refuse to open until input guarding works");
                _lastStatus = "已就绪";
                Log("Runtime ready. config=" + (_config.Enabled ? "enabled" : "disabled")
                    + " maps=" + _layout.Sites.Count + " contract=" + NativeContract.Status);
                try
                {
                    LevelManager.OnEvacuated += OnEvacuated;
                    LevelManager.OnMainCharacterDead += OnCharacterDead;
                    Saves.SavesSystem.OnSetFile += OnSaveFileSet;
                }
                catch (Exception exception) { Log("Event subscription failed: " + exception.Message); }
                RefreshSnapshot();
            }
            catch (Exception exception)
            {
                _lastStatus = "初始化失败";
                _showDiagnostics = true;
                Log("Initialization failed: " + exception);
            }
        }

        protected override void OnBeforeDeactivate() => Shutdown();

        private void OnDestroy() => Shutdown();

        private void OnApplicationQuit() => Shutdown();

        private void Shutdown()
        {
            if (!_initialized) return;
            _initialized = false;
            try { LevelManager.OnEvacuated -= OnEvacuated; } catch { }
            try { LevelManager.OnMainCharacterDead -= OnCharacterDead; } catch { }
            try { Saves.SavesSystem.OnSetFile -= OnSaveFileSet; } catch { }
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _world?.Dispose();
            _world = null;
            _panel?.Dispose();
            _panel = null;
            _patches?.Disable();
            _patches = null;
            _runtime = null;
            _store = null;
            _layout = null;
            _config = null;
            Log("Disabled version=" + Version);
            if (_font != null) Destroy(_font);
            _font = null;
            _boxStyle = null;
            _titleStyle = null;
            _bodyStyle = null;
            _smallStyle = null;
            _labelStyle = null;
        }

        private void Update()
        {
            if (!_initialized) return;
            PanelInputGuard.Tick();
            try
            {
                if (!_inputFailed)
                {
                    var keyboard = Keyboard.current;
                    if (keyboard != null && _config != null && keyboard[_config.DiagnosticsKey].wasPressedThisFrame)
                    {
                        _showDiagnostics = !_showDiagnostics;
                        Log("Diagnostics visible=" + _showDiagnostics);
                    }
                }
            }
            catch (Exception exception)
            {
                _inputFailed = true;
                Log("Keyboard diagnostics disabled: " + exception.Message);
            }

            try
            {
                _world?.Tick();
                _panel?.Tick();
                if (!_recoveryDone && _store != null && _runtime != null
                    && Time.unscaledTime >= _nextRecovery && _world != null && _world.Ready)
                {
                    _nextRecovery = Time.unscaledTime + 3f;
                    var message = _runtime.RecoverPending();
                    if (message != null)
                    {
                        _recoveryDone = true;
                        _world.Notify(message, true, "菜鸭驿站 · 发货已恢复",
                            StationWorld.NoticeKind.Success, "到马蜂自提点领取", "恢复");
                    }
                    else if (_store.Pending == null)
                    {
                        _recoveryDone = true;
                    }
                }
            }
            catch (Exception exception)
            {
                Log("Update failed: " + exception);
            }

            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + 1f;
                RefreshSnapshot();
            }
        }

        private void RefreshSnapshot()
        {
            try
            {
                var scene = SceneManager.GetActiveScene();
                _scene = scene.IsValid() ? scene.name : "等待场景";
                var actor = CharacterMainControl.Main;
                _player = actor == null
                    ? "未进入存档"
                    : string.Format(CultureInfo.InvariantCulture, "X {0:0.0} Y {1:0.0} Z {2:0.0}",
                        actor.transform.position.x, actor.transform.position.y, actor.transform.position.z);
            }
            catch (Exception exception)
            {
                _player = "读取失败";
                if (!_uiFailed) Log("Snapshot failed: " + exception.Message);
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Log("Scene loaded name=" + scene.name + " mode=" + mode);
        }

        private void OnEvacuated(EvacuationInfo info)
        {
            Log("Evacuated; delivered items stay in the storage buffer");
        }

        private void OnCharacterDead(DamageInfo info)
        {
            Log("Main character died; delivered items stay in the storage buffer");
        }

        /// <summary>A different save slot was loaded: reload this mod's keys and drop scene objects.</summary>
        private void OnSaveFileSet()
        {
            try
            {
                Log("Save file changed; reloading CaiYaStation state");
                _store?.Load();
                _world?.ResetForSave();
                _recoveryDone = false;
                _nextRecovery = 0;
            }
            catch (Exception exception)
            {
                Log("Save reload failed: " + exception.Message);
            }
        }

        private void OnGUI()
        {
            if (!_initialized || _uiFailed) return;
            var previousMatrix = GUI.matrix;
            var previousColor = GUI.color;
            try
            {
                EnsureStyles();
                var scale = Mathf.Clamp(Screen.height / 1080f, 0.8f, 1.5f);
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
                GUI.color = Color.white;
                var width = Screen.width / scale;
                var height = Screen.height / scale;
                var theme = _panel?.EnsureTheme();
                _world?.DrawWorldLabels(theme, _labelStyle!, scale, width, height);
                _panel?.Draw(width, height);
                if (_showDiagnostics) DrawDiagnostics(theme, width, height);
            }
            catch (Exception exception)
            {
                _uiFailed = true;
                PanelInputGuard.Set(false);
                _panel?.Close();
                Log("UI disabled after exception: " + exception);
            }
            finally
            {
                GUI.matrix = previousMatrix;
                GUI.color = previousColor;
            }
        }

        private void DrawDiagnostics(UiTheme? theme, float logicalWidth, float logicalHeight)
        {
            var rect = new Rect(16, 16, Mathf.Min(580, logicalWidth - 32), Mathf.Min(500, logicalHeight - 32));
            if (theme != null)
            {
                theme.DrawPanel(rect);
                theme.DrawHeader(new Rect(rect.x, rect.y, rect.width, 62));
                theme.DrawSolid(new Rect(rect.x, rect.y, 4, 62), theme.Accent);
            }
            else
            {
                GUI.Box(rect, GUIContent.none, _boxStyle);
            }
            var titleStyle = theme?.Title ?? _titleStyle!;
            var bodyStyle = theme?.BodyText ?? _bodyStyle!;
            var smallStyle = theme?.Small ?? _smallStyle!;
            GUI.Label(new Rect(rect.x + 20, rect.y + 8, rect.width - 260, 32), "菜鸭驿站 · 诊断 v" + Version, titleStyle);
            if (theme != null)
            {
                var badge = new Rect(rect.x + 210, rect.y + 14, 76, 22);
                theme.DrawPill(badge);
                GUI.Label(badge, NativeContract.Passed ? "契约通过" : "契约失败", theme.Badge);
            }
            var text = "状态：" + _lastStatus + "\n"
                       + "原生契约：" + NativeContract.Status + "\n"
                       + "场景：" + _scene + "\n"
                       + "地图：" + SafeMapId() + "\n"
                       + "玩家：" + _player + "\n"
                       + "最近驿站：" + (_world != null ? _world.NearestStationText() : "-") + "\n"
                       + "站点状态：" + (_world != null ? _world.StationStatusText() : "-") + "\n"
                       + "存档：" + (_store?.Status ?? "未初始化") + "\n"
                       + "背包：" + (_runtime != null ? _runtime.ReadBackpack().Count + " 个堆叠" : "-") + "\n"
                       + "服务：" + (_config != null && _config.Enabled ? "开启" : "关闭")
                       + "　按键：" + (_config?.Keys.Diagnostics ?? "F8") + " 诊断\n"
                       + "输入拦截：" + (_patches != null ? "开启（面板打开时）" : "未启用") + "\n"
                       + "到货缓冲区：" + BufferStatus() + "\n"
                       + "最近回执：" + LastReceiptSummary() + "\n"
                       + "日志：模组目录 / logs / caiyastation.log";
            GUI.Label(new Rect(rect.x + 20, rect.y + 74, rect.width - 40, rect.height - 126), text, bodyStyle);
            GUI.Label(new Rect(rect.x + 20, rect.yMax - 40, rect.width - 40, 24),
                "靠近菜鸭驿站按交互键打开寄送面板；Esc 关闭。", smallStyle);
        }

        private string LastReceiptSummary()
        {
            try
            {
                if (_store == null || _store.Receipts.Count == 0) return "无";
                var receipt = _store.Receipts[_store.Receipts.Count - 1];
                var when = DateTimeOffset.TryParse(receipt.CreatedUtc, out var parsed)
                    ? parsed.ToLocalTime().ToString("MM-dd HH:mm")
                    : "";
                return $"{when} 送达 {receipt.DeliveredStacks} 件 / 丢失 {receipt.LostStacks} 件 / 运费 {receipt.Fee:N0}";
            }
            catch { return "读取失败"; }
        }

        private static string BufferStatus()
        {
            try
            {
                if (PlayerStorage.Loading) return "加载中";
                return PlayerStorage.IncomingItemBuffer == null ? "不可用" : "可用";
            }
            catch (Exception exception)
            {
                return "读取失败：" + exception.Message;
            }
        }

        private static string SafeMapId()
        {
            try { return Duckov.Scenes.MultiSceneCore.MainSceneID ?? "无"; }
            catch { return "无"; }
        }

        private void EnsureStyles()
        {
            if (_bodyStyle != null) return;
            try
            {
                _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 16);
            }
            catch (Exception exception)
            {
                Log("Using default GUI font: " + exception.Message);
            }
            _boxStyle = new GUIStyle(GUI.skin.box);
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 21, fontStyle = FontStyle.Bold };
            _titleStyle.normal.textColor = new Color(0.35f, 0.95f, 0.66f);
            _bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            _bodyStyle.normal.textColor = Color.white;
            _smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            _smallStyle.normal.textColor = new Color(0.78f, 0.84f, 0.82f);
            _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            _labelStyle.normal.textColor = Color.white;
            if (_font != null)
            {
                _titleStyle.font = _font;
                _bodyStyle.font = _font;
                _smallStyle.font = _font;
                _labelStyle.font = _font;
            }
        }

        private void Log(string message)
        {
            Debug.Log(LogPrefix + message);
            if (_fileLogFailed || _logPath == null) return;
            try
            {
                var line = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture) + " " + message + Environment.NewLine;
                File.AppendAllText(_logPath, line, new UTF8Encoding(false));
            }
            catch (Exception exception)
            {
                _fileLogFailed = true;
                Debug.LogWarning(LogPrefix + "File logging disabled: " + exception.Message);
            }
        }
    }
}
