using DMG_Emu.Services;

namespace DMG_Emu;

internal class Program
{
    static void Main(string[] args)
    {
        // Game Test ROMs
        // MBC0
        /*Tetris (Japan) (En).gb*/

        // MBC1
        /*Legend of Zelda, The - Link's Awakening (USA, Europe) (Rev 2).gb*/

        // MBC2
        /*Kirby's Pinball Land (USA).gb*/
        /*Metroid II - Return of Samus (World).gb*/
        /*Golf(USA).gb*/

        // MBC3
        /*Pokemon - Blue Version (USA, Europe) (SGB Enhanced).gb*/
        /*Pokemon - Silver Version (USA, Europe) (SGB Enhanced) (GB Compatible).gbc*/
        /*Pokemon - Gold Version(USA, Europe)(SGB Enhanced)(GB Compatible).gbc*/

        // MBC5
        /*Legend of Zelda, The - Link's Awakening DX (USA, Europe) (Rev 2) (SGB Enhanced) (GB Compatible).gbc*/
        /*Pokemon Trading Card Game(USA) (SGB Enhanced).gbc*/

        /*Pokemon - Yellow Version - Special Pikachu Edition (USA, Europe) (CGB+SGB Enhanced).gb"*/

        string romPath = "Game/Kirby's Pinball Land (USA).gb";

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