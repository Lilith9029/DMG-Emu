using SDL2;
using System.Runtime.InteropServices;

public class RendererDMG
{
    private IntPtr _window;
    private IntPtr _renderer;
    private IntPtr _texture;
    private IntPtr _controller = IntPtr.Zero;

    // UI
    private const int Border = 12;
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

    // DMG
    private const int ScreenWidth = 160;
    private const int ScreenHeight = 144;
    private const int Scale = 3;

    private string _romPath = "";
    private DMG? _dmg;

    public string? DroppedRom { get; private set;  }

    public RendererDMG()
    {
        int winW = ScreenWidth * Scale + Border * 2;
        int winH = ScreenHeight * Scale + Border + (DockAutoHide ? Border : DockHeight);

        SDL.SDL_Init(SDL.SDL_INIT_VIDEO | SDL.SDL_INIT_GAMECONTROLLER);

        _window = SDL.SDL_CreateWindow("DMG Emulator", SDL.SDL_WINDOWPOS_CENTERED, SDL.SDL_WINDOWPOS_CENTERED, winW, winH, SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN);
        _renderer = SDL.SDL_CreateRenderer(_window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED);
        _texture = SDL.SDL_CreateTexture(_renderer, SDL.SDL_PIXELFORMAT_RGB24, (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING, ScreenWidth, ScreenHeight);
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

        int top = DockAutoHide ? Border : DockHeight;
        int availW = w - Border * 2;
        int availH = h - top - Border;

        int scale = Math.Max(1, Math.Min(availW / ScreenWidth, availH / ScreenHeight));
        int gw = ScreenWidth * scale;
        int gh = ScreenHeight * scale;

        return new SDL.SDL_Rect
        {
            x = Border + (availW - gw) / 2,
            y = top + (availH - gh) / 2,
            w = gw,
            h = gh
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

        SDL.SDL_GetWindowSize(_window, out int w, out int h);
        int delta = DockHeight - Border;
        SDL.SDL_SetWindowSize(_window, w, autoHide ? h - delta : h + delta);

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
            new MenuItem { Label = "EXIT", Shortcut = "CTRL+Q", OnClick = () => Console.WriteLine("Exit") },
        }});

        _menu.Tops.Add(new MenuItem { Label = "EMULATION",Children = new()
        {
            new MenuItem { Label = "PAUSE", Shortcut = "F4", OnClick = () => Console.WriteLine("Pause") },
            new MenuItem { Label = "STOP", Shortcut = "F5", OnClick = () => Console.WriteLine("Stop") },
            new MenuItem { Label = "RESTART", Shortcut = "F6", OnClick = () => Console.WriteLine("Restart") },
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
            new MenuItem { Label = "FULLSCREEN", Shortcut = "F11", OnClick = () => Console.WriteLine("Fullscreen") },
            new MenuItem { Label = "WINDOW SIZE", Children = new()
            {
                new MenuItem { Label = "x2", Shortcut = "CTRL+2", OnClick = () => Console.WriteLine("x2") },
                new MenuItem { Label = "x3", Shortcut = "CTRL+3", IsChecked = () => true, OnClick = () => Console.WriteLine("x3") },
                new MenuItem { Label = "x4", Shortcut = "CTRL+4", OnClick = () => Console.WriteLine("x4") },
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
        return true;
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

    private void SaveSlot(int slot) { if (_dmg != null) StateManager.SaveState(_romPath, _dmg, slot); }
    private void LoadSlot(int slot) { if (_dmg != null) StateManager.LoadState(_romPath, _dmg, slot); }
}