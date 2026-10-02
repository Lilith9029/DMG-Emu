using SDL2;

public class APU : IDisposable
{
    private MMU _mmu;

    public CH1 ch1 = new CH1();
    public CH2 ch2 = new CH2();
    public CH3 ch3 = new CH3();

    public byte NR50 = 0x77;
    public byte NR51 = 0xF3;
    private bool _power = true;

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
        ch1.TickTimer(cycles);
        ch2.TickTimer(cycles);
        ch3.TickTimer(cycles);

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
            case 4:
                ch1.TickLength();
                ch2.TickLength();
                ch3.TickLength();
                break;
            case 2:
            case 6:
                ch1.TickLength();
                ch1.TickSweep();
                ch2.TickLength();
                ch3.TickLength();
                break;
            case 7:
                ch1.TickEnvelope();
                ch2.TickEnvelope();
                break;
        }
        _frameSequencerStep = (_frameSequencerStep + 1) & 7;
    }

    public void GenerateAudioSample()
    {
        byte s1 = ch1.GetSample();
        byte s2 = ch2.GetSample();
        byte s3 = ch3.GetSample();

        float sample = (s1 + s2 + s3) / 45.0f;
        short pcm = (short)((sample * 2.0f - 1.0f) * 16000);

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
            0xFF10 => (byte)(ch1.NR10 | 0x80),
            0xFF11 => (byte)(ch1.NR11 | 0x3F),
            0xFF12 => ch1.NR12,
            0xFF13 => 0xFF,
            0xFF14 => (byte)(ch1.NR14 | 0xBF),
            0xFF16 => (byte)(ch2.NR21 | 0x3F),
            0xFF17 => ch2.NR22,
            0xFF18 => 0xFF,
            0xFF19 => (byte)(ch2.NR24 | 0xBF),
            0xFF1A => (byte)(ch3.NR30 | 0x7F),
            0xFF1B => 0xFF,
            0xFF1C => (byte)(ch3.NR32 | 0x9F),
            0xFF1D => 0xFF,
            0xFF1E => (byte)(ch3.NR34 | 0xBF),
            0xFF24 => NR50,
            0xFF25 => NR51,
            0xFF26 => (byte)((_power ? 0x80 : 0x00) | 0x70
            | (ch1.Enabled ? 0x01 : 0x00)
            | (ch2.Enabled ? 0x02 : 0x00)
            | (ch3.Enabled ? 0x04 : 0x00)),
            _ => 0xFF
        };
    }

    public void WriteRegister(ushort address, byte value)
    {
        if (!_power && address != 0xFF26)
            return;

        switch (address)
        {
            case 0xFF10:
                ch1.WriteSweep(value);
                break;
            case 0xFF11:
                ch1.WriteLength(value);
                break;
            case 0xFF12:
                ch1.WriteEnvelope(value);
                break;
            case 0xFF13:
                ch1.NR13 = value;
                break;
            case 0xFF14:
                ch1.WriteNR14(value);
                break;
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
                ch2.WriteNR24(value);
                break;
            case 0xFF1A:
                ch3.WriteNR30(value);
                break;
            case 0xFF1B:
                ch3.WriteLength(value);
                break;
            case 0xFF1C:
                ch3.NR32 = value;
                break;
            case 0xFF1D:
                ch3.NR33 = value;
                break;
            case 0xFF1E:
                ch3.WriteNR34(value);
                break;
            case 0xFF24:
                NR50 = value;
                break;
            case 0xFF25:
                NR51 = value;
                break;
            case 0xFF26:
                bool newPower = (value & 0x80) != 0;

                if (_power && !newPower)
                {
                    ch1.PowerOff();
                    ch2.PowerOff();
                    ch3.PowerOff();

                    NR50 = 0x00;
                    NR51 = 0x00;
                }
                else if (!_power && newPower)
                {
                    _frameSequencerStep = 0;
                }

                _power = newPower;
                break;
        }
    }

    public void Dispose()
    {
        _audioDevice.Dispose();
    }
}