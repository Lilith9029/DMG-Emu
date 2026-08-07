using SDL2;

public class RendererDMG
{
    private IntPtr _window;
    private IntPtr _renderer;
    private IntPtr _texture;

    private const int ScreenWidth = 160;
    private const int ScreenHeight = 144;
    private const int Scale = 3;

    public RendererDMG()
    {
        SDL.SDL_Init(SDL.SDL_INIT_VIDEO);

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

    public bool HandleEvents()
    {
        while (SDL.SDL_PollEvent(out SDL.SDL_Event e) != 0)
        {
            if (e.type == SDL.SDL_EventType.SDL_QUIT)
                return false;
        }
        return true;
    }

    public void Destroy()
    {
        SDL.SDL_DestroyTexture(_texture);
        SDL.SDL_DestroyRenderer(_renderer);
        SDL.SDL_DestroyWindow(_window);
        SDL.SDL_Quit();
    }
}