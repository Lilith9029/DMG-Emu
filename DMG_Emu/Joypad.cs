public enum Key
{
    Right = 0, Left = 1, Up = 2, Down = 3,
    A = 4, B = 5, Select = 6, Start = 7
}

public class Joypad
{
    private readonly MMU _mmu;

    byte _directions = 0x0F;
    byte _buttons = 0x0F;

    byte _select = 0x30;

    public Joypad(MMU mmu)
    {
        _mmu = mmu;
    }

    public void Write(byte value)
    {
        _select = (byte)(value & 0x30);
    }

    public byte Read()
    {
        byte result = (byte)(0xC0 | _select | 0x0F);

        if ((_select & 0x10) == 0)
            result &= (byte)(_directions | 0xF0);

        if ((_select & 0x20) == 0)
            result &= (byte)(_buttons | 0xF0);

        return result;
    }

    public void KeyDown(Key key)
    {
        bool wasUnpressed = IsUnpressed(key);

        int bit = (int)key % 4;

        if ((int)key < 4)
            _directions &= (byte)~(1 << bit);
        else
            _buttons &= (byte)~(1 << bit);

        if (wasUnpressed)
            _mmu.RequestInterrupt(4);
    }

    public void KeyUp(Key key)
    {
        int bit = (int)key % 4;
        if ((int)key < 4)
            _directions |= (byte)(1 << bit);
        else
            _buttons |= (byte)(1 << bit);
    }

    bool IsUnpressed(Key key)
    {
        int bit = (int)key % 4;
        return ((int)key < 4)
            ? (_directions & (1 << bit)) != 0
            : (_buttons & (1 << bit)) != 0;
    }
}