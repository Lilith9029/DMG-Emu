using SDL2;
using System.Runtime.InteropServices;

public class RendererDMG
{
    private IntPtr _window;
    private IntPtr _renderer;
    private IntPtr _texture;
    private IntPtr _controller = IntPtr.Zero;

    // UI
    private const int Border = 0;
    private const int DockHeight = 24;
    private const int HoverZone = 10;
    private const uint HideDelayMs = 400;
    private const float SlideMs = 120f;
    private const byte FrameR = 110, FrameG = 24, FrameB = 26;

    public bool DockAutoHide { get; private set; } = true;

    private float _dockOpen = 0f;
    private uint _lastHoverTick;
    private uint _lastFrameTick;

    private BitmapFont _font = null!;
    private const int TextScale = 2;
    private MenuBar _menu = null!;

    private uint _restoreUntil;
    private int _windowScale = Scale;
    private int _savedX, _savedY;

    // DMG
    private const int ScreenWidth = 160;
    private const int ScreenHeight = 144;
    private const int Scale = 3;

    private string _romPath = "";
    private DMG? _dmg;
    private bool _quitRequested;

    public string? DroppedRom { get; private set;  }

    public RendererDMG()
    {
        int winW = ScreenWidth * Scale + Border * 2;
        int winH = ScreenHeight * Scale + Border + (DockAutoHide ? Border : DockHeight);

        SDL.SDL_Init(SDL.SDL_INIT_VIDEO | SDL.SDL_INIT_GAMECONTROLLER);

        _window = SDL.SDL_CreateWindow("DMG Emulator",
            SDL.SDL_WINDOWPOS_CENTERED, SDL.SDL_WINDOWPOS_CENTERED, winW, winH,
            SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN | SDL.SDL_WindowFlags.SDL_WINDOW_RESIZABLE);
        SDL.SDL_SetWindowMinimumSize(_window,
            ScreenWidth + Border * 2,
            ScreenHeight + DockHeight + Border);
        _renderer = SDL.SDL_CreateRenderer(_window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED);
        _texture = SDL.SDL_CreateTexture(_renderer,
            SDL.SDL_PIXELFORMAT_RGB24, (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING, ScreenWidth, ScreenHeight);
        _dockOpen = DockAutoHide ? 0f : 1f;
        _lastFrameTick = SDL.SDL_GetTicks();
        _font = new BitmapFont(_renderer,
            Path.Combine(AppContext.BaseDirectory, "Assets", "font.png"),
            charW: 4, charH: 6, cols: 16);
        _menu = new MenuBar(_font, TextScale);
        BuildMenu();
    }

    public void Render(byte[] framebuffer)
    {
        EnforceWindowSize();
        UpdateDock();

        unsafe
        {
            fixed (byte* ptr = framebuffer)
            {
                SDL.SDL_UpdateTexture(_texture, IntPtr.Zero, (IntPtr)ptr, ScreenWidth * 3);
            }
        }

        SDL.SDL_SetRenderDrawColor(_renderer, FrameR, FrameG, FrameB, 255);
        SDL.SDL_RenderClear(_renderer);

        SDL.SDL_Rect game = GetGameRect();
        SDL.SDL_RenderCopy(_renderer, _texture, IntPtr.Zero, ref game);

        DrawDock();
        SDL.SDL_RenderPresent(_renderer);
    }

    private SDL.SDL_Rect GetGameRect()
    {
        SDL.SDL_GetWindowSize(_window, out int w, out int h);

        if (IsFullscreen)
        {
            int topF = DockAutoHide ? 0 : DockHeight;
            int availWF = w;
            int availHF = h - topF;

            int gh = availHF;
            int gw = gh * ScreenWidth / ScreenHeight;
            if (gw > availWF)
            {
                gw = availWF;
                gh = gw * ScreenHeight / ScreenWidth;
            }

            return new SDL.SDL_Rect
            {
                x = (availWF - gw) / 2,
                y = topF + (availHF - gh) / 2,
                w = gw,
                h = gh
            };
        }

        int top = DockAutoHide ? Border : DockHeight;
        int availW = w - Border * 2;
        int availH = h - top - Border;

        int scale = Math.Max(1, Math.Min(availW / ScreenWidth, availH / ScreenHeight));
        int gwW = ScreenWidth * scale;
        int ghW = ScreenHeight * scale;

        return new SDL.SDL_Rect
        {
            x = Border + (availW - gwW) / 2,
            y = top + (availH - ghW) / 2,
            w = gwW,
            h = ghW
        };
    }

    private void UpdateDock()
    {
        uint now = SDL.SDL_GetTicks();
        float dt = Math.Min(now - _lastFrameTick, 100u);
        _lastFrameTick = now;

        if (!DockAutoHide)
        {
            _dockOpen = 1f;
            return;
        }

        bool inside = (SDL.SDL_GetWindowFlags(_window) & (uint)SDL.SDL_WindowFlags.SDL_WINDOW_MOUSE_FOCUS) != 0;
        SDL.SDL_GetMouseState(out _, out int my);

        int zone = _dockOpen > 0.01f ? DockHeight : HoverZone;
        bool hover = inside && my >= 0 && my < zone;

        if (hover)
            _lastHoverTick = now;
        bool wantOpen = hover || (now - _lastHoverTick) < HideDelayMs || _menu.IsOpen;

        float step = dt / SlideMs;
        _dockOpen = wantOpen ? Math.Min(1f, _dockOpen + step) : Math.Max(0f, _dockOpen - step);
    }

    private void DrawDock()
    {
        if (_dockOpen <= 0.001f) return;

        SDL.SDL_GetWindowSize(_window, out int w, out int h);
        int y = (int)(-DockHeight * (1f - _dockOpen));

        _menu.Update(y, DockHeight, w, h);

        SDL.SDL_Rect bar = new SDL.SDL_Rect { x = 0, y = y, w = w, h = DockHeight };
        SDL.SDL_SetRenderDrawColor(_renderer, FrameR, FrameG, FrameB, 255);
        SDL.SDL_RenderFillRect(_renderer, ref bar);

        _menu.DrawBar(_renderer);
        _menu.DrawDropDowns(_renderer);
    }

    private void SetDockAutoHide(bool autoHide)
    {
        if (DockAutoHide == autoHide) return;
        DockAutoHide = autoHide;

        if (!IsFullscreen && !IsMaximized)
            ApplyWindowSize();

        _dockOpen = autoHide ? 0f : 1f;
    }

    public void BuildMenu()
    {
        _menu.Tops.Add(new MenuItem { Label = "FILE", Children = new() 
        {
            new MenuItem { Label = "LOAD ROM...", Shortcut = "CTRL+O", OnClick = () => Console.WriteLine("Load Rom") },
            new MenuItem { Label = "RECENT FILES", Children = new()
            {
                new MenuItem { Label = "(EMPTY)", IsEnabled = () => false },
                MenuItem.Sep(),
                new MenuItem { Label = "CLEAR RECENT FILES", OnClick = () => Console.WriteLine("Clear Recent") },
            }},
            MenuItem.Sep(),
            new MenuItem { Label = "OPEN EMU FOLDER", OnClick = () => Console.WriteLine("Open Emu Folder")},
            MenuItem.Sep(),
            new MenuItem { Label = "EXIT", Shortcut = "CTRL+Q", OnClick = () => RequestExit() },
        }});

        _menu.Tops.Add(new MenuItem { Label = "EMULATION",Children = new()
        {
            new MenuItem { Label = "PAUSE", Shortcut = "F4", OnClick = () => Console.WriteLine("Pause") },
            new MenuItem { Label = "STOP", Shortcut = "F5", OnClick = () => Console.WriteLine("Stop") },
            new MenuItem { Label = "RESTART", Shortcut = "F6", OnClick = () => RestartRom() },
            MenuItem.Sep(),
            new MenuItem { Label = "LOAD STATE", Children = new()
            {
                new MenuItem { Label = "STATE 1", Shortcut = "F3", OnClick = () => LoadSlot(1) },
                new MenuItem { Label = "STATE 2", Shortcut = "SHIFT+F3", OnClick = () => LoadSlot(2) },
            }},
            new MenuItem { Label = "SAVE STATE", Children = new()
            {
                new MenuItem { Label = "STATE 1", Shortcut = "F2", OnClick = () => SaveSlot(1) },
                new MenuItem { Label = "STATE 2", Shortcut = "SHIFT+F2", OnClick = () => SaveSlot(2) },
            }},
            MenuItem.Sep(),
            new MenuItem { Label = "CONFIGURE...", Shortcut = "CTRL+,", OnClick = () => Console.WriteLine("Configure") },
        }});

        _menu.Tops.Add(new MenuItem { Label = "VIEW", Children = new()
        {
            new MenuItem { Label = "FULLSCREEN", Shortcut = "F11", IsChecked = () => IsFullscreen, OnClick = () => ToggleFullscreen() },
            new MenuItem { Label = "WINDOW SIZE", Children = new()
            {
                ScaleItem(2),
                ScaleItem(3),
                ScaleItem(4)
            }},
            MenuItem.Sep(),
            new MenuItem { Label = "THEME", Children = new()
            {
                new MenuItem { Label = "THEME 1", OnClick = () => Console.WriteLine("THEME 1") },
                new MenuItem { Label = "THEME 2", OnClick = () => Console.WriteLine("THEME 2") },
                MenuItem.Sep(),
                new MenuItem { Label = "CONFIGURE...", OnClick = () => Console.WriteLine("Configure") },
            }},
            MenuItem.Sep(),
            new MenuItem { Label = "PIN DOCK", Shortcut = "F10",
                IsChecked = () => !DockAutoHide,
                OnClick = () => SetDockAutoHide(!DockAutoHide) },
        }});

        _menu.Tops.Add(new MenuItem { Label = "HELP", Children = new()
        {
            new MenuItem { Label = "GITHUB REPOSITORY", OnClick = () => Console.WriteLine("GitHub") },
            new MenuItem { Label = "ABOUT", Shortcut = "F1", OnClick = () => Console.WriteLine("About") },
        }});
    }

    public bool HandleEvents(Joypad joypad, string romPath, DMG dmg)
    {
        _romPath = romPath;
        _dmg = dmg;
        while (SDL.SDL_PollEvent(out SDL.SDL_Event e) != 0)
        {
            if (e.type == SDL.SDL_EventType.SDL_QUIT)
                return false;

            if (e.type == SDL.SDL_EventType.SDL_DROPFILE)
            {
                string? path = Marshal.PtrToStringUTF8(e.drop.file);

                if (path != null &&
                    (path.EndsWith(".gb", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".gbc", StringComparison.OrdinalIgnoreCase)))
                {
                    DroppedRom = path;
                    return false;
                }
            }

            if (e.type == SDL.SDL_EventType.SDL_KEYDOWN)
            {
                if (e.key.repeat == 0 && _menu.TryHotKey(e.key.keysym.sym, e.key.keysym.mod))
                    continue;
            }

            if (e.type == SDL.SDL_EventType.SDL_CONTROLLERDEVICEADDED)
            {
                if (_controller == IntPtr.Zero)
                    _controller = SDL.SDL_GameControllerOpen(e.cdevice.which);
            }
            else if (e.type == SDL.SDL_EventType.SDL_CONTROLLERDEVICEREMOVED)
            {
                if (_controller != IntPtr.Zero)
                {
                    SDL.SDL_GameControllerClose(_controller);
                    _controller = IntPtr.Zero;
                }
            }

            // Mouse events
            if (e.type == SDL.SDL_EventType.SDL_MOUSEMOTION)
            {
                _menu.OnMouseMove(e.motion.x, e.motion.y);
            }
            else if (e.type == SDL.SDL_EventType.SDL_MOUSEBUTTONDOWN)
            {
                _menu.OnMouseDown(e.button.x, e.button.y, e.button.button);
            }
            if (e.type == SDL.SDL_EventType.SDL_WINDOWEVENT && 
                e.window.windowEvent == SDL.SDL_WindowEventID.SDL_WINDOWEVENT_SIZE_CHANGED)
            {
                OnWindowResized();
            }

            // Keyboard events
            if (e.type == SDL.SDL_EventType.SDL_KEYDOWN)
            {
                Key? key = MapKey(e.key.keysym.sym);
                if (key.HasValue) joypad?.KeyDown(key.Value);
                if (e.key.keysym.sym == SDL.SDL_Keycode.SDLK_ESCAPE) _menu.Close();
            }
            else if (e.type == SDL.SDL_EventType.SDL_KEYUP)
            {
                Key? key = MapKey(e.key.keysym.sym);
                if (key.HasValue) joypad?.KeyUp(key.Value);
            }

            // Controller events
            if (e.type == SDL.SDL_EventType.SDL_CONTROLLERBUTTONDOWN)
            {
                Key? key = MapController((SDL.SDL_GameControllerButton)e.cbutton.button);
                if (key.HasValue) joypad?.KeyDown(key.Value);
            }
            else if (e.type == SDL.SDL_EventType.SDL_CONTROLLERBUTTONUP)
            {
                Key? key = MapController((SDL.SDL_GameControllerButton)e.cbutton.button);
                if (key.HasValue) joypad?.KeyUp(key.Value);
            }
        }
        return !_quitRequested;
    }

    private Key? MapKey(SDL.SDL_Keycode keycode)
    {
        return keycode switch
        {
            SDL.SDL_Keycode.SDLK_RIGHT => Key.Right,
            SDL.SDL_Keycode.SDLK_LEFT => Key.Left,
            SDL.SDL_Keycode.SDLK_UP => Key.Up,
            SDL.SDL_Keycode.SDLK_DOWN => Key.Down,
            SDL.SDL_Keycode.SDLK_x => Key.A,
            SDL.SDL_Keycode.SDLK_z => Key.B,
            SDL.SDL_Keycode.SDLK_RETURN => Key.Start,
            SDL.SDL_Keycode.SDLK_BACKSPACE => Key.Select,
            _ => null
        };
    }

    private Key? MapController(SDL.SDL_GameControllerButton button)
    {
        return button switch
        {
            SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_RIGHT => Key.Right,
            SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_LEFT => Key.Left,
            SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_UP => Key.Up,
            SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_DOWN => Key.Down,
            SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_A => Key.A,
            SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_B => Key.B,
            SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_START => Key.Start,
            SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_BACK => Key.Select,
            _ => null
        };
    }

    public void Destroy()
    {
        SDL.SDL_DestroyTexture(_texture);
        _font.Dispose();
        SDL.SDL_DestroyRenderer(_renderer);
        SDL.SDL_DestroyWindow(_window);
        SDL.SDL_Quit();
    }

    private int WindowW() => ScreenWidth * _windowScale + Border * 2;
    private int WindowH() => ScreenHeight * _windowScale + (DockAutoHide ? Border : DockHeight) + Border;
    private bool IsFullscreen =>
        (SDL.SDL_GetWindowFlags(_window) & (uint)SDL.SDL_WindowFlags.SDL_WINDOW_FULLSCREEN_DESKTOP) != 0;
    private bool IsMaximized =>
    (SDL.SDL_GetWindowFlags(_window) & (uint)SDL.SDL_WindowFlags.SDL_WINDOW_MAXIMIZED) != 0;

    private void ApplyWindowSize()
    {
        SDL.SDL_GetWindowSize(_window, out int w, out int h);
        if (w != WindowW() || h != WindowH())
            SDL.SDL_SetWindowSize(_window, WindowW(), WindowH());
    }

    private void ToggleFullscreen()
    {
        uint now = SDL.SDL_GetTicks();

        if (!IsFullscreen)
        {
            SDL.SDL_SetWindowFullscreen(_window, (uint)SDL.SDL_WindowFlags.SDL_WINDOW_FULLSCREEN_DESKTOP);
        }
        else
        {
            SDL.SDL_SetWindowFullscreen(_window, 0);
            _restoreUntil = now + 500;
            ApplyWindowSize();
            SDL.SDL_SetWindowPosition(_window, _savedX, _savedY);
        }
    }

    private void OnWindowResized()
    {
        if (IsFullscreen || IsMaximized || _restoreUntil != 0) return;

        SDL.SDL_GetWindowSize(_window, out int w, out int h);
        int top = DockAutoHide ? Border : DockHeight;
        int s = Math.Min((w - Border * 2) / ScreenWidth, (h - top - Border) / ScreenHeight);
        _windowScale = Math.Max(1, s);
    }

    private void EnforceWindowSize()
    {
        if (IsFullscreen || IsMaximized) return;

        if (_restoreUntil != 0)
        {
            if (SDL.SDL_GetTicks() > _restoreUntil)
            {
                _restoreUntil = 0;
            }
            else
            {
                ApplyWindowSize();
                SDL.SDL_GetWindowPosition(_window, out int x, out int y);
                if (x != _savedX || y != _savedY)
                    SDL.SDL_SetWindowPosition(_window, _savedX, _savedY);
                return;
            }
        }

        SDL.SDL_GetWindowPosition(_window, out _savedX, out _savedY);
    }

    private int CurrentScale() => GetGameRect().w / ScreenWidth;

    private void SetWindowScale(int s)
    {
        if (IsFullscreen) return;
        if (IsMaximized)
            SDL.SDL_RestoreWindow(_window);

        SDL.SDL_GetWindowPosition(_window, out int px, out int py);
        SDL.SDL_GetWindowSize(_window, out int ow, out int oh);

        _windowScale = s;
        int nw = WindowW(), nh = WindowH();

        int nx = px + (ow - nw) / 2;
        int ny = Math.Max(0, py + (oh - nh) / 2);

        SDL.SDL_SetWindowSize(_window, nw, nh);
        SDL.SDL_SetWindowPosition(_window, nx, ny);

        _savedX = nx;
        _savedY = ny;
    }

    private MenuItem ScaleItem(int s) => new MenuItem
    {
        Label = $"x{s}",
        Shortcut = $"CTRL+{s}",
        IsChecked = () => !IsFullscreen && _windowScale == s,
        OnClick = () => SetWindowScale(s)
    };

    private void RequestExit() => _quitRequested = true;

    private void RestartRom()
    {
        DroppedRom = _romPath;
        _quitRequested = true;
    }

    private void SaveSlot(int slot) { if (_dmg != null) StateManager.SaveState(_romPath, _dmg, slot); }
    private void LoadSlot(int slot) { if (_dmg != null) StateManager.LoadState(_romPath, _dmg, slot); }
}