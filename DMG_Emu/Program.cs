using DMG_Emu.Services;

namespace DMG_Emu;

internal class Program
{
    static void Main(string[] args)
    {
        // Game Test ROMs
        /*Pokemon - Yellow Version - Special Pikachu Edition (USA, Europe) (CGB+SGB Enhanced).gb"*/
        /*Pokemon - Blue Version (USA, Europe) (SGB Enhanced).gb*/
        /*Legend of Zelda, The - Link's Awakening (USA, Europe) (Rev 2).gb*/
        /*Tetris (Japan) (En).gb*/
        /*Pokemon - Silver Version (USA, Europe) (SGB Enhanced) (GB Compatible).gbc*/

        string romPath = "Game/Pokemon - Silver Version (USA, Europe) (SGB Enhanced) (GB Compatible).gbc";
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
        SaveManager.SaveData(romPath, cartridge);
    }
}