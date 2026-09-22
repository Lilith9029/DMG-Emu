public class MBC5 : ICartridge
{
    private readonly byte[] _rom;
    private readonly byte[] _ram;
    private readonly bool _hasBattery;

    private bool _ramEnabled = false;
    private byte _romBankLow = 1;
    private byte _romBankHigh = 0;
    private byte _ramBank = 0;

    public MBC5(byte[] romData, int ramSize, bool hasBattery)
    {
        _rom = romData;
        _ram = new byte[ramSize];
        _hasBattery = hasBattery;
    }

    public byte ReadROM(ushort address)
    {
        if (address < 0x4000)
        {
            return _rom[address];
        }
        else if (address < 0x8000)
        {
            int bank = (_romBankHigh << 8) | _romBankLow;
            bank %= (_rom.Length / 0x4000);

            int offset = address - 0x4000;
            return _rom[(bank * 0x4000) + offset];
        }

        return 0xFF;
    }

    public void WriteROM(ushort address, byte value)
    {
        if (address < 0x2000)
        {
            _ramEnabled = (value & 0x0F) == 0x0A;
        }
        else if (address < 0x3000)
        {
            _romBankLow = value;
        }
        else if (address < 0x4000)
        {
            _romBankHigh = (byte)(value & 0x01);
        }
        else if (address >= 0x4000 && address < 0x6000)
        {
            _ramBank = (byte)(value & 0x0F);
        }
    }

    public byte ReadRAM(ushort address)
    {
        if (_ramEnabled && _ram.Length > 0)
        {
            int bank = _ramBank % (_ram.Length / 0x2000);
            int offset = address - 0xA000;
            return _ram[(bank * 0x2000) + offset];
        }

        return 0xFF;
    }

    public void WriteRAM(ushort address, byte value)
    {
        if (_ramEnabled && _ram.Length > 0)
        {
            int bank = _ramBank % (_ram.Length / 0x2000);
            int offset = address - 0xA000;
            _ram[(bank * 0x2000) + offset] = value;
        }
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
        writer.Write(_romBankLow);
        writer.Write(_romBankHigh);
        writer.Write(_ramBank);

        writer.Write(_ram.Length);
        writer.Write(_ram);
    }

    public void DeserializeState(BinaryReader reader)
    {
        _ramEnabled = reader.ReadBoolean();
        _romBankLow = reader.ReadByte();
        _romBankHigh = reader.ReadByte();
        _ramBank = reader.ReadByte();

        int ramLength = reader.ReadInt32();
        byte[] ramData = reader.ReadBytes(ramLength);
        Array.Copy(ramData, _ram, Math.Min(ramLength, _ram.Length));
    }
}