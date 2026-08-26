using DMG_Emu.Services;

namespace DMG_Emu;

internal class Program
{
    static void Main(string[] args)
    {
        // Game Test ROMs
        /*byte[] rom = File.ReadAllBytes("Pokemon - Yellow Version - Special Pikachu Edition (USA, Europe) (CGB+SGB Enhanced).gb");*/
        /*byte[] rom = File.ReadAllBytes("Pokemon - Blue Version (USA, Europe) (SGB Enhanced).gb");*/
        /*byte[] rom = File.ReadAllBytes("Legend of Zelda, The - Link's Awakening (USA, Europe) (Rev 2).gb");*/
        /*byte[] rom = File.ReadAllBytes("Tetris (Japan) (En).gb");*/

        // CPU Test ROMs
        /*byte[] rom = File.ReadAllBytes("01-special.gb");*/
        /*byte[] rom = File.ReadAllBytes("02-interrupts.gb");*/
        /*byte[] rom = File.ReadAllBytes("03-op sp,hl.gb");*/
        /*byte[] rom = File.ReadAllBytes("04-op r,imm.gb");*/
        /*byte[] rom = File.ReadAllBytes("05-op rp.gb");*/
        /*byte[] rom = File.ReadAllBytes("06-ld r,r.gb");*/
        /*byte[] rom = File.ReadAllBytes("07-jr,jp,call,ret,rst.gb");*/
        /*byte[] rom = File.ReadAllBytes("08-misc instrs.gb");*/
        /*byte[] rom = File.ReadAllBytes("09-op r,r.gb");*/
        /*byte[] rom = File.ReadAllBytes("10-bit ops.gb");*/
        /*byte[] rom = File.ReadAllBytes("11-op a,(hl).gb");*/
        /*byte[] rom = File.ReadAllBytes("cpu_instrs.gb");*/

        string romPath = "Legend of Zelda, The - Link's Awakening (USA, Europe) (Rev 2).gb";

        if (!File.Exists(romPath))
        {
            Console.WriteLine($"ROM file not found: {romPath}");
            return;
        }

        byte[] rom = File.ReadAllBytes(romPath);

        ICartridge cartridge = CartridgeFactory.LoadCartridge(rom);
        SaveManager.LoadSaveData(romPath, cartridge);

        AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
        {
            SaveManager.SaveData(romPath, cartridge);
        };

        MMU mmu = new MMU(cartridge);
        DMG dmg = new DMG(mmu, romPath);

        dmg.Run();
    }
}