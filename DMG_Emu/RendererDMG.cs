using SDL2;

public class RendererDMG
{
    private IntPtr _window;
    private IntPtr _renderer;
    private IntPtr _texture;
    private IntPtr _controller = IntPtr.Zero;

    private const int ScreenWidth = 160;
    private const int ScreenHeight = 144;
    private const int Scale = 3;

    public RendererDMG()
    {
        SDL.SDL_Init(SDL.SDL_INIT_VIDEO | SDL.SDL_INIT_GAMECONTROLLER);

        _window = SDL.SDL_CreateWindow("DMG Emulator", SDL.SDL_WINDOWPOS_CENTERED, SDL.SDL_WINDOWPOS_CENTERED, ScreenWidth * Scale, ScreenHeight * Scale, SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN);
        _renderer = SDL.SDL_CreateRenderer(_window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED);
        _texture = SDL.SDL_CreateTexture(_renderer, SDL.SDL_PIXELFORMAT_RGB24, (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING, ScreenWidth, ScreenHeight);
    }

    public void Render(byte[] framebuffer)
    {
        unsafe
        {
            fixed (byte* ptr = framebuffer)
            {
                SDL.SDL_UpdateTexture(_texture, IntPtr.Zero, (IntPtr)ptr, ScreenWidth * 3);
            }
        }
        SDL.SDL_RenderClear(_renderer);
        SDL.SDL_RenderCopy(_renderer, _texture, IntPtr.Zero, IntPtr.Zero);
        SDL.SDL_RenderPresent(_renderer);
    }

    public bool HandleEvents(Joypad joypad)
    {
        while (SDL.SDL_PollEvent(out SDL.SDL_Event e) != 0)
        {
            if (e.type == SDL.SDL_EventType.SDL_QUIT)
                return false;

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

            // Keyboard events
            if (e.type == SDL.SDL_EventType.SDL_KEYDOWN)
            {
                Key? key = MapKey(e.key.keysym.sym);
                if (key.HasValue) joypad?.KeyDown(key.Value);
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
        SDL.SDL_DestroyRenderer(_renderer);
        SDL.SDL_DestroyWindow(_window);
        SDL.SDL_Quit();
    }
}