public static class CartridgeFactory
{
    public static ICartridge LoadCartridge(byte[] romData)
    {
        byte cartType = romData[0x0147];

        byte ramSizeType = romData[0x0149];
        int ramSize = ramSizeType switch
        {
            0x02 => 8 * 1024, // 8KB
            0x03 => 32 * 1024, // 32KB
            0x04 => 128 * 1024, // 128KB
            _ => 0, // No RAM
        };

        return cartType switch
        {
            0x00 => new RomOnly(romData),
            0x01 or 0x02 or 0x03 => new MBC1(romData, ramSize),
            // More cartridge types here
            _ => throw new NotSupportedException($"Cartridge type {cartType:X2} is not supported."),
        };
    }
}