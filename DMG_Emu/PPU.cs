public class PPU
{
    private MMU _mmu;
    private byte[] _framebuffer = new byte[160 * 144 * 3];
    private byte[] _bgScanlineBuffer = new byte[160];
    private int _cycles = 0;
    private int _mode = 2; // OAM
    private readonly byte[] _vram;
    private readonly byte[] _io;
    private readonly byte[] _oam;

    public bool FrameReady { get; private set; }
    private byte ly = 0;
    private byte _lastLyForStat = 0xFF;

    private static readonly (byte r, byte g, byte b)[] _color = {
        (155, 188, 15), // White
        (139, 172, 15), // Light Gray
        (48, 98, 48),   // Dark Gray
        (15, 56, 15)    // Black
    };

    public byte[] Framebuffer => _framebuffer;
    public void ClearFrameReady() => FrameReady = false;


    public PPU(MMU mmu)
    {
        _mmu = mmu;
        _vram = mmu.VRAM;
        _io = mmu.IO;
        _oam = mmu.OAM;
    }

    public void Tick(int cycles)
    {
        byte lcdc = _io[0x40];
        if ((lcdc & 0x80) == 0)
        {
            ly = 0;
            _mmu.IncrementLY(0);
            _cycles = 0;
            _mode = 0;
            return;
        }

        _cycles += cycles;

        if (ly < 144)
        {
            if (_cycles < 80)
                UpdateMode(2);
            else if (_cycles < 252)
            {
                if (_mode != 3)
                {
                    UpdateMode(3);
                    RenderScanline(ly);
                }
            }
            else if (_cycles < 456)
                UpdateMode(0);
        }
        else
        {
            UpdateMode(1);
        }

        if (_cycles >= 456)
        {
            _cycles -= 456;
            ly++;

            if (ly > 153)
            {
                ly = 0;
                FrameReady = true;
            }

            /*_mmu.Write(0xFF44, ly);*/
            _mmu.IncrementLY(ly);

            if (ly == 144)
            {
                _mmu.RequestInterrupt(0);
            }
        }

        byte lyc = _io[0x45];
        byte stat = _io[0x41];

        if (ly == lyc)
        {
            stat |= 0x04;

            if ((stat & 0x40) != 0 && _lastLyForStat != ly)
            {
                _mmu.RequestInterrupt(1);
                _lastLyForStat = ly;
            }
        }
        else
        {
            stat &= 0xFB;
            _lastLyForStat = 0xFF;
        }

        _io[0x41] = stat;
    }

    public void RenderScanline(byte ly)
    {
        RenderBackground(ly);
        RenderWindow(ly);
        RenderSprites(ly);
    }

    private void RenderBackground(byte ly)
    {
        byte lcdc = _io[0x40];
        if ((lcdc & 0x01) == 0) return;

        byte scx = _io[0x43];
        byte scy = _io[0x42];
        byte bgp = _io[0x47];

        int tileMapBase = (lcdc & 0x08) != 0 ? 0x1C00 : 0x1800;
        bool isUnsigned = (lcdc & 0x10) != 0;

        int bgY = (scy + ly) & 0xFF;
        int tileY = bgY >> 3;
        byte lineOffset = (byte)((bgY & 7) * 2);

        int x = 0;
        while (x < 160)
        {
            int bgX = (scx + x) & 0xFF;
            int tileX = bgX >> 3;
            int fineX = bgX & 7;

            byte tileIndex = _vram[tileMapBase + (tileY * 32) + tileX];

            int tileDataAddress = isUnsigned
                ? (tileIndex * 16)
                : (0x1000 + ((sbyte)tileIndex * 16));

            byte lo = _vram[tileDataAddress + lineOffset];
            byte hi = _vram[tileDataAddress + lineOffset + 1];

            int count = Math.Min(8 - fineX, 160 - x);
            for (int i = 0; i < count; i++)
            {
                int px = x + i;
                int bit = 7 - (fineX + i);

                int colorIndex = ((hi >> bit) & 0x01) << 1 | ((lo >> bit) & 0x01);
                _bgScanlineBuffer[px] = (byte)colorIndex;

                int colorBit = (bgp >> (colorIndex * 2)) & 0x03;
                var color = _color[colorBit];

                int index = (ly * 160 + px) * 3;
                _framebuffer[index] = color.r;
                _framebuffer[index + 1] = color.g;
                _framebuffer[index + 2] = color.b;
            }
            x += count;
        }
    }

    private void RenderWindow(byte ly)
    {
        byte lcdc = _io[0x40];
        if ((lcdc & 0x20) == 0) return;

        byte wy = _io[0x4A];
        byte wx = _io[0x4B];
        if (ly < wy || wx > 166) return;

        byte bgp = _io[0x47];
        int tileMapbase = (lcdc & 0x40) != 0 ? 0x1C00 : 0x1800;
        bool isUnsigned = (lcdc & 0x10) != 0;

        int winY = ly - wy;
        int tileY = winY >> 3;
        byte lineOffset = (byte)((winY & 7) * 2);

        int winXStart = wx - 7;
        int x = Math.Max(0, winXStart);

        while (x < 160)
        {
            int winX = x - winXStart;
            int tileX = winX >> 3;
            int fineX = winX & 7;

            byte tileIndex = _vram[tileMapbase + (tileY * 32) + tileX];

            int tileDataAddress = isUnsigned
                ? (tileIndex * 16)
                : (0x1000 + ((sbyte)tileIndex * 16));

            byte lo = _vram[tileDataAddress + lineOffset];
            byte hi = _vram[tileDataAddress + lineOffset + 1];

            int count = Math.Min(8 - fineX, 160 - x);
            for (int i = 0; i < count; i++)
            {
                int px = x + i;
                int bit = 7 - (fineX + i);

                int colorIndex = ((hi >> bit) & 0x01) << 1 | ((lo >> bit) & 0x01);
                _bgScanlineBuffer[px] = (byte)colorIndex;

                int colorBit = (bgp >> (colorIndex * 2)) & 0x03;
                var color = _color[colorBit];

                int index = (ly * 160 + px) * 3;
                _framebuffer[index] = color.r;
                _framebuffer[index + 1] = color.g;
                _framebuffer[(index + 2)] = color.b;
            }
            x += count;
        }
    }

    private void RenderSprites(byte ly)
    {
        byte lcdc = _io[0x40];

        if ((lcdc & 0x02) == 0) return;

        int spriteHeight = (lcdc & 0x04) != 0 ? 16 : 8;
        int spritesDrawn = 0;

        for (int i = 0; i < 40; i++)
        {
            int o = i * 4;
            int y = _oam[o] - 16;
            if (ly < y || ly >= y + spriteHeight) continue;

            int x = _oam[o + 1] - 8;
            byte tileIndex = _oam[o + 2];
            byte attr = _oam[o + 3];

            spritesDrawn++;
            if (spritesDrawn > 10) break;

            bool priority = (attr & 0x80) != 0;
            bool yFlip = (attr & 0x40) != 0;
            bool xFlip = (attr & 0x20) != 0;
            byte obp = _io[(attr & 0x10) != 0 ? 0x49 : 0x48];

            int line = ly - y;
            if (yFlip) line = spriteHeight - 1 - line;
            if (spriteHeight == 16) tileIndex &= 0xFE;

            int tileDataAddress = (tileIndex * 16) + (line * 2);
            byte lo = _vram[tileDataAddress];
            byte hi = _vram[tileDataAddress + 1];

            for (int px = 0; px < 8; px++)
            {
                int pixelX = x + px;
                if (pixelX < 0 || pixelX >= 160) continue;

                int bitIndex = xFlip ? px : 7 - px;

                int colorIndex = ((hi >> bitIndex) & 0x01) << 1 | ((lo >> bitIndex) & 0x01);

                if (colorIndex == 0) continue;

                if (priority && _bgScanlineBuffer[pixelX] != 0) continue;

                int colorBit = (obp >> (colorIndex * 2)) & 0x03;

                var color = _color[colorBit];
                int index = (ly * 160 + pixelX) * 3;
                _framebuffer[index] = color.r;
                _framebuffer[index + 1] = color.g;
                _framebuffer[(index + 2)] = color.b;
            }
        }
    }

    private void UpdateMode(int newMode)
    {
        if (_mode == newMode) return;
        _mode = newMode;

        byte stat = _io[0x41];
        stat = (byte)((stat & 0xFC) | (byte)(_mode & 0x03));
        _io[0x41] = stat;

        bool requestInterrupt = false;

        if (newMode == 0 && (stat & 0x08) != 0) // H-Blank
            requestInterrupt = true;
        else if (newMode == 1 && (stat & 0x10) != 0) // V-Blank
            requestInterrupt = true;
        else if (newMode == 2 && (stat & 0x20) != 0) // OAM
            requestInterrupt = true;

        if (requestInterrupt)
        {
            _mmu.RequestInterrupt(1);
        }
    }

    public void SerializeState(BinaryWriter writer)
    {
        writer.Write(_cycles);
        writer.Write(_mode);
        writer.Write(FrameReady);
        writer.Write(ly);
    }

    public void DeserializeState(BinaryReader reader)
    {
        _cycles = reader.ReadInt32();
        _mode = reader.ReadInt32();
        FrameReady = reader.ReadBoolean();
        ly = reader.ReadByte();
    }
}