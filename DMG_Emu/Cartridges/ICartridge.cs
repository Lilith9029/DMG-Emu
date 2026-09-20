public interface ICartridge
{
    byte ReadROM(ushort address);
    void WriteROM(ushort address, byte value);
    byte ReadRAM(ushort address);
    void WriteRAM(ushort address, byte value);

    byte[] GetSRAM();
    void SetSRAM(byte[] saveData);
    bool HasBattery();

    void Tick(int cycles);

    void SerializeState(BinaryWriter writer);
    void DeserializeState(BinaryReader reader);
}