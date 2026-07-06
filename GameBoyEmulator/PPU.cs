public class PPU
{
    private MMU _mmu;
    private byte[] _framebuffer = new byte[160 * 144 * 3];
    private int _cycles = 0;
    private int _mode = 2; // OAM

    public bool FrameReady { get; private set; }
    private byte ly = 0;

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
            stat |= 0x04;
        else
            stat &= 0xFB;

        _mmu.Write(0xFF41, stat);
    }

    public void RenderScanline(byte ly)
    {
        RenderBackground(ly);
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

        for (int x = 0; x< 160; x++)
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
            int colorBit = (bgp >> (colorIndex * 2)) & 0x03;

            var color = _color[colorBit];
            int index = (ly * 160 + x) * 3;
            _framebuffer[index] = color.r;
            _framebuffer[index + 1] = color.g;
            _framebuffer[index + 2] = color.b;
        }
    }

    private void UpdateMode(int newMode)
    {
        if (_mode == newMode) return;
        _mode = newMode;

        byte stat = _mmu.Read(0xFF41);
        stat = (byte)((stat & 0xFC) | (byte)(_mode & 0x03));
        _mmu.Write(0xFF41, stat);
    }
}