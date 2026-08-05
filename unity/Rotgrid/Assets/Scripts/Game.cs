using UnityEngine;

namespace Rotgrid
{
    /// <summary>
    /// Application shell and the single entry point: drop this on one empty
    /// GameObject in an otherwise empty scene and press Play. It builds the
    /// cameras, the menu backdrop and each run, and drives everything from one
    /// explicit tick so update order is never a surprise.
    /// </summary>
    [DisallowMultipleComponent]
    public class Game : MonoBehaviour
    {
        public static AudioEngine Audio = new AudioEngine();
        public static Game Instance;

        /// <summary>Layer 31 is reserved for the first-person weapon model.</summary>
        public const int ViewModelLayer = 31;

        Camera _worldCamera;
        MenuBackground _menuBg;
        Menus _menus;
        GameRun _run;
        bool _quitting;

        public RunSummary LastSummary { get; private set; }
        public bool MenuOpen { get { return _menus != null && _menus.IsOpen; } }

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 144;

            GameSettings.Load();
            GameSettings.ApplyQuality();
            SaveData.Load();
            Audio.Init();

            BuildWorldCamera();
            _menuBg = new MenuBackground();
            _menus = new Menus(this);
            _menus.Show(Screen2.Main);
            SetPlaying(false);
            Audio.StartMusic();
        }

        void BuildWorldCamera()
        {
            var go = new GameObject("WorldCamera");
            go.transform.SetParent(transform, false);
            _worldCamera = go.AddComponent<Camera>();
            _worldCamera.fieldOfView = GameSettings.Fov;
            _worldCamera.nearClipPlane = 0.08f;
            _worldCamera.farClipPlane = 260f;
            _worldCamera.clearFlags = CameraClearFlags.Color;
            _worldCamera.backgroundColor = Util.Hex(0x05070a);
            _worldCamera.depth = 0f;
            // Everything except the view-model layer.
            _worldCamera.cullingMask = ~(1 << ViewModelLayer);
            go.AddComponent<AudioListener>();
            _worldCamera.enabled = false;
        }

        // ================================================================ flow
        public void StartRun()
        {
            if (_run != null) { _run.Dispose(); _run = null; }
            Audio.StopMusic();
            _menus.Hide();
            _menuBg.SetActive(false);
            _worldCamera.enabled = true;
            _worldCamera.fieldOfView = GameSettings.Fov;
            _run = new GameRun(this, _worldCamera, ViewModelLayer);
            SetPlaying(true);
        }

        public void Pause()
        {
            if (_run == null || _run.over) return;
            _run.paused = true;
            SetPlaying(false);
            _menus.Show(Screen2.Pause);
        }

        public void Resume()
        {
            if (_run == null) return;
            _run.paused = false;
            _menus.Hide();
            SetPlaying(true);
        }

        public void EndToMenu()
        {
            if (_run != null) { _run.Dispose(); _run = null; }
            _worldCamera.enabled = false;
            _menuBg.SetActive(true);
            _menus.Show(Screen2.Main);
            SetPlaying(false);
            Audio.StartMusic();
        }

        public void OnGameOver(RunSummary summary)
        {
            LastSummary = summary;
            SetPlaying(false);
            _menus.Show(Screen2.GameOver);
            Audio.StartMusic();
        }

        public void Quit()
        {
            _quitting = true;
            Application.Quit();
        }

        void SetPlaying(bool playing)
        {
            InputMap.Enabled = playing;
            InputMap.SetCursorLocked(playing);
        }

        // ================================================================ loop
        void Update()
        {
            if (_quitting) return;
            float dt = Mathf.Min(0.05f, Time.deltaTime);

            if (Input.GetKeyDown(KeyCode.Escape) && _run != null && !_run.over)
            {
                if (_menus.current == Screen2.Pause) Resume();
                else if (!_menus.IsOpen) Pause();
            }

            // Clicking the view re-captures the cursor after alt-tabbing out.
            if (_run != null && !_run.over && !_menus.IsOpen && !InputMap.Locked && Input.GetMouseButtonDown(0))
                SetPlaying(true);

            if (_run != null && !_run.paused) _run.Update(dt);
            if (_run == null || _menus.current == Screen2.Main) _menuBg.Update(dt);

            Audio.ApplyVolumes();
        }

        void OnGUI()
        {
            UiKit.Begin();
            if (_run != null && !_run.over && !_menus.IsOpen) Hud.Draw(_run);
            _menus.Draw();
        }

        void OnDestroy()
        {
            if (_run != null) _run.Dispose();
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused && _run != null && !_run.over && !_menus.IsOpen) Pause();
        }
    }
}
