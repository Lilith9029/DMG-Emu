public class RTC
{
    public byte Seconds, Minutes, Hours;
    public ushort Days;
    public bool Halted;
    public bool DayCarry;
    
    public byte LatchedSeconds, LatchedMinutes, LatchedHours;
    public ushort LatchedDays;
    public bool LatchedDayCarry;

    private long _cycleAccumulator;
    private const long CyclesPerSecond = 4194304;

    public void Tick(int cycles)
    {
        if (Halted) return;

        _cycleAccumulator += cycles;
        while (_cycleAccumulator >= CyclesPerSecond)
        {
            _cycleAccumulator -= CyclesPerSecond;
            Seconds++;
            if (Seconds >= 60)
            {
                Seconds = 0;
                Minutes++;
                if (Minutes >= 60)
                {
                    Minutes = 0;
                    Hours++;
                    if (Hours >= 24)
                    {
                        Hours = 0;
                        Days++;
                        if (Days > 511)
                        {
                            Days = 0;
                            DayCarry = true;
                        }
                    }
                }
            }
        }
    }

    public void Latch()
    {
        LatchedSeconds = Seconds;
        LatchedMinutes = Minutes;
        LatchedHours = Hours;
        LatchedDays = Days;
        LatchedDayCarry = DayCarry;
    }

    public void SerializeState(BinaryWriter writer)
    {
        writer.Write(Seconds);
        writer.Write(Minutes);
        writer.Write(Hours);
        writer.Write(Days);
        writer.Write(Halted);
        writer.Write(DayCarry);

        writer.Write(LatchedSeconds);
        writer.Write(LatchedMinutes);
        writer.Write(LatchedHours);
        writer.Write(LatchedDays);
        writer.Write(LatchedDayCarry);

        writer.Write(_cycleAccumulator);
    }

    public void DeserializeState(BinaryReader reader)
    {
        Seconds = reader.ReadByte();
        Minutes = reader.ReadByte();
        Hours = reader.ReadByte();
        Days = reader.ReadUInt16();
        Halted = reader.ReadBoolean();
        DayCarry = reader.ReadBoolean();

        LatchedSeconds = reader.ReadByte();
        LatchedMinutes = reader.ReadByte();
        LatchedHours = reader.ReadByte();
        LatchedDays = reader.ReadUInt16();
        LatchedDayCarry = reader.ReadBoolean();

        _cycleAccumulator = reader.ReadInt64();
    }

    public void SaveBattery(BinaryWriter writer)
    {
        SerializeState(writer);
        writer.Write(DateTime.UtcNow.ToBinary());
    }

    public void LoadBattery(BinaryReader reader)
    {
        DeserializeState(reader);

        long savedTimeBinary = reader.ReadInt64();
        DateTime savedTime = DateTime.FromBinary(savedTimeBinary);

        if (!Halted)
        {
            double elapsedSeconds = (DateTime.UtcNow - savedTime).TotalSeconds;
            if (elapsedSeconds > 0)
                AddSeconds((long)elapsedSeconds);
        }
    }

    public void AddSeconds(long totalSeconds)
    {
        long totalCurrentSeconds = Seconds + Minutes * 60L + Hours * 3600L + Days * 86400L;
        totalCurrentSeconds += totalSeconds;

        Days = (ushort)((totalCurrentSeconds / 86400) % 512);
        Hours = (byte)((totalCurrentSeconds / 3600) % 24);
        Minutes = (byte)((totalCurrentSeconds / 60) % 60);
        Seconds = (byte)(totalCurrentSeconds % 60);

        if (totalCurrentSeconds >= 512 * 86400)
        {
            DayCarry = true;
        }
    }
}