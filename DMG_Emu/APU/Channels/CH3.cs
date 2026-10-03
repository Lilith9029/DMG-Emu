public class CH3
{
    public byte NR30 = 0x7F; // DAC enable
    public byte NR31 = 0xFF; // Length
    public byte NR32 = 0x9F; // Volume
    public byte NR33 = 0xFF; // Frequency low
    public byte NR34 = 0xBF; // Frequency high + Trigger + Length enable

    public byte[]WaveRam = new byte[16];

    public bool Enabled { get; private set; } = false;
    public bool DacEnabled => (NR30 & 0x80) != 0;

    private int _sampleIndex = 0;
    private int _frequencyTimer = 0;

    private int _lengthCounter = 0;
    public bool LengthEnable => (NR34 & 0x40) != 0;

    // (TriggerDelay, CorruptWindow)(AccessLo, AccessHi)
    // This is the list of all combinations that can pass tests 09, 10 and 12 at the same time
    // (3, 1)(2, 3 | 2, 4 | 2, 5 | 2, 6 | 2, 7 | 2, 8)
    // (3, 1)(3, 3 | 3, 4 | 3, 5 | 3, 6 | 3, 7 | 3, 8)
    // (3, 2)(2, 3 | 2, 4 | 2, 5 | 2, 6 | 2, 7 | 2, 8)
    // (3, 2)(3, 3 | 3, 4 | 3, 5 | 3, 6 | 3, 7 | 3, 8)
    // (4, 0)(1, 2 | 1, 3 | 1, 4 | 1, 5 | 1, 6 | 1, 7 | 1, 8)
    // (4, 0)(2, 2 | 2, 3 | 2, 4 | 2, 5 | 2, 6 | 2, 7 | 2, 8)
    // (4, 1)(1, 2 | 1, 3 | 1, 4 | 1, 5 | 1, 6 | 1, 7 | 1, 8)
    // (4, 1)(2, 2 | 2, 3 | 2, 4 | 2, 5 | 2, 6 | 2, 7 | 2, 8)
    public const int TriggerDelay = 3; 
    public const int CorruptWindow = 1;
    public const int AccessLo = 2;
    public const int AccessHi = 3;

    private int _sinceRead = 1000;

    private bool WaveAccessible =>
        Enabled && _sinceRead >= AccessLo && _sinceRead <= AccessHi;

    public void Trigger(bool nextStepOdd)
    {
        bool wasOn = Enabled;

        if (wasOn && _sinceRead <= CorruptWindow)
        {
            int pos = _sampleIndex >> 1;
            if (pos < 4)
                WaveRam[0] = WaveRam[pos];
            else
            {
                int b = pos & ~3;
                for (int i = 0; i < 4; i++)
                    WaveRam[i] = WaveRam[b + i];
            }
        }

        Enabled = DacEnabled;

        int rawFreq = NR33 | ((NR34 & 0x07) << 8);
        _frequencyTimer = (2048 - rawFreq) * 2 + TriggerDelay;

        if (_lengthCounter == 0)
        {
            _lengthCounter = 256;
            if (LengthEnable && nextStepOdd)
                _lengthCounter--;
        }

        _sampleIndex = 0;
        _sinceRead = 1000;
    }

    public void TickTimer(int cycles)
    {
        if (!Enabled) return;

        while (cycles > 0)
        {
            int step = Math.Min(cycles, _frequencyTimer);
            _frequencyTimer -= step;
            cycles -= step;
            _sinceRead = Math.Min(_sinceRead + step, 100);

            if (_frequencyTimer == 0)
            {
                int rawFreq = NR33 | ((NR34 & 0x07) << 8);
                _frequencyTimer = (2048 - rawFreq) * 2;
                _sampleIndex = (_sampleIndex + 1) & 31; // 32
                _sinceRead = 0;
            }
        }

        /*_frequencyTimer -= cycles;

        while (_frequencyTimer <= 0)
        {
            int rawFreq = NR33 | ((NR34 & 0x07) << 8);
            _frequencyTimer += (2048 - rawFreq) * 2;
            _sampleIndex = (_sampleIndex + 1) % 32;
        }*/
    }

    public void TickLength()
    {
        if (LengthEnable && _lengthCounter > 0)
        {
            _lengthCounter--;
            if (_lengthCounter == 0)
                Enabled = false;
        }
    }

    public byte GetSample()
    {
        if (!Enabled) return 0;

        byte sampleValue = WaveRam[_sampleIndex / 2];
        byte sample = (_sampleIndex % 2 == 0)
            ? (byte)(sampleValue >> 4)
            : (byte)(sampleValue & 0x0F);

        int volumeShift = (NR32 >> 5) & 0x03;
        return volumeShift switch
        {
            1 => sample, // 100%
            2 => (byte)(sample >> 1), // 50%
            3 => (byte)(sample >> 2), // 25%
            _ => 0, // Mute
        };
    }

    public byte ReadWaveRam(int offset)
    {
        if (Enabled)
            return WaveAccessible ? WaveRam[_sampleIndex >> 1] : (byte)0xFF;
        return WaveRam[offset];
    }

    public void WriteWaveRam(int offset, byte value)
    {
        if (Enabled)
        {
            if (WaveAccessible)
                WaveRam[_sampleIndex >> 1] = value;
            return;
        }
        WaveRam[offset] = value;
    }

    public void WriteLength(byte value)
    {
        NR31 = value;
        _lengthCounter = 256 - value;
    }

    public void WriteNR30(byte value)
    {
        NR30 = value;
        if (!DacEnabled)
            Enabled = false;
    }

    public void WriteNR34(byte value, bool nextStepOdd)
    {
        bool oldLen = LengthEnable;
        NR34 = value;
        bool trigger = (value & 0x80) != 0;

        if (!oldLen && LengthEnable && nextStepOdd && _lengthCounter > 0)
        {
            _lengthCounter--;
            if (_lengthCounter == 0)
                Enabled = false;
        }

        if (trigger)
            Trigger(nextStepOdd);
    }

    public void PowerOff()
    {
        NR30 = 0x00;
        NR32 = 0x00;
        NR33 = 0x00;
        NR34 = 0x00;
        Enabled = false;
    }
}