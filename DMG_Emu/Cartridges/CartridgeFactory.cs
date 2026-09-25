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

        bool hasRtc = cartType == 0x0F || cartType == 0x10;
        bool hasBattery = cartType is 0x03 or 0x0F or 0x10 or 0x13 or 0x1B or 0x1E;

        return cartType switch
        {
            0x00 => new RomOnly(romData),
            0x01 or 0x02 or 0x03 => new MBC1(romData, ramSize, hasBattery),
            0x05 or 0x06 => new MBC2(romData, hasBattery),
            0x0F or 0x10 or 0x11 or 0x12 or 0x13 => new MBC3(romData, ramSize, hasRtc, hasBattery),
            0x19 or 0x1A or 0x1B or 0x1C or 0x1D or 0x1E => new MBC5(romData, ramSize, hasBattery),
            // More cartridge types here
            _ => throw new NotSupportedException($"Cartridge type {cartType:X2} is not supported."),
        };
    }
}