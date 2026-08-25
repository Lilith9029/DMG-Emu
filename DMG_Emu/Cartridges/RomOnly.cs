public class RomOnly : ICartridge
{
    private readonly byte[] _rom;

    public RomOnly(byte[] romData)
    {
        _rom = romData;
    }

    public byte ReadROM(ushort address)
    {
        if (address < _rom.Length)
            return _rom[address];

        return 0xFF;
    }

    public void WriteROM(ushort address, byte value)
    {
        //do nothing
    }

    public byte ReadRAM(ushort address)
    {
        return 0xFF;
    }

    public void WriteRAM(ushort address, byte value)
    {
        // do nothing
    }
}