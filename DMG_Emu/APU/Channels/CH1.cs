public class CH1
{
    public byte NR10 = 0x80; // Sweep
    public byte NR11 = 0xBF; // Duty + length data
    public byte NR12 = 0xF3; // Envelope
    public byte NR13 = 0xFF;  // Frequency low
    public byte NR14 = 0xBF;  // Frequency high + Trigger + Length enable

    public bool Enabled { get; private set; } = false;
    public bool DacEnabled => (NR12 & 0xF8) != 0;

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
    public bool LengthEnable => (NR14 & 0x40) != 0;

    private int _currentVolume = 0;
    private int _envelopeTimer = 0;

    private int _shadowFrequency = 0;
    private int _sweepTimer = 0;
    private bool _sweepEnabled = false;
    private bool _sweepNegateUsed = false;

    public int SweepPeriod => (NR10 >> 4) & 0x07;
    public bool SweepNegate => (NR10 & 0x08) != 0;
    public int SweepShift => NR10 & 0x07;

    public void Trigger()
    {
        Console.WriteLine("[CH1] Trigger called!"); // remmber to remove >:(
        Enabled = DacEnabled;

        int rawFreq = NR13 | ((NR14 & 0x07) << 8);
        _frequencyTimer = (2048 - rawFreq) * 4;

        if (_lengthCounter == 0)
            _lengthCounter = 64;

        _currentVolume = NR12 >> 4;
        _envelopeTimer = NR12 & 0x07;

        _shadowFrequency = rawFreq;
        _sweepTimer = SweepPeriod != 0 ? SweepPeriod : 8;
        _sweepEnabled = SweepPeriod != 0 || SweepShift != 0;
        _sweepNegateUsed = false;

        if (SweepShift != 0)
            CalculateSweepFrequency();
    }

    public void TickTimer(int cycles)
    {
        _frequencyTimer -= cycles;

        while (_frequencyTimer <= 0)
        {
            int rawFreq = NR13 | ((NR14 & 0x07) << 8);
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
        int period = NR12 & 0x07;
        if (period == 0) return;

        _envelopeTimer--;
        if (_envelopeTimer <= 0)
        {
            _envelopeTimer = period;
            bool increase = (NR12 & 0x08) != 0;
            if (increase && _currentVolume < 15)
                _currentVolume++;
            else if (!increase && _currentVolume > 0)
                _currentVolume--;
        }
    }

    public void TickSweep()
    {
        if (_sweepTimer > 0)
            _sweepTimer--;

        if (_sweepTimer == 0)
        {
            _sweepTimer = SweepPeriod != 0 ? SweepPeriod : 8;

            if (_sweepEnabled && SweepPeriod != 0)
            {
                int newFreq = CalculateSweepFrequency();

                if (newFreq <= 2047 && SweepShift != 0)
                {
                    _shadowFrequency = newFreq;
                    NR13 = (byte)(newFreq & 0xFF);
                    NR14 = (byte)((NR14 & 0xF8) | ((newFreq >> 8) & 0x07));

                    CalculateSweepFrequency();
                }
            }
        }
    }

    private int CalculateSweepFrequency()
    {
        int newFreq = _shadowFrequency >> SweepShift;

        if (SweepNegate)
        {
            newFreq = _shadowFrequency - newFreq;
            _sweepNegateUsed = true;
        }
        else
        {
            newFreq = _shadowFrequency + newFreq;
        }

        if (newFreq > 2047)
        {
            Enabled = false;
            _sweepEnabled = false;
        }

        return newFreq;
    }

    public byte GetSample()
    {
        if (!Enabled) return 0;

        int duty = (NR11 >> 6) & 0x03;
        return DutyPatterns[duty][_dutyStep] != 0 ? (byte)_currentVolume : (byte)0;
    }

    public void WriteEnvelope(byte value)
    {
        NR12 = value;
        if (!DacEnabled)
            Enabled = false;
    }

    public void WriteLength(byte value)
    {
        NR11 = value;
        _lengthCounter = 64 - (value & 0x3F);
    }

    public void WriteSweep(byte value)
    {
        bool oldNegate = SweepNegate;
        NR10 = value;

        if (oldNegate && !SweepNegate && _sweepNegateUsed)
            Enabled = false;
    }

    public void PowerOff()
    {
        NR10 = 0x00;
        NR11 = 0x00;
        NR12 = 0x00;
        NR13 = 0x00;
        NR14 = 0x00;
        Enabled = false;
    }
}