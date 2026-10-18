using SDL2;

public sealed class MenuItem
{
    public string Label = "";
    public string? Shortcut;
    public Action? OnClick;
    public List<MenuItem>? Children;
    public Func<bool>? IsChecked;
    public Func<bool>? IsEnabled;
    public bool Separator;

    public bool Checked => IsChecked?.Invoke() ?? false;
    public bool Enabled => IsEnabled?.Invoke() ?? true;

    public static MenuItem Sep() => new MenuItem { Separator = true };
}

public sealed class MenuBar
{
    private static readonly (byte r, byte g, byte b) PanelBg = (78, 18, 20);
    private static readonly (byte r, byte g, byte b) PanelBorder = (150, 48, 52);
    private static readonly (byte r, byte g, byte b) Highlight = (150, 40, 44);
    private static readonly (byte r, byte g, byte b) TextNormal = (240, 200, 200);
    private static readonly (byte r, byte g, byte b) TextDim = (140, 105, 105);
    private static readonly (byte r, byte g, byte b) SepColor = (130, 40, 44);

    private const int BarPadX = 8;
    private const int BarGap = 2;
    private const int TextPadL = 16;
    private const int TextPadR = 14;
    private const int ShortcutGap = 20;
    private const int MinPanelW = 110;
    private const int SepH = 7;

    private readonly BitmapFont _font;
    private readonly int _scale;
    private readonly int _textH;
    private readonly int _itemH;

    public List<MenuItem> Tops { get; } = new();
    public int BarStartX { get; set; } = 8;

    private int _hoverTop = -1;
    private int _openTop = -1;
    private int _openSub = -1;
    private int _hoverL1 = -1, _hoverL2 = -1;

    private int _dockY, _dockH, _winW, _winH;
    private SDL.SDL_Rect[] _topRects = Array.Empty<SDL.SDL_Rect>();
    private SDL.SDL_Rect _l1, _l2;

    public bool IsOpen => _openTop >= 0;

    public MenuBar(BitmapFont font, int textScale)
    {
        _font = font;
        _scale = textScale;
        _textH = font.MeasureString("Ag", textScale).Height;
        _itemH = _textH + 6 * textScale;
    }

    public void Close()
    {
        _openTop = -1;
        _openSub = -1;
        _hoverL1 = _hoverL2 = -1;
    }

    public void Update(int dockY, int dockH, int winW, int winH)
    {
        _dockY = dockY;
        _dockH = dockH;
        _winW = winW;
        _winH = winH;
        Relayout();
    }

    private List<MenuItem>? L1Items => _openTop >= 0 ? Tops[_openTop].Children : null;
    private List<MenuItem>? L2Items =>
        (_openTop >= 0 && _openSub >= 0 && L1Items != null && _openSub < L1Items.Count)
        ? L1Items[_openSub].Children : null;

    private void Relayout()
    {
        if (_topRects.Length != Tops.Count)
            _topRects = new SDL.SDL_Rect[Tops.Count];

        int x = BarStartX;
        for (int i = 0; i < Tops.Count; i++)
        {
            int w = _font.MeasureString(Tops[i].Label, _scale).Width + BarPadX * 2;
            _topRects[i] = new SDL.SDL_Rect { x = x, y = _dockY, w = w, h = _dockH };
            x += w + BarGap;
        }

        _l1 = default;
        _l2 = default;
        var l1 = L1Items;
        if (l1 == null) return;

        int w1 = PanelWidth(l1);
        int h1 = PanelHeight(l1);
        int x1 = Math.Min(_topRects[_openTop].x, Math.Max(0, _winW - w1));
        _l1 = new SDL.SDL_Rect { x = x1, y = _dockY + _dockH, w = w1, h = h1 };

        var l2 = L2Items;
        if (l2 == null) return;

        int w2 = PanelWidth(l2);
        int h2 = PanelHeight(l2);
        int x2 = _l1.x + _l1.w - 1;
        if (x2 + w2 > _winW)
            x2 = _l1.x - w2 + 1;
        int y2 = ItemTop(_l1, l1, _openSub) - 1;
        if (y2 + h2 > _winH)
            y2 = Math.Max(0, _winH - h2);
        _l2 = new SDL.SDL_Rect { x = x2, y = y2, w = w2, h = h2 };
    }

    private int ItemHeight(MenuItem it) => it.Separator ? SepH : _itemH;

    private int PanelHeight(List<MenuItem> items)
    {
        int h = 2;
        foreach (var it in items)
            h += ItemHeight(it);
        return h;
    }

    private int PanelWidth(List<MenuItem> items)
    {
        int w = 0;
        foreach (var it in items)
        {
            if (it.Separator) continue;
            int line = _font.MeasureString(it.Label, _scale).Width;
            if (!string.IsNullOrEmpty(it.Shortcut))
                line += ShortcutGap + _font.MeasureString(it.Shortcut!, _scale).Width;
            if (line > w) w = line;
        }
        return Math.Max(MinPanelW, w + TextPadL + TextPadR + 2);
    }

    private int ItemTop(SDL.SDL_Rect panel, List<MenuItem> items, int index)
    {
        int y = panel.y + 1;
        for (int i = 0; i < index; i++)
            y += ItemHeight(items[i]);
        return y;
    }

    private static bool Inside(SDL.SDL_Rect r, int mx, int my) =>
        mx >= r.x && mx < r.x + r.w && my >= r.y && my < r.y + r.h;

    private int HitItem(SDL.SDL_Rect panel, List<MenuItem> items, int mx, int my)
    {
        if (!Inside(panel, mx, my))
            return -1;
        int y = panel.y + 1;
        for (int i = 0; i < items.Count; i++)
        {
            int h = ItemHeight(items[i]);
            if (my >= y && my < y + h)
                return items[i].Separator ? -1 : i;
            y += h;
        }
        return -1;
    }

    public void OnMouseMove(int mx, int my)
    {
        Relayout();
        _hoverTop = -1;
        _hoverL1 = -1;
        _hoverL2 = -1;

        for (int i = 0; i < _topRects.Length; i++)
        {
            if (!Inside(_topRects[i], mx, my)) continue;
            _hoverTop = i;
            if (IsOpen && i != _openTop)
            {
                _openTop = i;
                _openSub = -1;
                Relayout();
            }
        }
        if (!IsOpen) return;

        var l2 = L2Items;
        if (l2 != null && Inside(_l2, mx, my))
        {
            _hoverL2 = HitItem(_l2, l2, mx, my);
            _hoverL1 = _openSub;
            return;
        }

        var l1 = L1Items;
        if (l1 != null && Inside(_l1, mx, my))
        {
            int idx = HitItem(_l1, l1, mx, my);
            _hoverL1 = idx;
            if (idx >= 0)
            {
                bool hasSub = l1[idx].Children != null && l1[idx].Enabled;
                _openSub = hasSub ? idx : -1;
            }
        }
    }

    public bool OnMouseDown(int mx, int my, byte button)
    {
        if (button != SDL.SDL_BUTTON_LEFT)
            return IsOpen;
        Relayout();

        for (int i = 0; i < _topRects.Length; i++)
        {
            if (!Inside(_topRects[i], mx, my)) continue;
            if (_openTop == i)
                Close();
            else
            {
                _openTop = i;
                _openSub = -1;
            }
            return true;
        }
        if (!IsOpen)
            return false;

        var l2 = L2Items;
        if (l2 != null && Inside(_l2, mx, my))
        {
            int idx = HitItem(_l2, l2, mx, my);
            if (idx >= 0)
                Activate(l2[idx]);
            return true;
        }

        var l1 = L1Items;
        if (l1 != null && Inside(_l1, mx, my))
        {
            int idx = HitItem(_l1, l1, mx, my);
            if (idx >= 0)
            {
                if (l1[idx].Children != null)
                {
                    if (l1[idx].Enabled)
                        _openSub = idx;
                }
                else
                    Activate(l1[idx]);
            }
            return true;
        }

        Close();
        return true;
    }

    private void Activate(MenuItem it)
    {
        if (!it.Enabled || it.Children != null) return;
        Close();
        it.OnClick?.Invoke();
    }

    private static void Fill(IntPtr r, int x, int y, int w, int h, (byte r, byte g, byte b) c)
    {
        SDL.SDL_SetRenderDrawColor(r, c.r, c.g, c.b, 255);
        var rect = new SDL.SDL_Rect { x = x, y = y, w = w, h = h };
        SDL.SDL_RenderFillRect(r, ref rect);
    }

    public void DrawBar(IntPtr renderer)
    {
        for (int i = 0; i < Tops.Count; i++)
        {
            var rc = _topRects[i];
            if (i == _openTop || (!IsOpen && i == _hoverTop))
                Fill(renderer, rc.x, rc.y, rc.w, rc.h, Highlight);

            int ty = rc.y + (rc.h - _textH) / 2;
            _font.DrawString(renderer, Tops[i].Label, rc.x + BarPadX, ty,
                TextNormal.r, TextNormal.g, TextNormal.b, _scale);
        }
    }

    public void DrawDropDowns(IntPtr renderer)
    {
        var l1 = L1Items;
        if (l1 == null) return;
        DrawPanel(renderer, _l1, l1, _hoverL1, _openSub);

        var l2 = L2Items;
        if (l2 != null)
            DrawPanel(renderer, _l2, l2, _hoverL2, -1);
    }

    public void DrawPanel(IntPtr r, SDL.SDL_Rect p, List<MenuItem> items, int hover, int openSub)
    {
        Fill(r, p.x, p.y, p.w, p.h, PanelBorder);
        Fill(r, p.x + 1, p.y + 1, p.w - 2, p.h - 2, PanelBg);

        int y = p.y + 1;
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            int h = ItemHeight(it);

            if (it.Separator)
            {
                Fill(r, p.x + 4, y + h / 2, p.w - 8, 1, SepColor);
                y += h;
                continue;
            }

            if (it.Enabled && (i == hover || i == openSub))
                Fill(r, p.x + 1, y, p.w - 2, h, Highlight);

            var col = it.Enabled ? TextNormal : TextDim;
            int ty = y + (h - _textH) / 2;

            if (it.Checked)
            {
                int s = 3 * _scale;
                Fill(r, p.x + 6, y + (h - s) / 2, s, s, col);
            }

            _font.DrawString(r, it.Label, p.x + TextPadL, ty, col.r, col.g, col.b, _scale);

            if (!string.IsNullOrEmpty(it.Shortcut))
            {
                int sw = _font.MeasureString(it.Shortcut!, _scale).Width;
                _font.DrawString(r, it.Shortcut!, p.x + p.w - TextPadR - sw - 4, ty,
                    TextDim.r, TextDim.g, TextDim.b, _scale);
            }

            if(it.Children != null)
            {
                int aw = _font.MeasureString(">", _scale).Width;
                _font.DrawString(r, ">", p.x + p.w - aw - 6, ty, col.r, col.g, col.b, _scale);
            }

            y += h;
        }
    }

    private readonly struct Hotkey
    {
        public readonly SDL.SDL_Keycode Key;
        public readonly bool Ctrl, Shift, Alt;
        public Hotkey(SDL.SDL_Keycode key, bool ctrl, bool shilf, bool alt)
        {
            Key = key;
            Ctrl = ctrl;
            Shift = shilf;
            Alt = alt;
        }
    }

    private readonly Dictionary<MenuItem, Hotkey?> _hotkeys = new();

    private static Hotkey? ParseHotkey(string text)
    {
        bool ctrl = false, shilf = false, alt = false;
        string[] parts = text.Replace(" ", " ").ToUpperInvariant().Split('+');

        for (int i = 0; i < parts.Length - 1; i++)
        {
            switch (parts[i])
            {
                case "CTRL": ctrl = true; break;
                case "SHIFT": shilf = true; break;
                case "ALT": alt = true; break;
                default: return null;
            }
        }

        string k = parts[^1];
        if (k.Length >= 2 && k[0] == 'F' && int.TryParse(k.AsSpan(1), out int n) && n >= 1 && n <= 24
            && Enum.TryParse<SDL.SDL_Keycode>($"SDLK_F{n}", out var fk))
            return new Hotkey(fk, ctrl, shilf, alt);

        if (k.Length == 1)
            return new Hotkey((SDL.SDL_Keycode)char.ToLowerInvariant(k[0]), ctrl, shilf, alt);

        return null;
    }

    private Hotkey? GetHotkey(MenuItem it)
    {
        if (string.IsNullOrEmpty(it.Shortcut))
            return null;
        if (_hotkeys.TryGetValue(it, out var cached))
            return cached;

        var hk = ParseHotkey(it.Shortcut!);
        if (hk == null)
            Console.WriteLine($"[Menu] Shortcut error: \"{it.Shortcut}\" ({it.Label})");
        _hotkeys[it] = hk;
        return hk;
    }

    public bool TryHotKey(SDL.SDL_Keycode sym, SDL.SDL_Keymod mod)
    {
        bool ctrl = (mod & SDL.SDL_Keymod.KMOD_CTRL) != 0;
        bool shift = (mod & SDL.SDL_Keymod.KMOD_SHIFT) != 0;
        bool alt = (mod & SDL.SDL_Keymod.KMOD_ALT) != 0;

        foreach (var top in Tops)
            if (RunHotkey(top.Children, sym, ctrl, shift, alt))
                return true;
        return false;
    }

    private bool RunHotkey(List<MenuItem>? items, SDL.SDL_Keycode sym, bool ctrl, bool shift, bool alt)
    {
        if (items == null)
            return false;
        foreach (var it in items)
        {
            if (it.Separator) continue;
            if (it.Children != null)
            {
                if (RunHotkey(it.Children, sym, ctrl, shift, alt))
                    return true;
                continue;
            }

            var hk = GetHotkey(it);
            if (hk is { } h && h.Key == sym && h.Ctrl == ctrl && h.Shift == shift && h.Alt == alt && it.Enabled)
            {
                Close();
                it.OnClick?.Invoke();
                return true;
            }
        }
        return false;
    }
}