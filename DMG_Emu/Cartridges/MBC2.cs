public class MBC2: ICartridge
{
    private readonly byte[] _rom;
    private readonly byte[] _ram;
    private readonly bool _hasBattery;

    private bool _ramEnabled;
    private byte _romBank;

    public MBC2(byte[] romData, bool hasBattery)
    {
        _rom = romData;
        _ram = new byte[512];
        _romBank = 1; // Default to bank 1
        _hasBattery = hasBattery;
    }

    public byte ReadROM(ushort address)
    {
        if (address < 0x4000)
        {
            return _rom[address];
        }

        if (address < 0x8000)
        {
            int bank = _romBank % (_rom.Length / 0x4000);
            int offset = address - 0x4000;
            return _rom[(bank * 0x4000) + offset];
        }

        return 0xFF;
    }

    public void WriteROM(ushort address, byte value)
    {
        if (address < 0x4000)
        {
            bool isRomBankSelect = (address & 0x0100) != 0;

            if (isRomBankSelect)
            {
                _romBank = (byte)(value & 0x0F);
                if (_romBank == 0) _romBank = 1;
            }
            else
            {
                _ramEnabled = (value & 0x0F) == 0x0A;
            }
        }
    }

    public byte ReadRAM(ushort address)
    {
        if (!_ramEnabled) return 0xFF;

        int offset = (address - 0xA000) & 0x1FF;
        return (byte)(_ram[offset] | 0xF0);
    }

    public void WriteRAM(ushort address, byte value)
    {
        if (!_ramEnabled) return;

        int offset = (address - 0xA000) & 0x1FF;
        _ram[offset] = (byte)(value & 0x0F);
    }

    public byte[] GetSRAM()
    {
        return _ram != null ? (byte[])_ram.Clone() : Array.Empty<byte>();
    }

    public void SetSRAM(byte[] saveData)
    {
        if (_ram == null || saveData == null) return;

        int bytesToCopy = Math.Min(_ram.Length, saveData.Length);
        Array.Copy(saveData, _ram, bytesToCopy);
    }

    public bool HasBattery()
    {
        return _hasBattery;
    }

    public void Tick(int cycles)
    {
        // do nothing
    }

    public void SerializeState(BinaryWriter writer)
    {
        writer.Write(_ramEnabled);
        writer.Write(_romBank);

        writer.Write(_ram.Length);
        writer.Write(_ram);
    }

    public void DeserializeState(BinaryReader reader)
    {
        _ramEnabled = reader.ReadBoolean();
        _romBank = reader.ReadByte();

        int ramLength = reader.ReadInt32();
        byte[] ramData = reader.ReadBytes(ramLength);
        Array.Copy(ramData, _ram, Math.Min(ramLength, _ram.Length));
    }
}