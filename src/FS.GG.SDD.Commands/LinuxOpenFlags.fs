namespace FS.GG.SDD.Commands

open System.Runtime.InteropServices

/// Linux `open(2)` flag values for the no-follow, held-parent filesystem seams.
///
/// `O_DIRECTORY` and `O_NOFOLLOW` are not the same numbers on every Linux architecture: the x86 family
/// uses the asm-generic values, while arm and arm64 use `0x4000` and `0x8000`, where x86 numbers
/// `0x10000` and `0x20000` mean `O_DIRECT` and `O_LARGEFILE`. `O_CLOEXEC` and `O_NONBLOCK` agree on
/// every architecture listed here. The process architecture selects the table, because it fixes the
/// system call ABI the process speaks. An architecture outside the table has no flags: callers must
/// refuse rather than guess, since a wrong value silently drops the no-follow guarantee.
module internal LinuxOpenFlags =
    type Flags =
        {
            Directory: int
            NoFollow: int
            CloseOnExec: int
            NonBlock: int
        }

    // asm-generic/fcntl.h as used by x86 and x86-64.
    let x86Family =
        {
            Directory = 0x10000
            NoFollow = 0x20000
            CloseOnExec = 0x80000
            NonBlock = 0x800
        }

    // arch/arm/include/uapi/asm/fcntl.h and arch/arm64/include/uapi/asm/fcntl.h.
    let armFamily =
        {
            Directory = 0x4000
            NoFollow = 0x8000
            CloseOnExec = 0x80000
            NonBlock = 0x800
        }

    /// The flag table for an architecture, or `None` when it is not covered.
    let forArchitecture (architecture: Architecture) : Flags option =
        match architecture with
        | Architecture.X64
        | Architecture.X86 -> Some x86Family
        | Architecture.Arm64
        | Architecture.Arm
        | Architecture.Armv6 -> Some armFamily
        | _ -> None

    /// The flags for this process, or `None` on an uncovered architecture.
    let current: Flags option = forArchitecture RuntimeInformation.ProcessArchitecture

    /// `O_DIRECTORY | O_NOFOLLOW | O_CLOEXEC`.
    let directoryOpen (flags: Flags) =
        flags.Directory ||| flags.NoFollow ||| flags.CloseOnExec

    /// `O_NOFOLLOW | O_CLOEXEC | O_NONBLOCK`, which cannot hang on a FIFO swapped into the leaf.
    let fileOpen (flags: Flags) =
        flags.NoFollow ||| flags.CloseOnExec ||| flags.NonBlock

    /// `O_WRONLY | O_CREAT | O_EXCL | O_NOFOLLOW | O_CLOEXEC`; the first three agree on every architecture here.
    let exclusiveCreate (flags: Flags) =
        1 ||| 64 ||| 128 ||| flags.NoFollow ||| flags.CloseOnExec
