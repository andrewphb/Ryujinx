using ChocolArm64.Events;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace ChocolArm64.State
{
    public class AThreadState
    {
        internal const int LRIndex = 30;
        internal const int ZRIndex = 31;

        internal const int ErgSizeLog2 = 4;
        internal const int DczSizeLog2 = 4;

        private const int MinInstForCheck = 4000000;

        internal AExecutionMode ExecutionMode;

        //AArch32 state.
        public uint R0,  R1,  R2,  R3,
                    R4,  R5,  R6,  R7,
                    R8,  R9,  R10, R11,
                    R12, R13, R14, R15;

        public bool Thumb;

        //AArch64 state.
        public ulong X0,  X1,  X2,  X3,  X4,  X5,  X6,  X7,
                     X8,  X9,  X10, X11, X12, X13, X14, X15,
                     X16, X17, X18, X19, X20, X21, X22, X23,
                     X24, X25, X26, X27, X28, X29, X30, X31;

        public Vector128<float> V0,  V1,  V2,  V3,  V4,  V5,  V6,  V7,
                                V8,  V9,  V10, V11, V12, V13, V14, V15,
                                V16, V17, V18, V19, V20, V21, V22, V23,
                                V24, V25, V26, V27, V28, V29, V30, V31;

        public bool Overflow;
        public bool Carry;
        public bool Zero;
        public bool Negative;

        public bool Running { get; set; }
        public int  Core    { get; set; }

        private bool Interrupted;

        private int SyncCount;

        public long TpidrEl0 { get; set; }
        public long Tpidr    { get; set; }

        public int Fpcr { get; set; }
        public int Fpsr { get; set; }

        public int Psr
        {
            get
            {
                return (Negative ? (int)APState.N : 0) |
                       (Zero     ? (int)APState.Z : 0) |
                       (Carry    ? (int)APState.C : 0) |
                       (Overflow ? (int)APState.V : 0);
            }
        }

        public uint CtrEl0   => 0x8444c004;
        public uint DczidEl0 => 0x00000004;

        public ulong CntfrqEl0 { get; set; }
        public ulong CntpctEl0
        {
            get
            {
                double Ticks = TickCounter.ElapsedTicks * HostTickFreq;

                return (ulong)(Ticks * CntfrqEl0);
            }
        }

        public event EventHandler<EventArgs>               Interrupt;
        public event EventHandler<AInstExceptionEventArgs> Break;
        public event EventHandler<AInstExceptionEventArgs> SvcCall;
        public event EventHandler<AInstUndefinedEventArgs> Undefined;

        private static Stopwatch TickCounter;

        private static double HostTickFreq;

        static AThreadState()
        {
            HostTickFreq = 1.0 / Stopwatch.Frequency;

            TickCounter = new Stopwatch();

            TickCounter.Start();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool Synchronize(int BbWeight)
        {
            //Firing a interrupt frequently is expensive, so we only
            //do it after a given number of instructions has executed.
            SyncCount += BbWeight;

            if (SyncCount >= MinInstForCheck)
            {
                CheckInterrupt();
            }

            return Running;
        }

        internal void RequestInterrupt()
        {
            Interrupted = true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void CheckInterrupt()
        {
            SyncCount = 0;

            if (Interrupted)
            {
                Interrupted = false;

                Interrupt?.Invoke(this, EventArgs.Empty);
            }
        }

        internal void OnBreak(long Position, int Imm)
        {
            Break?.Invoke(this, new AInstExceptionEventArgs(Position, Imm));
        }

        internal void OnSvcCall(long Position, int Imm)
        {
            SvcCall?.Invoke(this, new AInstExceptionEventArgs(Position, Imm));
        }

        internal void OnUndefined(long Position, int RawOpCode)
        {
            Undefined?.Invoke(this, new AInstUndefinedEventArgs(Position, RawOpCode));
        }

        internal bool GetFpcrFlag(FPCR Flag)
        {
            return (Fpcr & (1 << (int)Flag)) != 0;
        }

        internal void SetFpsrFlag(FPSR Flag)
        {
            Fpsr |= 1 << (int)Flag;
        }

        internal ARoundMode FPRoundingMode()
        {
            return (ARoundMode)((Fpcr >> (int)FPCR.RMode) & 3);
        }
    }
}
