public interface ICartridge
{
    byte ReadROM(ushort address);

    void WriteROM(ushort address, byte value);

    byte ReadRAM(ushort address);

    void WriteRAM(ushort address, byte value);
}