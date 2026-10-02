public class CH2
{
    public byte NR21 = 0x3F; // Duty + length data
    public byte NR22 = 0x00; // Envelope
    public byte NR23 = 0xFF;  // Frequency low
    public byte NR24 = 0xBF;  // Frequency high + Trigger + Length enable

    public bool Enabled { get; private set; } = false;
    public bool DacEnabled => (NR22 & 0xF8) != 0;

    private static readonly byte[][] DutyPatterns =
    {
        new byte[] {0, 0, 0, 0, 0, 0, 0, 1}, // 12.5% 
        new byte[] {1, 0, 0, 0, 0, 0, 0, 1}, // 25%
        new byte[] {1, 0, 0, 0, 0, 1, 1, 1}, // 50%
        new byte[] {0, 1, 1, 1, 1, 1, 1, 0}, // 75%
    };

    private int _dutyStep = 0;
    private int _frequencyTimer = 0;

    private int _lengthCounter = 0;
    public bool LengthEnable => (NR24 & 0x40) != 0;

    private int _currentVolume = 0;
    private int _envelopeTimer = 0;

    public void Trigger()
    {
        Console.WriteLine("[CH2] Trigger called!"); // remmber to remove >:(
        Enabled = DacEnabled;

        int rawFreq = NR23 | ((NR24 & 0x07) << 8);
        _frequencyTimer = (2048 - rawFreq) * 4;

        if (_lengthCounter == 0)
            _lengthCounter = 64;

        _currentVolume = NR22 >> 4;
        _envelopeTimer = NR22 & 0x07;
    }

    public void TickTimer(int cycles)
    {
        _frequencyTimer -= cycles;

        while (_frequencyTimer <= 0)
        {
            int rawFreq = NR23 | ((NR24 & 0x07) << 8);
            _frequencyTimer += (2048 - rawFreq) * 4;
            _dutyStep = (_dutyStep + 1) & 7;
        }
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

    public void TickEnvelope()
    {
        int period = NR22 & 0x07;
        if (period == 0) return;

        _envelopeTimer--;
        if (_envelopeTimer <= 0)
        {
            _envelopeTimer = period;
            bool increase = (NR22 & 0x08) != 0;
            if (increase && _currentVolume < 15)
                _currentVolume++;
            else if (!increase && _currentVolume > 0)
                _currentVolume--;
        }
    }
    public byte GetSample()
    {
        if (!Enabled) return 0;

        int duty = (NR21 >> 6) & 0x03;
        return DutyPatterns[duty][_dutyStep] != 0 ? (byte)_currentVolume : (byte)0;
    }

    public void WriteEnvelope(byte value)
    {
        NR22 = value;
        if (!DacEnabled)
            Enabled = false;
    }

    public void WriteLength(byte value)
    {
        NR21 = value;
        _lengthCounter = 64 - (value & 0x3F);
    }

    public void PowerOff()
    {
        NR21 = 0x00;
        NR22 = 0x00;
        NR23 = 0x00;
        NR24 = 0x00;
        Enabled = false;
    }
}