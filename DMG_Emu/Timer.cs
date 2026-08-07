public class Timer
{
    public MMU _mmu;
    public int _divCounter = 0;
    public int _timaCounter = 0;

    private readonly int[] TAC_MODES = { 1024, 16, 64, 256 };

    public Timer(MMU mmu) { _mmu = mmu; }

    public void Tick(int cycles)
    {
        // DIV
        _divCounter += cycles;
        while (_divCounter >= 256)
        {
            _divCounter -= 256;
            _mmu.IncrementDiv();
        }

        // TIMA
        byte tac = _mmu.Read(0xFF07);
        bool enabled = (tac & 0x04) != 0;

        if (enabled)
        {
            _timaCounter += cycles;
            int threshold = TAC_MODES[tac & 0x03];

            while (_timaCounter >= threshold)
            {
                _timaCounter -= threshold;
                byte tima = _mmu.Read(0xFF05);

                if (tima == 0xFF)
                {
                    _mmu.Write(0xFF05, _mmu.Read(0xFF06));
                    _mmu.RequestInterrupt(2);
                }
                else
                {
                    _mmu.Write(0xFF05, (byte)(tima + 1));
                }
            }
        }
    }
}