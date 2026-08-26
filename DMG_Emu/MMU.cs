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

            return _io[address - 0xFF00];
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