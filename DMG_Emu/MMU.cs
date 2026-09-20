public class MMU
{
    public Joypad? Joypad { get; set; }

    private ICartridge _cartridge;

    private byte[] _wram = new byte[0x2000]; // 8KB
    private byte[] _vram = new byte[0x2000]; // 8KB
    private byte[] _oam = new byte[0xA0]; // 160 bytes
    private byte[] _io = new byte[0x80]; // 128 bytes
    private byte[] _hram = new byte[0x7F]; // 127 bytes
    private byte _ie; // 1 byte

    public MMU(ICartridge cartridge)
    {
        _cartridge = cartridge;

        // Joypad & Serial
        _io[0x00] = 0xCF; // P1/JOYP
        _io[0x01] = 0x00; // SB
        _io[0x02] = 0x7E; // SC

        // Timer
        _io[0x04] = 0xAB; // DIV
        _io[0x05] = 0x00; // TIMA
        _io[0x06] = 0x00; // TMA
        _io[0x07] = 0xF8; // TAC

        // Interrupts Flags
        _io[0x0F] = 0xE1;

        // Sound Registers
        _io[0x10] = 0x80; // NR10
        _io[0x11] = 0xBF; // NR11
        _io[0x12] = 0xF3; // NR12
        _io[0x14] = 0xBF; // NR14
        _io[0x16] = 0x3F; // NR21
        _io[0x17] = 0x00; // NR22
        _io[0x19] = 0xBF; // NR24
        _io[0x1A] = 0x7F; // NR30
        _io[0x1B] = 0xFF; // NR31
        _io[0x1C] = 0x9F; // NR32
        _io[0x1E] = 0xBF; // NR34
        _io[0x20] = 0xFF; // NR41
        _io[0x21] = 0x00; // NR42
        _io[0x22] = 0x00; // NR43
        _io[0x23] = 0xBF; // NR44
        _io[0x24] = 0x77; // NR50
        _io[0x25] = 0xF3; // NR51
        _io[0x26] = 0xF1; // NR52

        // LCD & PPU Registers
        _io[0x40] = 0x91; // LCDC
        _io[0x41] = 0x85; // STAT
        _io[0x42] = 0x00; // SCY
        _io[0x43] = 0x00; // SCX
        _io[0x45] = 0x00; // LYC
        _io[0x47] = 0xFC; // BGP
        _io[0x48] = 0xFF; // OBP0
        _io[0x49] = 0xFF; // OBP1
        _io[0x4A] = 0x00; // WY
        _io[0x4B] = 0x00; // WX
    }

    public byte Read(ushort address)
    {
        if (address < 0x8000) // ROM
            return _cartridge.ReadROM(address);

        if (address < 0xA000) // VRAM
            return _vram[address - 0x8000];

        if (address < 0xC000) // External RAM
            return _cartridge.ReadRAM(address);

        if (address < 0xE000) // WRAM
            return _wram[address - 0xC000];

        if (address < 0xFE00) // Echo RAM
            return _wram[address - 0xE000];

        if (address < 0xFEA0) // OAM
            return _oam[address - 0xFE00];

        if (address < 0xFF00) // Unusable memory
            return 0xFF;

        if (address < 0xFF80) // IO Registers
        {
            if (address == 0xFF00)
                return Joypad?.Read() ?? 0xFF;

            byte value = _io[address - 0xFF00];
            if (address == 0xFF0F)
                value |= 0xE0;

            return value;

            /*return _io[address - 0xFF00];*/
        }

        if (address <= 0xFFFE) // HRAM
            return _hram[address - 0xFF80];

        if (address == 0xFFFF) // Interrupt Enable Register
            return _ie;

        return 0xFF;
    }

    public void Write(ushort address, byte value)
    {
        if (address < 0x8000) // ROM
        {
            _cartridge.WriteROM(address, value);
            return;
        }

        if (address < 0xA000) // VRAM
        {
            _vram[address - 0x8000] = value;
            return;
        }

        if (address < 0xC000) // External RAM
        {
            _cartridge.WriteRAM(address, value);
            return;
        }

        if (address < 0xE000) // WRAM
        {
            _wram[address - 0xC000] = value;
            return;
        }

        if (address < 0xFE00) // Echo RAM
        {
            _wram[address - 0xE000] = value;
            return;
        }

        if (address < 0xFEA0) // OAM
        {
            _oam[address - 0xFE00] = value;
            return;
        }

        if (address < 0xFF00) // Unusable memory
            return;

        if (address < 0xFF80) // IO Registers
        {
            if (address == 0xFF00)
            {
                Joypad?.Write(value);
                return;
            }

            if (address == 0xFF04)
            {
                _io[0x04] = 0;
                return;
            }

            if (address == 0xFF44) return;

            _io[address - 0xFF00] = value;

            // Trigger OAM DMA Transfer
            if (address == 0xFF46)
            {
                ushort sourceAddr = (ushort)(value << 8);
                for (int i = 0; i < 0xA0; i++)
                {
                    byte data = Read((ushort)(sourceAddr + i));
                    _oam[i] = data;
                }
            }
            return;
        }

        if (address <= 0xFFFE) // HRAM
        {
            _hram[address - 0xFF80] = value;
            return;
        }

        if (address == 0xFFFF) // Interrupt Enable Register
        {
            _ie = value;
        }
    }

    public void IncrementDiv()
    {
        _io[0x04]++;
    }

    public void IncrementLY(byte value)
    {
        _io[0x44] = value;
    }

    public void RequestInterrupt(int bit)
    {
        byte flags = _io[0x0F];
        flags |= (byte)(1 << bit);
        _io[0x0F] = flags;
    }

    public void TickCartridge(int cycles)
    {
        _cartridge.Tick(cycles);
    }

    // Serialization and Deserialization
    public void SerializeState(BinaryWriter writer)
    {
        writer.Write(_wram);
        writer.Write(_vram);
        writer.Write(_oam);
        writer.Write(_io);
        writer.Write(_hram);
        _cartridge.SerializeState(writer);
    }

    public void DeserializeState(BinaryReader reader)
    {
        reader.Read(_wram, 0, _wram.Length);
        reader.Read(_vram, 0, _vram.Length);
        reader.Read(_oam, 0, _oam.Length);
        reader.Read(_io, 0, _io.Length);
        reader.Read(_hram, 0, _hram.Length);
        _cartridge.DeserializeState(reader);
    }
}