using SDL2;

public class APU : IDisposable
{
    private MMU _mmu;

    public CH2 ch2 = new CH2();

    private bool _lastDivBit4 = false;
    private int _frameSequencerStep = 0;

    private double _sampleAccumulator = 0.0;
    private const double CyclesPerSample = 4194304.0 / AudioDevice.SampleRate;

    private readonly short[] _sampleBuffer = new short[2048];
    private int _sampleBufferIndex = 0;
    private readonly AudioDevice _audioDevice = new AudioDevice();

    public APU(MMU mmu)
    {
        _mmu = mmu;
    }

    public void Tick(int cycles)
    {
        for (int i = 0; i < cycles; i += 4)
        {
            byte div = _mmu.Read(0xFF04);
            bool bit4 = (div & 0x10) != 0;

            if (_lastDivBit4 && !bit4)
                StepFrameSequencer();

            _lastDivBit4 = bit4;
        }
        ch2.TickTimer(cycles);

        _sampleAccumulator += cycles;
        while (_sampleAccumulator >= CyclesPerSample)
        {
            _sampleAccumulator -= CyclesPerSample;
            GenerateAudioSample();
        }
    }

    public void StepFrameSequencer()
    {
        switch (_frameSequencerStep)
        {
            case 0:
            case 2:
            case 4:
            case 6:
                ch2.TickLength();
                break;
            case 7:
                ch2.TickEnvelope();
                break;
        }
        _frameSequencerStep = (_frameSequencerStep + 1) & 7;
    }

    public void GenerateAudioSample()
    {
        byte s2 = ch2.GetSample();

        /*float sample = s2 / 15.0f;
        short pcm = (short)((sample * 2.0f - 1.0f) * 16000);*/

        short pcm = ch2.Enabled ? (short)((s2 / 15.0f * 2.0f - 1.0f) * 16000) : (short)0;

        _sampleBuffer[_sampleBufferIndex++] = pcm; // Left
        _sampleBuffer[_sampleBufferIndex++] = pcm; // Right

        if (_sampleBufferIndex >= _sampleBuffer.Length)
        {
            _audioDevice.QueueAudio(_sampleBuffer, _sampleBufferIndex);
            _sampleBufferIndex = 0;
        }
    }

    public byte ReadRegister(ushort address)
    {
        return address switch
        {
            0xFF16 => (byte)(ch2.NR21 | 0x3F),
            0xFF17 => ch2.NR22,
            0xFF18 => 0xFF,
            0xFF19 => (byte)(ch2.NR24 | 0xBF),
            _ => 0xFF
        };
    }

    public void WriteRegister(ushort address, byte value)
    {
        switch (address)
        {
            case 0xFF16:
                ch2.WriteLength(value);
                break;
            case 0xFF17:
                ch2.WriteEnvelope(value);
                break;
            case 0xFF18:
                ch2.NR23 = value;
                break;
            case 0xFF19:
                ch2.NR24 = value;
                if ((value & 0x80) != 0)
                    ch2.Trigger();
                break;

        }
    }

    public void Dispose()
    {
        _audioDevice.Dispose();
    }
}