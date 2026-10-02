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

    public void Trigger()
    {
        Console.WriteLine("[CH3] Trigger called!"); // remmber to remove >:(
        Enabled = DacEnabled;

        int rawFreq = NR33 | ((NR34 & 0x07) << 8);
        _frequencyTimer = (2048 - rawFreq) * 2;

        if (_lengthCounter == 0)
            _lengthCounter = 256;

        _sampleIndex = 0;
    }

    public void TickTimer(int cycles)
    {
        _frequencyTimer -= cycles;

        while (_frequencyTimer <= 0)
        {
            int rawFreq = NR33 | ((NR34 & 0x07) << 8);
            _frequencyTimer += (2048 - rawFreq) * 2;
            _sampleIndex = (_sampleIndex + 1) % 32;
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

    public void WriteNR34(byte value)
    {
        NR34 = value;
        if ((value & 0x80) != 0)
            Trigger();
    }

    public void PowerOff()
    {
        NR30 = 0x00;
        NR31 = 0x00;
        NR32 = 0x00;
        NR33 = 0x00;
        NR34 = 0x00;
        Enabled = false;
    }
}