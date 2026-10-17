using SDL2;
using StbImageSharp;

public class BitmapFont : IDisposable
{
    private readonly IntPtr _texture;
    private readonly int _charW, _charH, _cols, _rows;
    private readonly char _startChar;

    public int CharW => _charW;
    public int CharH => _charH;

    public int SpaceWidth { get; set; } = 1;
    public int LetterSpacing { get; set; } = 1;

    public BitmapFont(IntPtr renderer, string path, int charW, int charH, int cols, char startChar = ' ')
    {
        _charW = charW;
        _charH = charH;
        _cols = cols;
        _startChar = startChar;

        using var stream = File.OpenRead(path);
        ImageResult img = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        _rows = img.Height / charH;

        _texture = SDL.SDL_CreateTexture(renderer, SDL.SDL_PIXELFORMAT_ABGR8888,
            (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STATIC, img.Width, img.Height);

        unsafe
        {
            fixed (byte* ptr = img.Data)
            {
                SDL.SDL_UpdateTexture(_texture, IntPtr.Zero, (IntPtr)ptr, img.Width * 4);
            }
        }

        SDL.SDL_SetTextureBlendMode(_texture, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
    }

    public void DrawString(IntPtr renderer, string text, int x, int y, byte r, byte g, byte b, int scale = 1)
    {
        SDL.SDL_SetTextureColorMod(_texture, r, g, b);

        foreach (char c in text)
        {
            int index = c - _startChar;

            if (c == ' ' || index < 0 || index >= _cols * _rows)
            {
                x += SpaceWidth * scale;
                continue;
            }

            SDL.SDL_Rect src = new SDL.SDL_Rect
            {
                x = (index % _cols) * _charW,
                y = (index / _cols) * _charH,
                w = _charW,
                h = _charH
            };
            SDL.SDL_Rect dst = new SDL.SDL_Rect { x = x, y = y, w = _charW * scale, h = _charH * scale };

            SDL.SDL_RenderCopy(renderer, _texture, ref src, ref dst);
            x += ((_charW - 1) + LetterSpacing) * scale;
        }
    }

    public (int Width, int Height) MeasureString(string text, int scale = 1)
    {
        if (string.IsNullOrEmpty(text)) return (0, 0);

        int width = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == ' ')
                width += SpaceWidth;
            else
                width += (i == text.Length - 1) ? (_charW - 1) : ((_charW - 1) + LetterSpacing);
        }

        return (width * scale, _charH * scale);
    }

    public void Dispose()
    {
        SDL.SDL_DestroyTexture(_texture);
    }
}