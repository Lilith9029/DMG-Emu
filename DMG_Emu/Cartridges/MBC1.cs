public class MBC1 : ICartridge
{
    private readonly byte[] _rom;
    private readonly byte[] _ram;

    private bool _ramEnabled = false;
    private byte _romBankLower = 1; // Default to bank 1
    private byte _ramBankOrRomBankHigh;
    private byte _bankingMode = 0; // 0 = ROM mode, 1 = RAM mode

    public MBC1(byte[] romData, int ramSize)
    {
        _rom = romData;
        _ram = new byte[ramSize];
    }

    public byte ReadROM(ushort address)
    {
        if (address < 0x4000)
        {
            int bank = (_bankingMode == 1) ? (_ramBankOrRomBankHigh << 5) % (_rom.Length / 0x4000) : 0;

            return _rom[(bank * 0x4000) + address];
        }

        if (address < 0x8000)
        {
            int bank = (_ramBankOrRomBankHigh << 5) | _romBankLower;
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
        else if (address < 0x4000)
        {
            _romBankLower = (byte)(value & 0x1F);

            if (_romBankLower == 0) _romBankLower = 1;
        }
        else if (address < 0x6000)
        {
            _ramBankOrRomBankHigh = (byte)(value & 0x03);

        }
        else if (address < 0x8000)
        {
            _bankingMode = (byte)(value & 0x01);
        }
    }

    public byte ReadRAM(ushort address)
    {
        if (!_ramEnabled || _ram == null || _ram.Length == 0)
            return 0xFF;

        int numRamBanks = _ram.Length / 0x2000;
        int bank = (_bankingMode == 1) ? (_ramBankOrRomBankHigh % Math.Max(1, numRamBanks)) : 0;

        int offset = address - 0xA000;
        return _ram[(bank * 0x2000) + offset];
    }

    public void WriteRAM(ushort address, byte value)
    {
        if (!_ramEnabled || _ram == null || _ram.Length == 0)
            return;

        int numRamBanks = _ram.Length / 0x2000;
        int bank = (_bankingMode == 1) ? (_ramBankOrRomBankHigh % Math.Max(1, numRamBanks)) : 0;

        int offset = address - 0xA000;
        _ram[(bank * 0x2000) + offset] = value;
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
        return _ram != null && _ram.Length > 0;
    }

    public void SerializeState(BinaryWriter writer)
    {
        writer.Write(_ramEnabled);
        writer.Write(_romBankLower);
        writer.Write(_ramBankOrRomBankHigh);
        writer.Write(_bankingMode);

        writer.Write(_ram.Length);
        writer.Write(_ram);
    }

    public void DeserializeState(BinaryReader reader)
    {
        _ramEnabled = reader.ReadBoolean();
        _romBankLower = reader.ReadByte();
        _ramBankOrRomBankHigh = reader.ReadByte();
        _bankingMode = reader.ReadByte();

        int ramLength = reader.ReadInt32();
        byte[] ramData = reader.ReadBytes(ramLength);
        Array.Copy(ramData, _ram, Math.Min(ramLength, _ram.Length));
    }
}