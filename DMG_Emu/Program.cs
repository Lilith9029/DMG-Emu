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

        string romPath = "mem-timming - 01/03-modify_timing.gb";
        /*cpu_instrs/cpu_instrs.gb
        mem-timming - 01/01-read_timing.gb
        mem-timming - 01/02-write_timing.gb
        mem-timming - 01/03-modify_timing.gb
        mem-timming - 01/mem_timing.gb*/

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