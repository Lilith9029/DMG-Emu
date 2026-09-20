public class DMG
{
    public CPU _cpu;
    public Joypad _joypad;
    public MMU _mmu;
    public Timer _timer;
    public RendererDMG _renderer;
    public PPU _ppu;

    private readonly string _romPath;

    public DMG(MMU mmu, string romPath)
    {
        _mmu = mmu;
        _romPath = romPath;

        _joypad = new Joypad(_mmu);
        _mmu.Joypad = _joypad;

        _cpu = new CPU(_mmu, TickCycles);
        _timer = new Timer(_mmu);
        _ppu = new PPU(_mmu);
        _renderer = new RendererDMG();
    }

    public void TickCycles(int cycles)
    {
        _timer.Tick(cycles);
        _ppu.Tick(cycles);
        _mmu.TickCartridge(cycles);
    }

    public void Run()
    {
        const double TargetFrameTime = 1000.0 / 59.73; // ~16.75ms
        var sw = System.Diagnostics.Stopwatch.StartNew();

        while (_renderer.HandleEvents(_joypad, _romPath, this))
        {
            long startTime = sw.ElapsedMilliseconds;

            while (!_ppu.FrameReady)
            {
                int leftover = _cpu.ExecuteInstruction();

                if (leftover > 0)
                    TickCycles(leftover);

                HandleInterrupts();
            }

            _renderer.Render(_ppu.Framebuffer);
            _ppu.ClearFrameReady();

            while (sw.ElapsedMilliseconds - startTime < TargetFrameTime)
            {
                System.Threading.Thread.Sleep(0);
            }
        }

        _renderer.Destroy();
    }

    private void HandleInterrupts()
    {
        byte IF = _mmu.Read(0xFF0F);
        byte IE = _mmu.Read(0xFFFF);
        byte pending = (byte)(IF & IE & 0x1F);

        if (pending == 0) return;

        _cpu.UnHault();

        if (!_cpu._ime) return;

        _cpu._ime = false;

        ushort[] vectors = { 0x0040, 0x0048, 0x0050, 0x0058, 0x0060 };
        for (int i = 0; i < 5; i++)
        {
            if ((pending & (1 << i)) != 0)
            {
                _mmu.Write(0xFF0F, (byte)(IF & ~(1 << i)));
                int dispatchCycles = _cpu.CallInterrupt(vectors[i]);

                _timer.Tick(dispatchCycles);
                _ppu.Tick(dispatchCycles);

                return;
            }
        }
    }
}
