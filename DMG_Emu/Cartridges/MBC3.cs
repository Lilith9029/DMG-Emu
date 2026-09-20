public class MBC3 : ICartridge
{
    private readonly byte[] _rom;
    private readonly byte[] _ram;
    private readonly bool _hasBattery;
    private readonly bool _hasRtc;
    public readonly RTC? _rtc;

    private bool _ramEnabled = false;
    private byte _romBank = 1; // Default to bank 1
    private byte _ramBankOrRtcSelect;
    private byte _latchState = 0xFF;

    public MBC3(byte[] romData, int ramSize, bool hasRtc, bool hasBattery)
    {
        _rom = romData;
        _ram = new byte[ramSize];
        _hasBattery = hasBattery;
        _hasRtc = hasRtc;
        _rtc = hasRtc ? new RTC() : null;
    }

    public byte ReadROM(ushort address)
    {
        if (address < 0x4000)
        {
            return _rom[address];
        }
        else if (address < 0x8000)
        {
            int bank = _romBank % (_rom.Length / 0x4000);
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
            _romBank = (byte)(value & 0x7F);
            if (_romBank == 0) _romBank = 1;
        }
        else if (address < 0x6000)
        {
            _ramBankOrRtcSelect = value;
        }
        else
        {
            if (_latchState == 0 && value == 1)
            {
                _rtc?.Latch();
            }
            _latchState = value;
        }
    }

    public byte ReadRAM(ushort address)
    {
        if (!_ramEnabled) return 0xFF;

        if (_ramBankOrRtcSelect <= 0x03)
        {
            if (_ram.Length == 0) return 0xFF;

            int numRamBanks = _ram.Length / 0x2000;
            int bank = _ramBankOrRtcSelect % numRamBanks;
            int offset = address - 0xA000;
            return _ram[(bank * 0x2000) + offset];
        }

        if (_rtc != null && _ramBankOrRtcSelect >= 0x08 && _ramBankOrRtcSelect <= 0x0C)
        {
            if (_ramBankOrRtcSelect == 0x0C)
            {
                byte dayHigh = 0;
                if ((_rtc.LatchedDays & 0x100) != 0) dayHigh |= 0x01;
                if (_rtc.Halted) dayHigh |= 0x40;
                if (_rtc.LatchedDayCarry) dayHigh |= 0x80;
                return dayHigh;
            }

            return _ramBankOrRtcSelect switch
            {
                0x08 => _rtc.LatchedSeconds,
                0x09 => _rtc.LatchedMinutes,
                0x0A => _rtc.LatchedHours,
                0x0B => (byte)(_rtc.LatchedDays & 0xFF),
                _ => 0xFF
            };
        }

        return 0xFF;
    }

    public void WriteRAM(ushort address, byte value)
    {
        if (!_ramEnabled) return;

        if (_ramBankOrRtcSelect <= 0x03)
        {
            if (_ram.Length == 0) return;

            int numRamBanks = _ram.Length / 0x2000;
            int bank = _ramBankOrRtcSelect % numRamBanks;
            int offset = address - 0xA000;
            _ram[(bank * 0x2000) + offset] = value;
        }

        if (_rtc != null && _ramBankOrRtcSelect >= 0x08 && _ramBankOrRtcSelect <= 0x0C)
        {
            switch (_ramBankOrRtcSelect)
            {
                case 0x08:
                    _rtc.Seconds = value;
                    break;
                case 0x09:
                    _rtc.Minutes = value;
                    break;
                case 0x0A:
                    _rtc.Hours = value;
                    break;
                case 0x0B:
                    _rtc.Days = (ushort)((_rtc.Days & 0x100) | value);
                    break;
                case 0x0C:
                    _rtc.Days = (ushort)((_rtc.Days & 0xFF) | ((value & 0x01) << 8));
                    _rtc.Halted = (value & 0x40) != 0;
                    _rtc.DayCarry = (value & 0x80) != 0;
                    break;
            }
        }
    }

    public byte[] GetSRAM()
    {
        using var  ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write(_ram);

        if (_hasRtc && _rtc != null)
            _rtc.SaveBattery(writer);

        return ms.ToArray();
    }

    public void SetSRAM(byte[] saveData)
    {
        if (saveData == null || saveData.Length == 0) return;

        int bytesToCopy = Math.Min(_ram.Length, saveData.Length);
        Array.Copy(saveData, 0, _ram, 0, bytesToCopy);

        if (_hasRtc && _rtc != null && saveData.Length > _ram.Length)
        {
            try
            {
                using var ms = new MemoryStream(saveData);
                ms.Position = _ram.Length;
                using var reader = new BinaryReader(ms);
                _rtc.LoadBattery(reader);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading RTC save data: {ex.Message}");
            }
        }
    }

    public bool HasBattery()
    {
        return _hasBattery;
    }

    public void Tick(int cycles)
    {
        _rtc?.Tick(cycles);
    }

    public void SerializeState(BinaryWriter writer)
    {
        writer.Write(_ramEnabled);
        writer.Write(_romBank);
        writer.Write(_ramBankOrRtcSelect);
        writer.Write(_latchState);

        writer.Write(_ram.Length);
        writer.Write(_ram);

        writer.Write(_hasRtc);
        _rtc?.SerializeState(writer);
    }

    public void DeserializeState(BinaryReader reader)
    {
        _ramEnabled = reader.ReadBoolean();
        _romBank = reader.ReadByte();
        _ramBankOrRtcSelect = reader.ReadByte();
        _latchState = reader.ReadByte();

        int ramLength = reader.ReadInt32();
        byte[] ramData = reader.ReadBytes(ramLength);
        Array.Copy(ramData, _ram, Math.Min(_ram.Length, ramData.Length));

        bool hasRtc = reader.ReadBoolean();
        if (hasRtc && _rtc != null)
            _rtc.DeserializeState(reader);
    }
}