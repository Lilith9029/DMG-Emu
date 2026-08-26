namespace DMG_Emu.Services;

public static class SaveManager
{
    public static void LoadSaveData(string romPath, ICartridge cartridge)
    {
        if (cartridge == null || !cartridge.HasBattery()) return;

        string savePath = Path.ChangeExtension(romPath, ".sav");

        if (File.Exists(savePath))
        {
            try
            {
                byte[] saveData = File.ReadAllBytes(savePath);
                cartridge.SetSRAM(saveData);
                Console.WriteLine($"Loaded save data from {savePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading save data: {ex.Message}");
            }
        }
    }

    public static void SaveData(string romPath, ICartridge cartridge)
    {
        if (cartridge == null || !cartridge.HasBattery()) return;

        string savePath = Path.ChangeExtension(romPath, ".sav");
        byte[] saveData = cartridge.GetSRAM();

        try
        {
            File.WriteAllBytes(savePath, saveData);
            Console.WriteLine($"Saved data to {savePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving data: {ex.Message}");
        }
    }
}