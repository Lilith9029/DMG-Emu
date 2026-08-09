public class MMU
{
    public Joypad Joypad { get; set; }

    public MMU(byte[] rom)
    {
        Array.Copy(rom, _rom, Math.Min(rom.Length, _rom.Length));
    }

    private byte[] _rom = new byte[0x8000]; // 32KB
    private byte[] _wram = new byte[0x2000]; // 8KB
    private byte[] _vram = new byte[0x2000]; // 8KB
    private byte[] _eram = new byte[0x2000]; // 8KB
    private byte[] _oam = new byte[0xA0]; // 160 bytes
    private byte[] _io = new byte[0x80]; // 128 bytes
    private byte[] _hram = new byte[0x7F]; // 127 bytes
    private byte _ie; // 1 byte

    public byte Read(ushort address)
    {
        if (address >= 0x0000 && address < 0x8000) // ROM
            return _rom[address - 0x0000];
        else if (address >= 0x8000 && address < 0xA000) // VRAM
            return _vram[address - 0x8000];
        else if (address >= 0xA000 && address < 0xC000) // External RAM
            return _eram[address - 0xA000];
        else if (address >= 0xC000 && address < 0xE000) // WRAM
            return _wram[address - 0xC000];
        else if (address >= 0xE000 && address < 0xFE00) // Echo RAM
            return _wram[address - 0xE000];
        else if (address >= 0xFE00 && address < 0xFEA0) // OAM
            return _oam[address - 0xFE00];
        else if (address >= 0xFEA0 && address < 0xFF00) // Unusable memory
            return 0xFF;
        else if (address >= 0xFF00 && address < 0xFF80) // IO Registers
        {
            if (address == 0xFF00)
                return Joypad?.Read() ?? 0xFF;

            return _io[address - 0xFF00];
        }
        else if (address >= 0xFF80 && address < 0xFFFF) // HRAM
            return _hram[address - 0xFF80];
        else if (address == 0xFFFF) // Interrupt Enable Register
            return _ie;
        return 0xFF;
    }

    public void Write(ushort address, byte value)
    {
        if (address >= 0x8000 && address < 0xA000) // VRAM
            _vram[address - 0x8000] = value;
        else if (address >= 0xC000 && address < 0xE000) // WRAM
            _wram[address - 0xC000] = value;
        else if (address >= 0xA000 && address < 0xC000) // External RAM
            _eram[address - 0xA000] = value;
        else if (address >= 0xE000 && address < 0xFE00) // Echo RAM
            _wram[address - 0xE000] = value;
        else if (address >= 0xFE00 && address < 0xFEA0) // OAM
            _oam[address - 0xFE00] = value;
        else if (address >= 0xFEA0 && address < 0xFF00) // Unusable memory
            return; // Do nothing
        else if (address >= 0xFF00 && address < 0xFF80) // IO Registers 
        {
            if (address == 0xFF00)
            {
                Joypad?.Write(value);
                return;
            }

            if (address == 0xFF44) return;

            _io[address - 0xFF00] = value;

            if (address == 0xFF46)
            {
                ushort sourceAddr = (ushort)(value << 8);
                for (int i = 0; i < 0xA0; i++)
                {
                    byte data = Read((ushort)(sourceAddr + i));
                    _oam[i] = data;
                }
            }
        }
        else if (address >= 0xFF80 && address < 0xFFFF) // HRAM
            _hram[address - 0xFF80] = value;
        else if (address == 0xFFFF) // Interrupt Enable Register
            _ie = value;
    }

    public void IncrementDiv()
    {
        _io[0x04]++; // $FF04 - $FF00 = 0x04
    }

    public void IncrementLY(byte value)
    {
        _io[0x44] = value; // $FF44 - $FF00 = 0x44  
    }

    public void RequestInterrupt(int bit)
    {
        byte flags = _io[0x0F]; // $FF0F - $FF00 = 0x0F
        flags |= (byte)(1 << bit);
        _io[0x0F] = flags;
    }   
}
