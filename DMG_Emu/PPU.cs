public class PPU
{
    private MMU _mmu;
    private byte[] _framebuffer = new byte[160 * 144 * 3];
    private byte[] _bgScanlineBuffer = new byte[160];
    private int _cycles = 0;
    private int _mode = 2; // OAM

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
    }

    public void Tick(int cycles)
    {
        byte lcdc = _mmu.Read(0xFF40);
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

        byte lyc = _mmu.Read(0xFF45);
        byte stat = _mmu.Read(0xFF41);

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

        _mmu.Write(0xFF41, stat);
    }

    public void RenderScanline(byte ly)
    {
        RenderBackground(ly);
        RenderWindow(ly);
        RenderSprites(ly);
    }

    private void RenderBackground(byte ly)
    {
        byte lcdc = _mmu.Read(0xFF40);
        if ((lcdc & 0x01) == 0) return;

        byte scx = _mmu.Read(0xFF43);
        byte scy = _mmu.Read(0xFF42);
        byte bgp = _mmu.Read(0xFF47);

        ushort tileMapBase = (ushort)((lcdc & 0x08) != 0 ? 0x9C00 : 0x9800);
        bool isUnsigned = (lcdc & 0x10) != 0;

        int bgY = (scy + ly) % 256;
        int tileY = bgY / 8;
        byte lineOffset = (byte)((bgY % 8) * 2);

        for (int x = 0; x < 160; x++)
        {
            int bgX = (scx + x) % 256;
            int tileX = bgX / 8;

            ushort tileAddress = (ushort)(tileMapBase + (tileY * 32) + tileX);
            byte tileIndex = _mmu.Read(tileAddress);

            ushort tileDataAddress = isUnsigned
                ? (ushort)(0x8000 + (tileIndex * 16))
                : (ushort)(0x9000 + ((sbyte)tileIndex * 16));

            byte lo = _mmu.Read((ushort)(tileDataAddress + lineOffset));
            byte hi = _mmu.Read((ushort)(tileDataAddress + lineOffset + 1));
            byte bitIndex = (byte)(7 - (bgX % 8));

            int colorIndex = ((hi >> bitIndex) & 0x01) << 1 | ((lo >> bitIndex) & 0x01);
            _bgScanlineBuffer[x] = (byte)colorIndex;
            int colorBit = (bgp >> (colorIndex * 2)) & 0x03;

            var color = _color[colorBit];
            int index = (ly * 160 + x) * 3;
            _framebuffer[index] = color.r;
            _framebuffer[index + 1] = color.g;
            _framebuffer[index + 2] = color.b;
        }
    }

    private void RenderWindow(byte ly)
    {
        byte lcdc = _mmu.Read(0xFF40);
        if ((lcdc & 0x20) == 0) return;

        byte wy = _mmu.Read(0xFF4A);
        byte wx = _mmu.Read(0xFF4B);

        if (ly < wy || wx > 166) return;

        ushort tileMapbase = (ushort)((lcdc & 0x40) != 0 ? 0x9C00 : 0x9800);
        bool isUnsigned = (lcdc & 0x10) != 0;

        int winY = ly - wy;
        int tileY = winY / 8;
        byte lineOffset = (byte)((winY % 8) * 2);

        int winXStart = wx - 7;

        for (int x = 0; x < 160; x++)
        {
            if (x < winXStart) continue;

            int winX = x - winXStart;
            int tileX = winX / 8;

            ushort tileAddress = (ushort)(tileMapbase + (tileY * 32) + tileX);
            byte tileIndex = _mmu.Read(tileAddress);

            ushort tileDataAddress = isUnsigned
                ? (ushort)(0x8000 + (tileIndex * 16))
                : (ushort)(0x9000 + ((sbyte)tileIndex * 16));

            byte lo = _mmu.Read((ushort)(tileDataAddress + lineOffset));
            byte hi = _mmu.Read((ushort)(tileDataAddress + lineOffset + 1));
            byte bitIndex = (byte)(7 - (winX % 8));

            int colorIndex = ((hi >> bitIndex) & 0x01) << 1 | ((lo >> bitIndex) & 0x01);
            _bgScanlineBuffer[x] = (byte)colorIndex;
            int colorBit = (_mmu.Read(0xFF47) >> (colorIndex * 2)) & 0x03;

            var color = _color[colorBit];
            int index = (ly * 160 + x) * 3;
            _framebuffer[index] = color.r;
            _framebuffer[index + 1] = color.g;
            _framebuffer[(index + 2)] = color.b;
        }
    }

    private void RenderSprites(byte ly)
    {
        byte lcdc = _mmu.Read(0xFF40);

        if ((lcdc & 0x02) == 0) return;

        int spriteHeight = (lcdc & 0x04) != 0 ? 16 : 8;
        int spritesDrawn = 0;

        for (int i = 0; i < 40; i++)
        {
            int y = _mmu.Read((ushort)(0xFE00 + (i * 4))) - 16;
            int x = _mmu.Read((ushort)(0xFE00 + (i * 4) + 1)) - 8;

            byte tileIndex = _mmu.Read((ushort)(0xFE00 + (i * 4) + 2));
            byte attr = _mmu.Read((ushort)(0xFE00 + (i * 4) + 3));

            if (ly < y || ly >= y + spriteHeight) continue;

            spritesDrawn++;
            if (spritesDrawn > 10) break;

            bool priority = (attr & 0x80) != 0;
            bool yFlip = (attr & 0x40) != 0;
            bool xFlip = (attr & 0x20) != 0;
            ushort pallete = (ushort)((attr & 0x10) != 0 ? 0xFF49 : 0xFF48);

            int line = ly - y;
            if (yFlip) line = spriteHeight - 1 - line;
            if (spriteHeight == 16) tileIndex &= 0xFE;
            ushort tileDataAddress = (ushort)(0x8000 + (tileIndex * 16) + (line * 2));

            byte lo = _mmu.Read(tileDataAddress);
            byte hi = _mmu.Read((ushort)(tileDataAddress + 1));

            for (int px = 0; px < 8; px++)
            {
                int pixelX = x + px;
                if (pixelX < 0 || pixelX >= 160) continue;

                int bitIndex = xFlip ? px : 7 - px;

                int colorIndex = ((hi >> bitIndex) & 0x01) << 1 | ((lo >> bitIndex) & 0x01);

                if (colorIndex == 0) continue;

                if (priority && _bgScanlineBuffer[pixelX] != 0) continue;

                int colorBit = (_mmu.Read(pallete) >> (colorIndex * 2)) & 0x03;

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

        byte stat = _mmu.Read(0xFF41);
        stat = (byte)((stat & 0xFC) | (byte)(_mode & 0x03));
        _mmu.Write(0xFF41, stat);

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

    // State Serialization and Deserialization
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