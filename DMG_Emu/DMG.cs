using System.Diagnostics;
using System.Runtime.InteropServices;

public class DMG
{
    public CPU _cpu;
    public Joypad _joypad;
    public MMU _mmu;
    public Timer _timer;
    public RendererDMG _renderer;
    public PPU _ppu;
    public APU _apu;

    private static readonly ushort[] Vectors = { 0x0040, 0x0048, 0x0050, 0x0058, 0x0060 };
    private readonly string _romPath;
    public string? NextRom => _renderer.DroppedRom;

    [DllImport("winmm.dll")] private static extern uint timeBeginPeriod(uint ms);
    [DllImport("winmm.dll")] private static extern uint timeEndPeriod(uint ms);

    public DMG(MMU mmu, string romPath)
    {
        _mmu = mmu;
        _romPath = romPath;

        _joypad = new Joypad(_mmu);
        _mmu.Joypad = _joypad;

        _apu = new APU(_mmu);
        _mmu.APU = _apu;

        _timer = new Timer(_mmu);
        _mmu.Timer = _timer;

        _cpu = new CPU(_mmu, TickCycles);
        _ppu = new PPU(_mmu);
        _renderer = new RendererDMG();
    }

    public void TickCycles(int cycles)
    {
        _timer.Tick(cycles);
        _apu.Tick(cycles);
        _ppu.Tick(cycles);
        _mmu.TickCartridge(cycles);
    }

    public void Run()
    {
        const double fps = 4194304.0 / 70224.0; // ~59.7275
        long frameTicks = (long)(Stopwatch.Frequency / fps);
        long next = Stopwatch.GetTimestamp();
        long spinTicks = Stopwatch.Frequency * 1 / 1000;

        // Test
        int frames = 0;
        long lastReport = Stopwatch.GetTimestamp();

        if (OperatingSystem.IsWindows())
            timeBeginPeriod(1);

        while (_renderer.HandleEvents(_joypad, _romPath, this))
        {
            while (!_ppu.FrameReady)
            {
                int leftover = _cpu.ExecuteInstruction();

                if (leftover > 0)
                    TickCycles(leftover);

                HandleInterrupts();
            }

            _renderer.Render(_ppu.Framebuffer);
            _ppu.ClearFrameReady();

            long curr = Stopwatch.GetTimestamp();
            if (curr - next > 5 * frameTicks)
                next = curr;

            next += frameTicks;
            while (true)
            {
                long remain = next - Stopwatch.GetTimestamp();

                if (remain <= 0) break;

                if (remain > spinTicks)
                    Thread.Sleep(1);
                else
                    Thread.SpinWait(30);
            }

            // Test
            frames++;
            long now = Stopwatch.GetTimestamp();
            if (now - lastReport >= Stopwatch.Frequency)
            {
                double secs = (now - lastReport) / (double)Stopwatch.Frequency;
                Console.WriteLine($"Framerate: {frames / secs:F2}");
                frames = 0;
                lastReport = now;
            } 
        }

        _renderer.Destroy();
        if (OperatingSystem.IsWindows())
            timeEndPeriod(1);
        _apu.Dispose();
    }

    private void HandleInterrupts()
    {
        byte IF = _mmu.InterruptFlags;
        byte IE = _mmu.InterruptEnable;
        byte pending = (byte)(IF & IE & 0x1F);

        if (pending == 0) return;

        _cpu.UnHault();

        if (!_cpu._ime) return;

        _cpu._ime = false;

        for (int i = 0; i < 5; i++)
        {
            if ((pending & (1 << i)) != 0)
            {
                _mmu.Write(0xFF0F, (byte)(IF & ~(1 << i)));
                int dispatchCycles = _cpu.CallInterrupt(Vectors[i]);

                _timer.Tick(dispatchCycles);
                _ppu.Tick(dispatchCycles);
                _apu.Tick(dispatchCycles);

                return;
            }
        }
    }
}
