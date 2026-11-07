namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open System.Runtime.InteropServices
open FS.GG.SDD.Commands
open Xunit

/// Linux open(2) flag numbers differ by architecture. x86 numbers used on arm64 silently turn
/// O_DIRECTORY|O_NOFOLLOW into O_DIRECT|O_LARGEFILE. These controls pin the table against the kernel
/// uapi values (written in octal, as the headers do) and check the selected table on the running host.
module LinuxOpenFlagsTests =
    [<DllImport("libc", SetLastError = true, EntryPoint = "open")>]
    extern int private nativeOpen(string path, int flags)

    [<DllImport("libc", SetLastError = true, EntryPoint = "close")>]
    extern int private nativeClose(int fd)

    // Independent of the module under test: uapi values per architecture family.
    let private x86Directory = 0o200000
    let private x86NoFollow = 0o400000
    let private armDirectory = 0o40000
    let private armNoFollow = 0o100000
    let private armDirect = 0o200000 // O_DIRECT on arm/arm64 (x86's O_DIRECTORY number)
    let private armLargeFile = 0o400000 // O_LARGEFILE on arm/arm64 (x86's O_NOFOLLOW number)
    let private cloexec = 0o2000000
    let private nonblock = 0o4000

    [<Fact>]
    let ``x86 family uses the asm-generic numbers`` () =
        for architecture in [ Architecture.X64; Architecture.X86 ] do
            let flags = (LinuxOpenFlags.forArchitecture architecture).Value
            Assert.Equal(x86Directory, flags.Directory)
            Assert.Equal(x86NoFollow, flags.NoFollow)
            Assert.Equal(cloexec, flags.CloseOnExec)
            Assert.Equal(nonblock, flags.NonBlock)

    [<Fact>]
    let ``arm family uses the arm numbers and never the x86 ones`` () =
        for architecture in [ Architecture.Arm64; Architecture.Arm; Architecture.Armv6 ] do
            let flags = (LinuxOpenFlags.forArchitecture architecture).Value
            Assert.Equal(armDirectory, flags.Directory)
            Assert.Equal(armNoFollow, flags.NoFollow)
            Assert.Equal(cloexec, flags.CloseOnExec)
            Assert.Equal(nonblock, flags.NonBlock)

            // The three composed open masks must not carry O_DIRECT or O_LARGEFILE on arm.
            for mask in
                [
                    LinuxOpenFlags.directoryOpen flags
                    LinuxOpenFlags.fileOpen flags
                    LinuxOpenFlags.exclusiveCreate flags
                ] do
                Assert.Equal(0, mask &&& (armDirect ||| armLargeFile))

    [<Fact>]
    let ``composed masks keep their access, create and exclusive bits on every covered architecture`` () =
        for architecture in
            [
                Architecture.X64
                Architecture.X86
                Architecture.Arm64
                Architecture.Arm
                Architecture.Armv6
            ] do
            let flags = (LinuxOpenFlags.forArchitecture architecture).Value
            Assert.Equal(flags.Directory ||| flags.NoFollow ||| flags.CloseOnExec, LinuxOpenFlags.directoryOpen flags)
            Assert.Equal(flags.NoFollow ||| flags.CloseOnExec ||| flags.NonBlock, LinuxOpenFlags.fileOpen flags)
            let create = LinuxOpenFlags.exclusiveCreate flags
            Assert.Equal(1 ||| 0o100 ||| 0o200, create &&& (1 ||| 0o100 ||| 0o200))
            Assert.NotEqual(0, create &&& flags.NoFollow)

    [<Fact>]
    let ``an architecture outside the table has no flags and so must refuse`` () =
        for architecture in
            [
                Architecture.RiscV64
                Architecture.S390x
                Architecture.LoongArch64
                Architecture.Ppc64le
                Architecture.Wasm
            ] do
            Assert.True((LinuxOpenFlags.forArchitecture architecture).IsNone)

    [<Fact>]
    let ``the running host selects the table for its own process architecture`` () =
        let expected = LinuxOpenFlags.forArchitecture RuntimeInformation.ProcessArchitecture
        Assert.Equal(expected, LinuxOpenFlags.current)

    // Executed only on the host that runs the suite: proves the selected numbers mean
    // O_DIRECTORY and O_NOFOLLOW to this kernel, whatever its architecture.
    [<Fact>]
    let ``host kernel treats the selected flags as directory and no-follow`` () =
        if OperatingSystem.IsLinux() && (LinuxOpenFlags.current).IsSome then
            let flags = LinuxOpenFlags.current.Value

            let root =
                Path.Combine(Path.GetTempPath(), "sdd-open-flags-" + Guid.NewGuid().ToString("N"))

            Directory.CreateDirectory root |> ignore

            try
                let realDirectory = Path.Combine(root, "dir")
                let file = Path.Combine(root, "file")
                let linkToDirectory = Path.Combine(root, "link-dir")
                let linkToFile = Path.Combine(root, "link-file")
                Directory.CreateDirectory realDirectory |> ignore
                File.WriteAllText(file, "x")
                File.CreateSymbolicLink(linkToDirectory, realDirectory) |> ignore
                File.CreateSymbolicLink(linkToFile, file) |> ignore

                let directoryMask = LinuxOpenFlags.directoryOpen flags
                let fileMask = LinuxOpenFlags.fileOpen flags

                let opens path mask =
                    let descriptor = nativeOpen (path, mask)

                    if descriptor >= 0 then
                        nativeClose descriptor |> ignore

                    descriptor >= 0

                Assert.True(opens realDirectory directoryMask)
                Assert.False(opens linkToDirectory directoryMask) // O_NOFOLLOW
                Assert.False(opens file directoryMask) // O_DIRECTORY
                Assert.True(opens file fileMask)
                Assert.False(opens linkToFile fileMask) // O_NOFOLLOW
            finally
                Directory.Delete(root, true)

    // The table is only useful if the seams consume it. A literal x86 flag number in either seam
    // would still pass on an x86 host, so pin the source: no seam spells those numbers itself.
    [<Fact>]
    let ``no-follow seams take their open flags from the table and not from literals`` () =
        for relative in
            [
                "src/FS.GG.SDD.Commands/CommandEffects.fs"
                "src/FS.GG.SDD.Commands/GenerationSourceSnapshot.fs"
            ] do
            let text = File.ReadAllText(Path.Combine(TestSupport.repoRoot, relative))
            Assert.Contains("LinuxOpenFlags.", text)

            // A whole hex literal only: 0x80000000u (GENERIC_READ) is a different constant.
            Assert.False(System.Text.RegularExpressions.Regex.IsMatch(text, "0x(10000|20000|80000)(?![0-9a-fA-F])"))
