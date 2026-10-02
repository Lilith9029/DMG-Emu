public class CH4
{
    public byte NR41 = 0xFF; // Length
    public byte NR42 = 0x00; // Envelope
    public byte NR43 = 0x00; // Polynomial Counter
    public byte NR44 = 0xBF; // Trigger + Length enable

    public bool Enabled { get; private set; } = false;
    public bool DacEnabled => (NR42 & 0xF8) != 0;

    private static readonly byte[] Divisors = { 8, 16, 32, 48, 64, 80, 96, 112 };

    private ushort _lfsr = 0x7FFF;
    private int _frequencyTimer = 0;

    private int _lengthCounter = 0;
    public bool LengthEnable => (NR44 & 0x40) != 0;

    private int _currentVolume = 0;
    private int _envelopeTimer = 0;

    public void Trigger()
    {
        Console.WriteLine("[CH4] Trigger called!"); // remmber to remove >:(
        Enabled = DacEnabled;

        _lfsr = 0x7FFF;
        _frequencyTimer = GetTimerPeriod();

        if (_lengthCounter == 0)
            _lengthCounter = 64;

        _currentVolume = NR42 >> 4;
        _envelopeTimer = NR42 & 0x07;
    }

    private int GetTimerPeriod()
    {
        int divisorCode = NR43 & 0x07;
        int clockShift = (NR43 >> 4) & 0x0F;
        return Divisors[divisorCode] << clockShift;
    }

    public void TickTimer(int cycles)
    {
        _frequencyTimer -= cycles;

        while (_frequencyTimer <= 0)
        {
            _frequencyTimer += GetTimerPeriod();

            int xorResult = (_lfsr & 0x01) ^ ((_lfsr >> 1) & 0x01);
            _lfsr = (ushort)((_lfsr >> 1) | (xorResult << 14));

            if ((NR43 & 0x08) != 0)
                _lfsr = (ushort)((_lfsr & ~0x40) | (xorResult << 6));
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
        int period = NR42 & 0x07;
        if (period == 0) return;

        _envelopeTimer--;
        if (_envelopeTimer <= 0)
        {
            _envelopeTimer = period;
            bool increase = (NR42 & 0x08) != 0;
            if (increase && _currentVolume < 15)
                _currentVolume++;
            else if (!increase && _currentVolume > 0)
                _currentVolume--;
        }
    }

    public byte GetSample()
    {
        if (!Enabled) return 0;

        return ((_lfsr & 1) == 0) ? (byte)_currentVolume : (byte)0;
    }

    public void WriteEnvelope(byte value)
    {
        NR42 = value;
        if (!DacEnabled)
            Enabled = false;
    }

    public void WriteLength(byte value)
    {
        NR41 = value;
        _lengthCounter = 64 - (value & 0x3F);
    }

    public void WriteNR44(byte value)
    {
        NR44 = value;
        if ((value & 0x80) != 0)
            Trigger();
    }

    public void PowerOff()
    {
        NR41 = 0x00;
        NR42 = 0x00;
        NR43 = 0x00;
        NR44 = 0x00;
        Enabled = false;
    }
}