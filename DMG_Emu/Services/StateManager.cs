namespace DMG_Emu.Services;

internal class StateManager
{
    private const uint MagicNumber = 0x316C314C;
    private const ushort Version = 1;

    public static bool SaveState(string romPath, DMG dmg, int slot = 1)
    {
        string statePath = Path.ChangeExtension(romPath, $".ss{slot}");

        try
        {
            using var stream = File.Create(statePath);
            using var writer = new BinaryWriter(stream);

            writer.Write(MagicNumber);
            writer.Write(Version);

            dmg._cpu.SerializeState(writer);
            dmg._mmu.SerializeState(writer);
            dmg._timer.SerializeState(writer);
            dmg._ppu.SerializeState(writer);

            Console.WriteLine($"State saved to {slot}: {statePath}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save state: {ex.Message}");
            return false;
        }
    }

    public static bool LoadState(string romPath, DMG dmg, int slot = 1)
    {
        string statePath = Path.ChangeExtension(romPath, $".ss{slot}");

        if (!File.Exists(statePath))
        {
            Console.WriteLine($"Save state file not found: {statePath}");
            return false;
        }

        try
        {
            using var stream = File.OpenRead(statePath);
            using var reader = new BinaryReader(stream);

            uint magic = reader.ReadUInt32();
            ushort version = reader.ReadUInt16();

            if (magic != MagicNumber || version != Version)
            {
                Console.WriteLine($"Invalid save state file: {statePath}");
                return false;
            }

            dmg._cpu.DeserializeState(reader);
            dmg._mmu.DeserializeState(reader);
            dmg._timer.DeserializeState(reader);
            dmg._ppu.DeserializeState(reader);

            Console.WriteLine($"State loaded successfully from {slot}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load state: {ex.Message}");
            return false;
        }
    }
}
