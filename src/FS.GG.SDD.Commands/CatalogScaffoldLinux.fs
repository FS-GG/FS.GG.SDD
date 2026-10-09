namespace FS.GG.SDD.Commands

open System
open System.IO
open System.Text
open System.Diagnostics
open System.Runtime.InteropServices
open System.Security.Cryptography
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks

/// The qualified first-host primitives. Directory objects and native children remain owned
/// independently of pathname observations and the pure workflow projection.
module internal CatalogScaffoldLinux =
    module private Native =
        [<DllImport("libc.so.6", SetLastError = true, EntryPoint = "open")>]
        extern int openFd(string path, int flags)
        [<DllImport("libc.so.6", SetLastError = true)>]
        extern int openat(int parent, string path, int flags, uint32 mode)
        [<DllImport("libc.so.6", SetLastError = true)>]
        extern int close(int fd)
        [<DllImport("libc.so.6", SetLastError = true)>]
        extern int ftruncate(int fd, int64 length)
        [<DllImport("libc.so.6", SetLastError = true)>]
        extern int fchmod(int fd, uint32 mode)
        [<DllImport("libc.so.6", SetLastError = true)>]
        extern int mkdirat(int parent, string path, uint32 mode)
        [<DllImport("libc.so.6", SetLastError = true)>]
        extern int unlinkat(int parent, string path, int flags)
        [<DllImport("libc.so.6", SetLastError = true)>]
        extern int renameat2(int sourceParent, string source, int targetParent, string target, uint32 flags)
        [<DllImport("libc.so.6", SetLastError = true)>]
        extern int statx(int parent, string path, int flags, uint32 mask, nativeint buffer)
        [<DllImport("libc.so.6", SetLastError = true)>]
        extern int fcntl(int fd, int command, int argument)
        [<DllImport("libc.so.6", SetLastError = true)>]
        extern int pipe2(nativeint descriptors, int flags)
        [<DllImport("libc.so.6", SetLastError = true)>]
        extern nativeint read(int fd, nativeint buffer, unativeint count)
        [<DllImport("libc.so.6", SetLastError = true)>]
        extern nativeint write(int fd, nativeint buffer, unativeint count)
        [<DllImport("libc.so.6", SetLastError = true)>]
        extern int sigemptyset(nativeint signals)
        [<DllImport("libc.so.6", SetLastError = true)>]
        extern int sigaddset(nativeint signals, int signal)
        [<DllImport("libc.so.6", SetLastError = true)>]
        extern int posix_spawnattr_init(nativeint attributes)
        [<DllImport("libc.so.6", SetLastError = true)>]
        extern int posix_spawnattr_destroy(nativeint attributes)
        [<DllImport("libc.so.6", SetLastError = true)>]
        extern int posix_spawnattr_setflags(nativeint attributes, int16 flags)
        [<DllImport("libc.so.6", SetLastError = true)>]
        extern int posix_spawnattr_setsigmask(nativeint attributes, nativeint signals)
        [<DllImport("libc.so.6", SetLastError = true)>]
        extern int posix_spawnattr_setsigdefault(nativeint attributes, nativeint signals)
        [<DllImport("libc.so.6", SetLastError = true)>]
        extern int posix_spawn_file_actions_init(nativeint actions)
        [<DllImport("libc.so.6", SetLastError = true)>]
        extern int posix_spawn_file_actions_destroy(nativeint actions)
        [<DllImport("libc.so.6", SetLastError = true)>]
        extern int posix_spawn_file_actions_addfchdir_np(nativeint actions, int cwd)
        [<DllImport("libc.so.6", SetLastError = true)>]
        extern int posix_spawn_file_actions_adddup2(nativeint actions, int source, int target)
        [<DllImport("libc.so.6", SetLastError = true)>]
        extern int posix_spawn_file_actions_addclosefrom_np(nativeint actions, int first)
        [<DllImport("libc.so.6", SetLastError = true)>]
        extern int pidfd_spawn(nativeint slot, string executable, nativeint actions, nativeint attributes, nativeint argv, nativeint environment)
        [<DllImport("libc.so.6", SetLastError = true)>]
        extern int waitid(int kind, uint32 descriptor, nativeint info, int options)
        [<DllImport("libc.so.6", SetLastError = true)>]
        extern int pidfd_send_signal(int descriptor, int signal, nativeint info, uint32 flags)
        [<DllImport("libc.so.6", SetLastError = true)>]
        extern nativeint gnu_get_libc_version()

    type AbiLayout =
        { SpawnAttributesBytes: int
          SpawnAttributesAlignment: int
          SpawnFileActionsBytes: int
          SpawnFileActionsAlignment: int
          SignalInfoBytes: int
          SignalInfoAlignment: int
          PollDescriptorBytes: int
          StatxBytes: int }
    type AbiObservation =
        { LibcVersion: string
          Layout: AbiLayout
          RequiredExports: (string * bool) list }
    type ObjectIdentity =
        { DeviceMajor: uint32
          DeviceMinor: uint32
          Inode: uint64
          Mount: uint64
          Kind: int }
    type DirectoryCustody =
        private
            { Fd: int
              Identity: ObjectIdentity
              mutable Parent: (DirectoryCustody * string) option
              Owner: obj }
    type CapturedFile = { Path: string; Bytes: byte array; Sha256: string }
    type BoundArgument = Literal of string | DirectoryPath of DirectoryCustody * string
    type Settlement = Pending | KnownTerminal | Unknown
    type ChildObservation =
        { ExitCode: int option
          Stdout: byte array
          Stderr: byte array
          StdoutEof: bool
          StderrEof: bool
          Settlement: Settlement
          Diagnostics: Fsgg.ProviderCatalog.Diagnostic list }
    exception Refused of Fsgg.ProviderCatalog.Diagnostic list
    exception CustodyUnknown of Fsgg.ProviderCatalog.Diagnostic list
    type PhaseBudget =
        private
            { WorkEnd: int64
              End: int64
              Cancellation: CancellationToken
              Stop: ManualResetEventSlim
              mutable OutputBytes: int64
              mutable StdoutBytes: int64
              mutable StderrBytes: int64
              DiagnosticGate: obj
              mutable DiagnosticBytes: int
              mutable DiagnosticLimitReported: bool }
    type private Reader =
        { Gate: obj
          Bytes: MemoryStream
          mutable Eof: bool
          mutable Error: string option
          mutable Overflow: bool
          Task: Task }
    type ChildCustody =
        private
            { Gate: obj
              mutable Pidfd: int option
              mutable Pid: int option
              mutable WaitPid: int option
              mutable Exit: (int * int) option
              mutable DispatchAttempted: bool
              mutable DispatchResult: int option
              mutable Reaped: bool
              mutable Unknown: bool
              mutable Diagnostics: Fsgg.ProviderCatalog.Diagnostic list
              mutable Launch: Task<int> option
              mutable Readers: (Reader * Reader) option
              Info: nativeint
              Owner: obj }
    type private CommitReceipt =
        { mutable TaskStarted: bool
          mutable Dispatched: bool
          mutable SyscallResult: int option
          mutable Errno: int option
          mutable Outcome: CatalogScaffoldWorkflow.CommitOutcome
          mutable OriginalUnknown: bool }
    type Custody =
        private
            { Gate: obj
              Descriptors: ResizeArray<int>
              Pointers: ResizeArray<nativeint>
              Utf8: ResizeArray<nativeint>
              UncertainDescriptors: ResizeArray<int>
              UncertainPointers: ResizeArray<nativeint>
              UncertainUtf8: ResizeArray<nativeint>
              Tasks: ResizeArray<Task>
              Disposals: ResizeArray<PhaseBudget -> unit>
              Commits: ResizeArray<CommitReceipt>
              Children: ResizeArray<ChildCustody>
              KnownFiles: Dictionary<ObjectIdentity * string, ObjectIdentity>
              KnownDirectories: Dictionary<ObjectIdentity * string, ObjectIdentity>
              mutable Change: TaskCompletionSource<unit>
              mutable Unknown: bool
              mutable Released: bool }

    let private diagnostic code message : Fsgg.ProviderCatalog.Diagnostic =
        { Code = code; Path = "$.scaffold.host"; Message = message }
    let private refuse code message = raise (Refused [ diagnostic code message ])
    let private unknown code message = raise (CustodyUnknown [ diagnostic code message ])
    let private ticks () = Stopwatch.GetTimestamp()
    let private directoryFlags = 65536 ||| 131072 ||| 524288
    let private fileFlags = 131072 ||| 524288 ||| 2048
    let private hash (bytes: byte array) = "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()
    let private changed (custody: Custody) =
        lock custody.Gate (fun () ->
            let prior = custody.Change
            custody.Change <- TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
            prior.TrySetResult(()) |> ignore)
    let createCustody () : Custody =
        { Gate = obj()
          Descriptors = ResizeArray()
          Pointers = ResizeArray()
          Utf8 = ResizeArray()
          UncertainDescriptors = ResizeArray()
          UncertainPointers = ResizeArray()
          UncertainUtf8 = ResizeArray()
          Tasks = ResizeArray()
          Disposals = ResizeArray()
          Commits = ResizeArray()
          Children = ResizeArray()
          KnownFiles = Dictionary()
          KnownDirectories = Dictionary()
          Change = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
          Unknown = false
          Released = false }
    let beginPhase (seconds: int) (cancellation: CancellationToken) : PhaseBudget =
        if seconds <= 0 then refuse "catalog.invalidBudget" "A phase requires a positive whole-phase ceiling."
        let start = ticks()
        let reserve = min 5.0 (float seconds / 10.0)
        { WorkEnd = start + int64 ((float seconds - reserve) * float Stopwatch.Frequency)
          End = start + int64 (float seconds * float Stopwatch.Frequency)
          Cancellation = cancellation
          Stop = new ManualResetEventSlim(false)
          OutputBytes = 0L
          StdoutBytes = 0L
          StderrBytes = 0L
          DiagnosticGate = obj()
          DiagnosticBytes = 0
          DiagnosticLimitReported = false }
    let stopUsefulWork (budget: PhaseBudget) = budget.Stop.Set()
    let checkWork (budget: PhaseBudget) =
        if budget.Stop.IsSet || budget.Cancellation.IsCancellationRequested || ticks() >= budget.WorkEnd then
            budget.Stop.Set()
            refuse "catalog.workStopped" "Cancellation or the original useful-work end stopped further work."
    let checkCleanup (budget: PhaseBudget) =
        if ticks() >= budget.End then unknown "catalog.cleanupDeadline" "The original whole-phase end expired with custody retained."
    let private addCapped (counter: byref<int64>, amount: int64, ceiling: int64) =
        let mutable doneAdding = false
        let mutable result = ceiling + 1L
        while not doneAdding do
            let prior = Volatile.Read(&counter)
            let next = if prior > ceiling || amount > ceiling - prior then ceiling + 1L else prior + amount
            doneAdding <- Interlocked.CompareExchange(&counter, next, prior) = prior
            result <- next
        result
    // One immutable bounded reserve projection is reused by subsequent observers.
    // Reprojection does not charge or allocate another terminal refusal.
    let private diagnosticLimitProjection =
        [diagnostic "catalog.diagnosticLimit" "The original diagnostic/aggregate output ceiling was exceeded; additional details are not retained."]
    let chargeDiagnostics (budget: PhaseBudget) (diagnostics: Fsgg.ProviderCatalog.Diagnostic list) =
        lock budget.DiagnosticGate (fun () ->
            let cost = diagnostics |> List.sumBy (fun value -> int64 (Encoding.UTF8.GetByteCount(value.Code) + Encoding.UTF8.GetByteCount(value.Path) + Encoding.UTF8.GetByteCount(value.Message)))
            let acceptable = cost <= 65536L && int64 budget.DiagnosticBytes + cost <= 65536L
            let total = if acceptable then addCapped(&budget.OutputBytes, cost, 2097152L) else 2097152L
            if acceptable && total <= 2096640L then
                budget.DiagnosticBytes <- budget.DiagnosticBytes + int cost
                diagnostics
            else
                stopUsefulWork budget
                if budget.DiagnosticLimitReported then diagnosticLimitProjection
                else
                    budget.DiagnosticLimitReported <- true
                    // A fixed512-byte reporting reserve is inside the2MiB whole-phase ceiling.
                    let refusal = List.head diagnosticLimitProjection
                    budget.DiagnosticBytes <- budget.DiagnosticBytes + Encoding.UTF8.GetByteCount(refusal.Message) + Encoding.UTF8.GetByteCount(refusal.Code) + Encoding.UTF8.GetByteCount(refusal.Path)
                    diagnosticLimitProjection)
    let private ownFd (custody: Custody) (fd: int) : int =
        if fd < 0 then refuse "catalog.nativeIo" ("Native acquisition failed, errno=" + string (Marshal.GetLastPInvokeError()))
        lock custody.Gate (fun () ->
            if not (custody.Descriptors.Contains fd) then custody.Descriptors.Add fd)
        fd
    let private closeFd (budget: PhaseBudget) (custody: Custody) (fd: int) =
        checkCleanup budget
        lock custody.Gate (fun () ->
            if not (custody.Descriptors.Contains fd) then
                unknown "catalog.closeSlotUnavailable" "The original descriptor slot has already been consumed or is uncertain."
            custody.UncertainDescriptors.Add fd
            custody.Descriptors.Remove fd |> ignore)
        try
            checkCleanup budget
            if Native.close(fd) <> 0 then
                unknown "catalog.closeUnknown" "An owned descriptor close did not have a known outcome."
            lock custody.Gate (fun () -> custody.UncertainDescriptors.Remove fd |> ignore)
            checkCleanup budget
        with error ->
            custody.Unknown <- true
            raise error
    let private allocate (custody: Custody) (count: int) : nativeint =
        let pointer = Marshal.AllocHGlobal(count)
        lock custody.Gate (fun () -> custody.Pointers.Add pointer)
        Marshal.Copy(Array.zeroCreate<byte> count, 0, pointer, count)
        pointer
    let private registerTask (custody: Custody) (task: Task) =
        lock custody.Gate (fun () -> custody.Tasks.Add task)
        task.ContinueWith(Action<Task>(fun _ -> changed custody), TaskScheduler.Default) |> ignore
    let private awaitWork (budget: PhaseBudget) (task: Task<'a>) : 'a =
        while not task.IsCompleted do
            checkWork budget
            // A bounded readiness wait; expiry leaves the same registered task and buffers owned.
            Thread.Sleep(1)
        checkWork budget
        task.GetAwaiter().GetResult()
    let private io (custody: Custody) (budget: PhaseBudget) (action: unit -> 'a) : 'a =
        checkWork budget
        let task = new Task<'a>(action)
        registerTask custody task
        checkWork budget
        task.Start(TaskScheduler.Default)
        try awaitWork budget task
        with
        | Refused diagnostics -> raise (Refused(chargeDiagnostics budget diagnostics))
        | CustodyUnknown diagnostics -> raise (CustodyUnknown(chargeDiagnostics budget diagnostics))
    let private zero (name: string) (result: int) =
        if result <> 0 then refuse "catalog.nativeCall" (name + " failed, result=" + string result + ", errno=" + string (Marshal.GetLastPInvokeError()))
    let private leaf (value: string) =
        if String.IsNullOrWhiteSpace value || value = "." || value = ".." || value.IndexOfAny([| '/'; '\\'; char 0 |]) >= 0 then
            refuse "catalog.invalidPath" "Only one nonempty relative path component is allowed."
    let private parts (relative: string) =
        if String.IsNullOrWhiteSpace relative || Path.IsPathRooted relative || relative.Contains('\\') then
            refuse "catalog.invalidPath" "A normalized relative path is required."
        let values = relative.Split('/')
        values |> Array.iter leaf
        values
    // Called by actual metadata and cold cleanup dispatch paths. The allocation /
    // registration prelude cannot authorize a later syscall or Start after expiry.
    let private metadataProbe (guard: unit -> unit) (prepare: unit -> 'a) (probe: 'a -> 'b) : 'b =
        guard()
        let prepared = prepare()
        guard()
        let result = probe prepared
        guard()
        result

    let private stamp (custody: Custody) (guard: unit -> unit) (fd: int) : ObjectIdentity * byte array =
        let buffer =
            metadataProbe guard
                (fun () -> allocate custody 256)
                (fun buffer -> zero "statx" (Native.statx(fd, "", 4096 ||| 256, 0x17ffu, buffer)); buffer)
        guard()
        if (uint32 (Marshal.ReadInt32(buffer, 0)) &&& 0x13c4u) <> 0x13c4u then
            refuse "catalog.identityUnavailable" "The filesystem did not report required physical identity fields."
        let bytes = Array.zeroCreate<byte> 256
        guard()
        Marshal.Copy(buffer, bytes, 0, bytes.Length)
        guard()
        { DeviceMajor = uint32 (Marshal.ReadInt32(buffer, 136))
          DeviceMinor = uint32 (Marshal.ReadInt32(buffer, 140))
          Inode = uint64 (Marshal.ReadInt64(buffer, 32))
          Mount = uint64 (Marshal.ReadInt64(buffer, 144))
          Kind = int (uint16 (Marshal.ReadInt16(buffer, 28))) &&& 0xf000 }, bytes
    let private asDirectory (custody: Custody) (guard: unit -> unit) (parent: (DirectoryCustody * string) option) (fd: int) : DirectoryCustody =
        let actual, _ = stamp custody guard fd
        if actual.Kind <> 0x4000 then refuse "catalog.notDirectory" "The opened object is not a physical directory."
        match parent with
        | Some(container, name) ->
            lock custody.Gate (fun () ->
                match custody.KnownDirectories.TryGetValue((container.Identity, name)) with
                | true, original when original <> actual -> refuse "catalog.directoryChanged" "A previously captured directory binding changed."
                | true, _ -> ()
                | _ -> custody.KnownDirectories[(container.Identity, name)] <- actual)
        | None -> ()
        { Fd = fd; Identity = actual; Parent = parent; Owner = (custody :> obj) }
    let private openChild (custody: Custody) (guard: unit -> unit) (parent: DirectoryCustody) (name: string) =
        leaf name
        guard()
        let fd = ownFd custody (Native.openat(parent.Fd, name, directoryFlags, 0u))
        guard()
        asDirectory custody guard (Some(parent, name)) fd
    let openDirectory (custody: Custody) (budget: PhaseBudget) (absolute: string) : DirectoryCustody =
        if not (Path.IsPathFullyQualified absolute) || absolute.Contains(char 0) || absolute.Contains('\\') then
            refuse "catalog.invalidRoot" "An absolute normalized Linux root is required."
        io custody budget (fun () ->
            checkWork budget
            let root = asDirectory custody (fun () -> checkWork budget) None (ownFd custody (Native.openFd("/", directoryFlags)))
            if absolute = "/" then root
            else
                let values = absolute.Substring(1).Split('/')
                values |> Array.iter leaf
                (root, values) ||> Array.fold (fun parent name -> checkWork budget; openChild custody (fun () -> checkWork budget) parent name))
    let identity (directory: DirectoryCustody) = directory.Identity
    let rec private validateWith (budget: PhaseBudget) (check: unit -> unit) (custody: Custody) (directory: DirectoryCustody) =
        check()
        let actual, _ = stamp custody check directory.Fd
        if actual <> directory.Identity then refuse "catalog.directoryChanged" "The held directory identity changed."
        match directory.Parent with
        | None -> ()
        | Some(parent, name) ->
            validateWith budget check custody parent
            check()
            let opened = openChild custody check parent name
            let same = opened.Identity = directory.Identity
            closeFd budget custody opened.Fd
            if not same then refuse "catalog.directoryChanged" "A pathname component no longer identifies the held physical directory."
    let private validate custody budget directory = validateWith budget (fun () -> checkWork budget) custody directory
    let private validateCleanup custody budget directory = validateWith budget (fun () -> checkCleanup budget) custody directory
    let private boundTo (directory: DirectoryCustody) (parent: DirectoryCustody) (name: string) =
        match directory.Parent with
        | Some(actual, leaf) -> actual.Fd = parent.Fd && actual.Identity = parent.Identity && leaf = name
        | None -> false
    let revalidateDirectory (budget: PhaseBudget) (directory: DirectoryCustody) =
        let owner = unbox<Custody> directory.Owner
        io owner budget (fun () -> validate owner budget directory)
    let createDirectory (custody: Custody) (budget: PhaseBudget) (parent: DirectoryCustody) (name: string) =
        leaf name
        io custody budget (fun () ->
            validate custody budget parent
            checkWork budget
            zero "mkdirat" (Native.mkdirat(parent.Fd, name, 448u))
            let child = openChild custody (fun () -> checkWork budget) parent name
            if child.Identity.Mount <> parent.Identity.Mount then refuse "catalog.mountChanged" "Private staging crossed the selected mount."
            child)
    let private pathParent (custody: Custody) (budget: PhaseBudget) (root: DirectoryCustody) (relative: string) (create: bool) =
        let values = parts relative
        let mutable parent = root
        for index in 0 .. values.Length - 2 do
            checkWork budget
            if create then
                let result = Native.mkdirat(parent.Fd, values[index], 448u)
                if result <> 0 && Marshal.GetLastPInvokeError() <> 17 then zero "mkdirat" result
            parent <- openChild custody (fun () -> checkWork budget) parent values[index]
        parent, values[values.Length - 1]
    let private stableBytes (custody: Custody) (budget: PhaseBudget) (fd: int) (maximumBytes: int) =
        let objectBefore, before = stamp custody (fun () -> checkWork budget) fd
        if objectBefore.Kind <> 0x8000 then refuse "catalog.nonRegularFile" "Links and special payload objects are refused."
        let size = BitConverter.ToInt64(before, 40)
        if size < 0L || size > int64 maximumBytes then refuse "catalog.inputSize" "The actual opened file exceeds its byte ceiling."
        let buffer = allocate custody (max 1 (int size + 1))
        let mutable total = 0
        let mutable eof = false
        while not eof do
            checkWork budget
            let count = Native.read(fd, buffer + nativeint total, unativeint (int size + 1 - total))
            if count = nativeint 0 then eof <- true
            elif count < nativeint 0 then
                if Marshal.GetLastPInvokeError() <> 4 then refuse "catalog.fileRead" "The owned input read failed."
            else
                total <- total + int count
                if total > int size then refuse "catalog.fileChanged" "The opened file grew during capture."
        let objectAfter, after = stamp custody (fun () -> checkWork budget) fd
        let relevant (value: byte array) = Array.concat [ value[16..19]; value[32..47]; value[96..127]; value[136..151] ]
        if total <> int size || objectAfter <> objectBefore || relevant before <> relevant after then
            refuse "catalog.fileChanged" "The captured physical file changed during its read."
        let bytes = Array.zeroCreate<byte> total
        Marshal.Copy(buffer, bytes, 0, total)
        bytes
    let private rememberFile (custody: Custody) (parent: DirectoryCustody) (name: string) (actual: ObjectIdentity) =
        lock custody.Gate (fun () ->
            match custody.KnownFiles.TryGetValue((parent.Identity, name)) with
            | true, original when original <> actual ->
                refuse "catalog.fileBindingChanged" "A previously captured regular-file binding changed."
            | true, _ -> ()
            | _ -> custody.KnownFiles[(parent.Identity, name)] <- actual)
    let readFile (custody: Custody) (budget: PhaseBudget) (root: DirectoryCustody) (relative: string) (maximumBytes: int) : CapturedFile =
        if maximumBytes < 0 then refuse "catalog.inputSize" "A nonnegative capture ceiling is required."
        io custody budget (fun () ->
            validate custody budget root
            let parent, name = pathParent custody budget root relative false
            let fd = ownFd custody (Native.openat(parent.Fd, name, fileFlags, 0u))
            let actual, _ = stamp custody (fun () -> checkWork budget) fd
            let bytes = stableBytes custody budget fd maximumBytes
            rememberFile custody parent name actual
            closeFd budget custody fd
            { Path = relative; Bytes = bytes; Sha256 = hash bytes })
    let writeFile (custody: Custody) (budget: PhaseBudget) (root: DirectoryCustody) (relative: string) (bytes: byte array) (replaceKnown: bool) =
        io custody budget (fun () ->
            validate custody budget root
            let parent, name = pathParent custody budget root relative true
            // Existing content is opened and classified before any truncation. No O_TRUNC follows a substituted name.
            let flags = fileFlags ||| 1 ||| (if replaceKnown then 0 else 64 ||| 128)
            let fd = ownFd custody (Native.openat(parent.Fd, name, flags, 384u))
            let actual, _ = stamp custody (fun () -> checkWork budget) fd
            if actual.Kind <> 0x8000 then refuse "catalog.nonRegularFile" "Only owned regular output files may be written."
            let _, outputStamp = stamp custody (fun () -> checkWork budget) fd
            if BitConverter.ToUInt32(outputStamp, 16) <> 1u then refuse "catalog.outputAlias" "A mutative output must have exactly one hard-link binding."
            if replaceKnown then
                let known = lock custody.Gate (fun () ->
                    match custody.KnownFiles.TryGetValue((parent.Identity, name)) with
                    | true, value -> Some value
                    | _ -> None)
                if known <> Some actual then refuse "catalog.outputChanged" "The replacement object is not the exact previously captured regular file."
                checkWork budget
                zero "ftruncate" (Native.ftruncate(fd, 0L))
            rememberFile custody parent name actual
            let buffer = allocate custody (max 1 bytes.Length)
            Marshal.Copy(bytes, 0, buffer, bytes.Length)
            let mutable offset = 0
            while offset < bytes.Length do
                checkWork budget
                let count = Native.write(fd, buffer + nativeint offset, unativeint (bytes.Length - offset))
                if count <= nativeint 0 then refuse "catalog.fileWrite" "The owned output write failed."
                offset <- offset + int count
            closeFd budget custody fd)
    let setExecutable (custody: Custody) (budget: PhaseBudget) (root: DirectoryCustody) (relative: string) =
        if not(obj.ReferenceEquals(root.Owner, custody)) then
            refuse "catalog.fileOwner" "Executable mode requires the original file custody."
        io custody budget (fun () ->
            validate custody budget root
            let parent, name = pathParent custody budget root relative false
            checkWork budget
            let fd = ownFd custody (Native.openat(parent.Fd, name, fileFlags, 0u))
            let actual, before = stamp custody (fun () -> checkWork budget) fd
            if actual.Kind <> 0x8000 then refuse "catalog.nonRegularFile" "Only owned regular files may become executable."
            if BitConverter.ToUInt32(before, 16) <> 1u then
                refuse "catalog.outputAlias" "A mutative output must have exactly one hard-link binding."
            let known = lock custody.Gate (fun () ->
                match custody.KnownFiles.TryGetValue((parent.Identity, name)) with
                | true, value -> Some value
                | _ -> None)
            if known <> Some actual then
                refuse "catalog.fileBindingChanged" "Executable mode requires the exact previously captured regular file."
            let checkBinding () =
                validate custody budget parent
                checkWork budget
                let binding = ownFd custody (Native.openat(parent.Fd, name, fileFlags, 0u))
                let observed, _ = stamp custody (fun () -> checkWork budget) binding
                closeFd budget custody binding
                if observed <> actual then refuse "catalog.fileBindingChanged" "The executable file binding changed."
            checkBinding()
            // Change only executable permission bits on the held file, preserving its bytes and first identity.
            let mode = (uint32 (BitConverter.ToUInt16(before, 28)) &&& 4095u) ||| 73u
            checkWork budget
            zero "fchmod" (Native.fchmod(fd, mode))
            checkWork budget
            let afterIdentity, after = stamp custody (fun () -> checkWork budget) fd
            if afterIdentity <> actual || BitConverter.ToUInt32(after, 16) <> 1u
               || (uint32 (BitConverter.ToUInt16(after, 28)) &&& 4095u) <> mode
               || before[40..47] <> after[40..47] || before[112..127] <> after[112..127] then
                refuse "catalog.fileChanged" "The held executable file failed identity, mode or content-metadata readback."
            checkBinding()
            closeFd budget custody fd)
    let private names (directory: DirectoryCustody) =
        let values =
            Directory.GetFileSystemEntries(sprintf "/proc/self/fd/%d" directory.Fd)
            |> Array.map (fun path ->
                match Path.GetFileName path with
                | null -> refuse "catalog.invalidPath" "Only one nonempty relative path component is allowed."
                | name -> name)
            |> Array.sortWith (fun (left: string) (right: string) -> StringComparer.Ordinal.Compare(left, right))
        values |> Array.iter leaf
        if (values |> Array.distinctBy (fun name -> name.ToUpperInvariant())).Length <> values.Length then
            refuse "catalog.pathAlias" "Case aliases are refused in the closed staging payload."
        values
    let files (custody: Custody) (budget: PhaseBudget) (root: DirectoryCustody) (maximumBytes: int) : CapturedFile list =
        io custody budget (fun () ->
            let result = ResizeArray<CapturedFile>()
            let mutable consumed = 0
            let rec walk (directory: DirectoryCustody) (prefix: string) =
                checkWork budget
                let identityBefore, before = stamp custody (fun () -> checkWork budget) directory.Fd
                let original = names directory
                for name in original do
                    checkWork budget
                    let fd = ownFd custody (Native.openat(directory.Fd, name, fileFlags, 0u))
                    let objectIdentity, _ = stamp custody (fun () -> checkWork budget) fd
                    let path = if prefix = "" then name else prefix + "/" + name
                    match objectIdentity.Kind with
                    | 0x4000 -> walk (asDirectory custody (fun () -> checkWork budget) (Some(directory, name)) fd) path
                    | 0x8000 ->
                        let _, payloadStamp = stamp custody (fun () -> checkWork budget) fd
                        if BitConverter.ToUInt32(payloadStamp, 16) <> 1u then refuse "catalog.payloadAlias" "Hard-linked staging payloads are refused."
                        let bytes = stableBytes custody budget fd (maximumBytes - consumed)
                        consumed <- consumed + bytes.Length
                        rememberFile custody directory name objectIdentity
                        result.Add { Path = path; Bytes = bytes; Sha256 = hash bytes }
                    | _ -> refuse "catalog.nonRegularFile" "Links and special staging objects are refused."
                    closeFd budget custody fd
                let identityAfter, after = stamp custody (fun () -> checkWork budget) directory.Fd
                if identityAfter <> identityBefore || before[96..127] <> after[96..127] || original <> names directory then
                    refuse "catalog.directoryChanged" "The closed payload changed during enumeration."
            validate custody budget root
            walk root ""
            result |> Seq.sortBy (fun value -> value.Path) |> Seq.toList)

    let private awaitCleanup (budget: PhaseBudget) (task: Task<'a>) : 'a =
        while not task.IsCompleted do
            checkCleanup budget
            Thread.Sleep(1)
        checkCleanup budget
        task.GetAwaiter().GetResult()
    let private cleanupIo (custody: Custody) (budget: PhaseBudget) (action: unit -> 'a) : 'a =
        let task =
            metadataProbe (fun () -> checkCleanup budget)
                (fun () ->
                    let task = new Task<'a>(action)
                    registerTask custody task
                    task)
                (fun task -> task.Start(TaskScheduler.Default); task)
        try awaitCleanup budget task
        with
        | Refused diagnostics -> raise (Refused(chargeDiagnostics budget diagnostics))
        | CustodyUnknown diagnostics -> raise (CustodyUnknown(chargeDiagnostics budget diagnostics))
    let commitNoReplace (budget: PhaseBudget) (sourceParent: DirectoryCustody) (source: DirectoryCustody) (sourceLeaf: string) (targetParent: DirectoryCustody) (targetLeaf: string) =
        leaf sourceLeaf
        leaf targetLeaf
        let owner = unbox<Custody> source.Owner
        let receipt =
            { TaskStarted = false; Dispatched = false; SyscallResult = None; Errno = None
              Outcome = CatalogScaffoldWorkflow.NotAttempted; OriginalUnknown = false }
        lock owner.Gate (fun () -> owner.Commits.Add receipt)
        try
            checkWork budget
            let task = new Task<CatalogScaffoldWorkflow.CommitOutcome>(fun () ->
                validate owner budget sourceParent
                validate owner budget targetParent
                validate owner budget source
                if not (boundTo source sourceParent sourceLeaf) then
                    refuse "catalog.stagingChanged" "Commit source no longer has its selected owned binding."
                checkWork budget
                // This receipt belongs to the caller-held operation before the consumed syscall.
                receipt.Dispatched <- true
                let result = Native.renameat2(sourceParent.Fd, sourceLeaf, targetParent.Fd, targetLeaf, 1u)
                let error = Marshal.GetLastPInvokeError()
                receipt.SyscallResult <- Some result
                receipt.Errno <- Some error
                if result <> 0 then receipt.Outcome <- CatalogScaffoldWorkflow.Refused
                else
                    source.Parent <- Some(targetParent, targetLeaf)
                    try
                        checkWork budget
                        let destination = openChild owner (fun () -> checkWork budget) targetParent targetLeaf
                        let same = destination.Identity = source.Identity
                        closeFd budget owner destination.Fd
                        validate owner budget targetParent
                        receipt.Outcome <- if same then CatalogScaffoldWorkflow.Committed else CatalogScaffoldWorkflow.Unknown
                    with _ -> receipt.Outcome <- CatalogScaffoldWorkflow.Unknown
                if receipt.OriginalUnknown then CatalogScaffoldWorkflow.Unknown else receipt.Outcome)
            registerTask owner task
            checkWork budget
            receipt.TaskStarted <- true
            task.Start(TaskScheduler.Default)
            awaitWork budget task
        with error ->
            if receipt.TaskStarted then
                receipt.OriginalUnknown <- true
                // Later return of the existing task may fill raw syscall facts. It cannot
                // upgrade this immutable original publication outcome or authorize retry.
                CatalogScaffoldWorkflow.Unknown
            else raise error
    let commitObservations (custody: Custody) =
        lock custody.Gate (fun () ->
            custody.Commits |> Seq.map (fun receipt -> receipt.Dispatched, receipt.SyscallResult, receipt.Errno, receipt.Outcome, receipt.OriginalUnknown) |> Seq.toList)
    let private childUnsettled (child: ChildCustody) =
        child.Unknown || not child.Reaped
        || (child.Readers |> Option.exists(fun (first, second) ->
            [first; second] |> List.exists(fun reader ->
                lock reader.Gate (fun () ->
                    reader.Error.IsSome || (reader.Task.Status <> TaskStatus.Created && not reader.Task.IsCompleted)))))
    let captureRetirementInventory (custody: Custody) (budget: PhaseBudget) (root: DirectoryCustody) =
        // Caller contract: the single consuming Effects runner has stopped all
        // relevant original producer budgets and no concurrent same-Custody caller
        // can register/start work. This check is not a global exclusion lock.
        checkCleanup budget
        if not(obj.ReferenceEquals(root.Owner,custody)) then
            refuse "catalog.retirementOwner" "Retirement inventory requires the original owning custody of the private staging root."
        let unavailable =
            lock custody.Gate (fun () ->
                custody.Unknown
                || (custody.Children |> Seq.exists childUnsettled)
                || (custody.Tasks |> Seq.exists(fun task -> task.Status <> TaskStatus.Created && not task.IsCompleted)))
        if unavailable then
            unknown "catalog.retirementInventoryUnknown" "Children, readers or tasks still own staging; no inventory or retirement is authorized."
        cleanupIo custody budget (fun () ->
            let rec capture (directory: DirectoryCustody) =
                checkCleanup budget
                validateCleanup custody budget directory
                checkCleanup budget
                let originalIdentity,before = stamp custody (fun () -> checkCleanup budget) directory.Fd
                checkCleanup budget
                let entries = names directory
                checkCleanup budget
                for name in entries do
                    checkCleanup budget
                    let fd = ownFd custody (Native.openat(directory.Fd,name,fileFlags,0u))
                    checkCleanup budget
                    let identity,metadata = stamp custody (fun () -> checkCleanup budget) fd
                    checkCleanup budget
                    if identity.Mount <> directory.Identity.Mount then
                        refuse "catalog.mountChanged" "Retirement inventory cannot cross the held staging mount."
                    match identity.Kind with
                    | 0x4000 ->
                        // asDirectory retains the first identity and refuses substitution.
                        checkCleanup budget
                        let child = asDirectory custody (fun () -> checkCleanup budget) (Some(directory,name)) fd
                        checkCleanup budget
                        capture child
                    | 0x8000 ->
                        if BitConverter.ToUInt32(metadata,16) <> 1u then
                            refuse "catalog.payloadAlias" "Hard-linked staging entries are outside the closed retirement profile."
                        checkCleanup budget
                        rememberFile custody directory name identity
                        checkCleanup budget
                    | _ -> refuse "catalog.retirementPayload" "Links and special objects are retained without inventory adoption."
                    closeFd budget custody fd
                    checkCleanup budget
                checkCleanup budget
                let finalIdentity,after = stamp custody (fun () -> checkCleanup budget) directory.Fd
                checkCleanup budget
                let finalEntries = names directory
                checkCleanup budget
                if originalIdentity <> finalIdentity || before[96..127] <> after[96..127] || entries <> finalEntries then
                    refuse "catalog.directoryChanged" "Staging changed during cleanup-only metadata capture."
                validateCleanup custody budget directory
                checkCleanup budget
            capture root
            checkCleanup budget)

    let retireDirectory (custody: Custody) (budget: PhaseBudget) (parent: DirectoryCustody) (name: string) (root: DirectoryCustody) =
        leaf name
        if custody.Unknown then
            unknown "catalog.retirementUnknown" "Original operation custody is unknown; no filesystem retirement is authorized."
        if custody.Children |> Seq.exists childUnsettled then
            unknown "catalog.childrenUnsettled" "A native child still owns the workspace; no filesystem retirement is authorized."
        if custody.Tasks |> Seq.exists (fun task -> task.Status <> TaskStatus.Created && not task.IsCompleted) then
            unknown "catalog.tasksUnsettled" "An in-flight task still owns the workspace; no filesystem retirement is authorized."
        cleanupIo custody budget (fun () ->
            checkCleanup budget
            validateCleanup custody budget parent
            validateCleanup custody budget root
            if not (boundTo root parent name) then refuse "catalog.retirementBinding" "The original private directory binding changed."
            let rec remove (directory: DirectoryCustody) (prefix: string) =
                for entry in names directory do
                    checkCleanup budget
                    let fd = ownFd custody (Native.openat(directory.Fd, entry, fileFlags, 0u))
                    let actual, _ = stamp custody (fun () -> checkCleanup budget) fd
                    if actual.Mount <> directory.Identity.Mount then refuse "catalog.mountChanged" "Retirement cannot cross a mount."
                    match actual.Kind with
                    | 0x4000 ->
                        let known = lock custody.Gate (fun () ->
                            match custody.KnownDirectories.TryGetValue((directory.Identity, entry)) with
                            | true, value -> Some value
                            | _ -> None)
                        if known <> Some actual then refuse "catalog.retirementBinding" "A private directory differs from its prior captured identity; it is retained."
                        let child = asDirectory custody (fun () -> checkCleanup budget) (Some(directory, entry)) fd
                        let relative = if prefix = "" then entry else prefix + "/" + entry
                        remove child relative
                        validateCleanup custody budget child
                        checkCleanup budget
                        zero "unlinkat-directory" (Native.unlinkat(directory.Fd, entry, 512))
                    | 0x8000 ->
                        let relative = if prefix = "" then entry else prefix + "/" + entry
                        let known = lock custody.Gate (fun () ->
                            match custody.KnownFiles.TryGetValue((directory.Identity, entry)) with
                            | true, value -> Some value
                            | _ -> None)
                        if known <> Some actual then refuse "catalog.retirementBinding" "A private file differs from the prior captured owned identity; it is retained."
                        // Also recheck the current binding immediately before unlink.
                        let check = ownFd custody (Native.openat(directory.Fd, entry, fileFlags, 0u))
                        let current, _ = stamp custody (fun () -> checkCleanup budget) check
                        closeFd budget custody check
                        if current <> actual then refuse "catalog.retirementBinding" "A private file binding changed."
                        checkCleanup budget
                        zero "unlinkat-file" (Native.unlinkat(directory.Fd, entry, 0))
                    | _ -> refuse "catalog.retirementPayload" "Unrecognized objects are retained, never recursively deleted."
                    closeFd budget custody fd
            remove root ""
            validateCleanup custody budget root
            checkCleanup budget
            zero "unlinkat-operation" (Native.unlinkat(parent.Fd, name, 512)))

    let private requiredExports =
        [ "pidfd_spawn"; "pidfd_send_signal"; "waitid"; "poll"; "pipe2"
          "posix_spawnattr_init"; "posix_spawnattr_destroy"; "posix_spawnattr_setflags"
          "posix_spawnattr_setsigmask"; "posix_spawnattr_setsigdefault"
          "posix_spawn_file_actions_init"; "posix_spawn_file_actions_destroy"
          "posix_spawn_file_actions_addfchdir_np"; "posix_spawn_file_actions_adddup2"
          "posix_spawn_file_actions_addclose"; "posix_spawn_file_actions_addclosefrom_np"
          "sigemptyset"; "sigaddset"; "openat"; "mkdirat"; "unlinkat"; "renameat2"
          "statx"; "fcntl"; "read"; "write"; "close"; "__errno_location"; "ftruncate"; "fchmod" ]
    let private projectAbiFailure (budget: PhaseBudget) (primary: Choice<exn, Fsgg.ProviderCatalog.Diagnostic list>) (secondary: exn option) =
        let original =
            match primary with
            | Choice1Of2 error -> [ diagnostic "catalog.unsupportedAbi" error.Message ]
            | Choice2Of2 diagnostics -> diagnostics
        // Primary detail gets first use of the SAME aggregate/diagnostic allowance.
        // If even it cannot fit, the explicit limit marker is the only projection;
        // a secondary cleanup error must never become the apparent primary cause.
        let projected = chargeDiagnostics budget original
        if projected <> original then projected
        else
            match secondary with
            | None -> projected
            | Some error -> projected @ chargeDiagnostics budget [ diagnostic "catalog.abiRetirementUnknown" error.Message ]
    let observeAbi (custody: Custody) (budget: PhaseBudget) : Result<AbiObservation, Fsgg.ProviderCatalog.Diagnostic list> =
        if not (OperatingSystem.IsLinux()) || RuntimeInformation.ProcessArchitecture <> Architecture.X64 then
            Error [ diagnostic "catalog.unsupportedHost" "The selected first host requires Linux x64." ]
        else
            let mutable primaryFailure: exn option = None
            let mutable originalResult: Result<AbiObservation, Fsgg.ProviderCatalog.Diagnostic list> option = None
            let mutable retirementFailure: exn option = None
            try
                let gate = obj()
                let mutable library = nativeint 0
                let mutable loadAttempted = false
                let mutable loadReturned = false
                let mutable releaseAttempted = false
                let release (retirementBudget: PhaseBudget) =
                    lock gate (fun () ->
                        if loadAttempted && not loadReturned then
                            custody.Unknown <- true
                            unknown "catalog.libraryLoadUnknown" "The original library acquisition has no known return."
                        if library <> nativeint 0 && not releaseAttempted then
                            checkCleanup budget
                            checkCleanup retirementBudget
                            releaseAttempted <- true
                            try
                                NativeLibrary.Free library
                                library <- nativeint 0
                                checkCleanup budget
                                checkCleanup retirementBudget
                            with error ->
                                custody.Unknown <- true
                                raise error)
                // The library slot belongs to the caller BEFORE acquisition.
                lock custody.Gate (fun () -> custody.Disposals.Add release)
                checkWork budget
                loadAttempted <- true
                try
                    library <- NativeLibrary.Load("libc.so.6")
                    loadReturned <- true
                with error ->
                    custody.Unknown <- true
                    raise error
                let observation =
                    try
                        let outcome =
                            try
                                checkWork budget
                                let exports = requiredExports |> List.map (fun name ->
                                    let mutable address = nativeint 0
                                    checkWork budget
                                    let present = NativeLibrary.TryGetExport(library, name, &address)
                                    checkWork budget
                                    name, present)
                                checkWork budget
                                let versionPointer = Native.gnu_get_libc_version()
                                checkWork budget
                                let version =
                                    match Marshal.PtrToStringUTF8(versionPointer) with
                                    | null -> ""
                                    | value -> value
                                checkWork budget
                                let missing = exports |> List.choose (fun (name, present) -> if present then None else Some name)
                                if version <> "2.44" || not missing.IsEmpty then
                                    Error
                                        [ diagnostic "catalog.unsupportedAbi"
                                            (sprintf "Actual libc version=%s (required2.44); missing required exports=[%s]." version (String.concat "," missing)) ]
                                else
                                    checkWork budget
                                    Ok
                                        { LibcVersion = version
                                          Layout =
                                            { SpawnAttributesBytes = 336; SpawnAttributesAlignment = 8
                                              SpawnFileActionsBytes = 80; SpawnFileActionsAlignment = 8
                                              SignalInfoBytes = 128; SignalInfoAlignment = 8
                                              PollDescriptorBytes = 8; StatxBytes = 256 }
                                          RequiredExports = exports }
                            with error ->
                                primaryFailure <- Some error
                                raise error
                        originalResult <- Some outcome
                        outcome
                    finally
                        try release budget
                        with cleanupError ->
                            custody.Unknown <- true
                            retirementFailure <- Some cleanupError
                            if primaryFailure.IsNone then raise cleanupError
                match observation with
                | Error diagnostics -> Error(projectAbiFailure budget (Choice2Of2 diagnostics) None)
                | Ok value -> Ok value
            with error ->
                match originalResult with
                | Some(Error diagnostics) -> Error(projectAbiFailure budget (Choice2Of2 diagnostics) retirementFailure)
                | _ ->
                    let primary = defaultArg primaryFailure error
                    let secondary = if primaryFailure.IsSome then retirementFailure else None
                    Error(projectAbiFailure budget (Choice1Of2 primary) secondary)
    let private initializedObject (budget: PhaseBudget) (custody: Custody) (name: string) (initialize: nativeint -> int) (destroy: nativeint -> int) (pointer: nativeint) =
        let gate = obj()
        let mutable initializationAttempted = false
        let mutable initializationReturned = false
        let mutable initialized = false
        let mutable attempted = false
        let release (retirementBudget: PhaseBudget) =
            lock gate (fun () ->
                if initializationAttempted && not initializationReturned then
                    custody.Unknown <- true
                    unknown "catalog.opaqueInitUnknown" (name + " initialization has no known return.")
                if initialized && not attempted then
                    checkCleanup budget
                    checkCleanup retirementBudget
                    attempted <- true
                    try
                        if destroy pointer <> 0 then
                            unknown "catalog.opaqueDestroyUnknown" (name + " destruction failed; original object remains retained.")
                        initialized <- false
                        checkCleanup budget
                        checkCleanup retirementBudget
                    with error ->
                        custody.Unknown <- true
                        raise error)
        // Register the original slot BEFORE init can acquire an opaque native object.
        lock custody.Gate (fun () -> custody.Disposals.Add release)
        checkWork budget
        initializationAttempted <- true
        let result =
            try
                let result = initialize pointer
                initialized <- result = 0
                initializationReturned <- true
                result
            with error ->
                custody.Unknown <- true
                raise error
        zero (name + "-init") result
        release
    let private vector (custody: Custody) (values: string list) =
        let pointer = allocate custody ((values.Length + 1) * IntPtr.Size)
        values |> List.iteri (fun index value ->
            if obj.ReferenceEquals(value, null) || value.Contains(char 0) then refuse "catalog.invalidArgument" "NUL and null argv/environment values are refused."
            let text = Marshal.StringToCoTaskMemUTF8(value)
            lock custody.Gate (fun () -> custody.Utf8.Add text)
            Marshal.WriteIntPtr(pointer, index * IntPtr.Size, text))
        pointer
    let private pipe (custody: Custody) =
        let pointer = allocate custody 8
        zero "pipe2" (Native.pipe2(pointer, 524288))
        ownFd custody (Marshal.ReadInt32(pointer, 0)), ownFd custody (Marshal.ReadInt32(pointer, 4))
    let private reader (custody: Custody) (budget: PhaseBudget) (stderr: bool) (fd: int) =
        let buffer = allocate custody 8192
        let gate = obj()
        let bytes = new MemoryStream()
        let mutable record: Reader option = None
        let task = new Task(fun () ->
            let actual = record.Value
            try
                let mutable eof = false
                while not eof do
                    let count = Native.read(fd, buffer, unativeint 8192)
                    if count = nativeint 0 then
                        lock gate (fun () -> actual.Eof <- true)
                        eof <- true
                    elif count < nativeint 0 then
                        if Marshal.GetLastPInvokeError() <> 4 then
                            let details = chargeDiagnostics budget [ diagnostic "catalog.nativeReaderError" ("read errno=" + string (Marshal.GetLastPInvokeError())) ]
                            lock gate (fun () -> actual.Error <- Some(details |> List.map (fun value -> value.Message) |> String.concat "; "))
                            eof <- true
                    else
                        let amount = int count
                        let total = addCapped(&budget.OutputBytes, int64 amount, 2097152L)
                        let streamTotal = if stderr then addCapped(&budget.StderrBytes, int64 amount, 1048576L) else addCapped(&budget.StdoutBytes, int64 amount, 1048576L)
                        lock gate (fun () ->
                            if actual.Overflow || streamTotal > 1048576L || total > 2096640L then
                                actual.Overflow <- true
                                stopUsefulWork budget
                            else
                                let chunk = Array.zeroCreate<byte> amount
                                Marshal.Copy(buffer, chunk, 0, amount)
                                bytes.Write(chunk, 0, amount))
                // This preowned reader remains alive across deadlines. Late EOF is a fact, not a new run.
            with error ->
                let details = chargeDiagnostics budget [ diagnostic "catalog.readerError" error.Message ]
                lock gate (fun () -> actual.Error <- Some(details |> List.map (fun value -> value.Message) |> String.concat "; "))
                // The exact exception remains in the original preheld faulted task. Its
                // rendered projection is budgeted; it is never printed/retained as extra output.
                raise error)
        let result = { Gate = gate; Bytes = bytes; Eof = false; Error = None; Overflow = false; Task = task }
        record <- Some result
        registerTask custody task
        result
    let private dispatchSpawn (budget: PhaseBudget) (child: ChildCustody) (invoke: unit -> int) =
        // This same actual callsite is exercised with a pure callback by source controls.
        // A scheduled task has no authority to reach a later syscall after WorkEnd.
        checkWork budget
        child.DispatchAttempted <- true
        let result = invoke()
        child.DispatchResult <- Some result
        result
    let private parsePidfdIdentities (rows: string array) =
        let invalid () = unknown "catalog.pidfdIdentity" "The exact returned pidfd identities are missing, invalid or inconsistent."
        let row (prefix: string) =
            match rows |> Array.filter (fun line -> line.StartsWith(prefix, StringComparison.Ordinal)) with
            | [| value |] -> value.Substring(prefix.Length).Split([| ' '; '\t' |], StringSplitOptions.RemoveEmptyEntries)
            | _ -> invalid()
        let positive (value: string) =
            if value.Length = 0 || (value |> Seq.exists (fun character -> character < '0' || character > '9')) then invalid()
            match Int32.TryParse(value, Globalization.NumberStyles.None, Globalization.CultureInfo.InvariantCulture) with
            | true, pid when pid > 0 -> pid
            | _ -> invalid()
        let procPid =
            match row "Pid:" with
            | [| value |] -> positive value
            | _ -> invalid()
        let namespacePids = row "NSpid:" |> Array.map positive
        if namespacePids.Length = 0 || namespacePids[0] <> procPid then invalid()
        procPid, namespacePids[namespacePids.Length - 1]

    let startChild (custody: Custody) (budget: PhaseBudget) (executable: string) (arguments: BoundArgument list) (environment: (string * BoundArgument) list) (cwd: DirectoryCustody) : ChildCustody =
        checkWork budget
        if not (Path.IsPathFullyQualified executable) || executable.Contains(char 0) then refuse "catalog.executableSelection" "An absolute executable is required."
        if (environment |> List.map fst |> List.distinct).Length <> environment.Length then refuse "catalog.environmentDuplicate" "Environment names must be unique."
        for name, _ in environment do
            if String.IsNullOrEmpty name || name.Contains('=') || name.Contains(char 0) then refuse "catalog.environmentName" "Invalid environment name."
        let child =
            { Gate = obj(); Pidfd = None; Pid = None; WaitPid = None; Exit = None; DispatchAttempted = false; DispatchResult = None; Reaped = false; Unknown = false
              Diagnostics = []; Launch = None; Readers = None; Info = allocate custody 128; Owner = (custody :> obj) }
        lock custody.Gate (fun () -> custody.Children.Add child)
        try
            // All buffers, fd roots, pipe ends and cold tasks belong to the operation before Start.
            let roots =
                (arguments @ (environment |> List.map snd))
                |> List.choose (function DirectoryPath(root, _) -> Some root | Literal _ -> None)
                |> List.distinctBy (fun root -> root.Fd)
            let mapping = roots |> List.mapi (fun index root -> root.Fd, index + 3) |> Map.ofList
            let render = function
                | Literal value -> value
                | DirectoryPath(root, relative) ->
                    if relative <> "." then parts relative |> ignore
                    sprintf "/proc/self/fd/%d/%s" mapping[root.Fd] relative
            let stdoutRead, stdoutWrite = pipe custody
            let stderrRead, stderrWrite = pipe custody
            let nullFd = ownFd custody (Native.openFd("/dev/null", 524288))
            let stdout, stderr = reader custody budget false stdoutRead, reader custody budget true stderrRead
            child.Readers <- Some(stdout, stderr)
            let attributes, actions, empty, defaults, slot = allocate custody 336, allocate custody 80, allocate custody 128, allocate custody 128, allocate custody 4
            Marshal.WriteInt32(slot, -1)
            let destroyAttributes = initializedObject budget custody "spawnattr" Native.posix_spawnattr_init Native.posix_spawnattr_destroy attributes
            let destroyActions = initializedObject budget custody "spawn-actions" Native.posix_spawn_file_actions_init Native.posix_spawn_file_actions_destroy actions
            zero "sigemptyset-mask" (Native.sigemptyset empty)
            zero "sigemptyset-defaults" (Native.sigemptyset defaults)
            for signal in [ 2; 13; 15; 17 ] do zero "sigaddset" (Native.sigaddset(defaults, signal))
            zero "spawn-flags" (Native.posix_spawnattr_setflags(attributes, int16 (128 ||| 4 ||| 8)))
            zero "spawn-sigmask" (Native.posix_spawnattr_setsigmask(attributes, empty))
            zero "spawn-sigdefault" (Native.posix_spawnattr_setsigdefault(attributes, defaults))
            // High duplicated source descriptors prevent child target mappings from overwriting an input source.
            let duplicate (fd: int) = ownFd custody (Native.fcntl(fd, 1030, max 64 (roots.Length + 8)))
            let cwdSource = duplicate cwd.Fd
            zero "spawn-fchdir" (Native.posix_spawn_file_actions_addfchdir_np(actions, cwdSource))
            let sources = (roots |> List.map (fun root -> duplicate root.Fd, mapping[root.Fd])) @ [ duplicate nullFd, 0; duplicate stdoutWrite, 1; duplicate stderrWrite, 2 ]
            for source, target in sources do zero "spawn-dup2" (Native.posix_spawn_file_actions_adddup2(actions, source, target))
            zero "spawn-closefrom" (Native.posix_spawn_file_actions_addclosefrom_np(actions, roots.Length + 3))
            let argv = vector custody (executable :: (arguments |> List.map render))
            let env = vector custody (environment |> List.map (fun (name, value) -> name + "=" + render value))
            let launch = new Task<int>(fun () ->
                try
                    let result = dispatchSpawn budget child (fun () -> Native.pidfd_spawn(slot, executable, actions, attributes, argv, env))
                    if result = 0 then
                        // The returned slot is retained before any metadata read that might fail.
                        let pidfd = ownFd custody (Marshal.ReadInt32 slot)
                        lock child.Gate (fun () -> child.Pidfd <- Some pidfd)
                        // procfs exposes the outer identity; waitid reports the caller's namespace identity.
                        // Both are captured from the same held descriptor, never translated or reacquired by PID.
                        let procPid, waitPid = File.ReadAllLines(sprintf "/proc/self/fdinfo/%d" pidfd) |> parsePidfdIdentities
                        lock child.Gate (fun () ->
                            child.Pid <- Some procPid
                            child.WaitPid <- Some waitPid)
                    else lock child.Gate (fun () -> child.Reaped <- true)
                    result
                finally
                    if not child.DispatchAttempted then
                        // This original cold slot never reached the spawn syscall.
                        lock child.Gate (fun () -> child.Reaped <- true)
                    // Dispatch has returned before parent writers close. Reader tasks were preheld,
                    // and their only start is under this same preowned creation task.
                    let cleanup action =
                        try
                            checkCleanup budget
                            action()
                            checkCleanup budget
                        with
                        | CustodyUnknown diagnostics ->
                            child.Unknown <- true
                            child.Diagnostics <- child.Diagnostics @ chargeDiagnostics budget diagnostics
                        | error ->
                            custody.Unknown <- true
                            child.Unknown <- true
                            child.Diagnostics <- child.Diagnostics @ chargeDiagnostics budget [ diagnostic "catalog.launchCleanupUnknown" error.Message ]
                    // Independent cleanup findings cannot replace an earlier launch failure.
                    cleanup (fun () -> destroyAttributes budget)
                    cleanup (fun () -> destroyActions budget)
                    for fd in [ stdoutWrite; stderrWrite; nullFd; cwdSource ] @ (sources |> List.map fst) do cleanup (fun () -> closeFd budget custody fd)
                    cleanup (fun () -> stdout.Task.Start(TaskScheduler.Default))
                    cleanup (fun () -> stderr.Task.Start(TaskScheduler.Default)))
            child.Launch <- Some launch
            registerTask custody launch
            checkWork budget
            launch.Start(TaskScheduler.Default)
            let result = awaitWork budget launch
            if result <> 0 then refuse "catalog.spawnFailed" ("pidfd_spawn returned " + string result)
            child
        with error ->
            // A cold, never-dispatched launch cannot have created a child. Its registered
            // tasks/resources remain held and closeable; a dispatched launch is never guessed absent.
            if (child.Launch |> Option.forall (fun task -> task.Status = TaskStatus.Created)) then child.Reaped <- true
            raise error

    let private snapshot (budget: PhaseBudget) (child: ChildCustody) : ChildObservation =
        let output (reader: Reader) = lock reader.Gate (fun () -> reader.Bytes.ToArray(), reader.Eof, reader.Error, reader.Overflow)
        let stdout, stdoutEof, outError, outOverflow, stderr, stderrEof, errError, errOverflow =
            match child.Readers with
            | Some(first, second) ->
                let a, b, c, d = output first
                let e, f, g, h = output second
                a, b, c, d, e, f, g, h
            | None -> [||], false, None, false, [||], false, None, false
        let errors =
            [ if outOverflow || errOverflow then diagnostic "catalog.outputLimit" "The original phase output limit was exceeded."
              if outError.IsSome || errError.IsSome then diagnostic "catalog.readUnknown" "An original output reader failed." ]
        { ExitCode = child.Exit |> Option.map (fun (code, status) -> if code = 1 then status else 128 + status)
          Stdout = stdout; Stderr = stderr; StdoutEof = stdoutEof; StderrEof = stderrEof
          Settlement =
            if child.Unknown || outError.IsSome || errError.IsSome then Unknown
            elif child.Reaped && stdoutEof && stderrEof then KnownTerminal
            else Pending
          Diagnostics = child.Diagnostics @ chargeDiagnostics budget errors }
    let observeChild (budget: PhaseBudget) (child: ChildCustody) : ChildObservation =
        checkCleanup budget
        match child.Pidfd, child.WaitPid, child.Launch with
        | Some fd, Some pid, Some launch when launch.IsCompleted && child.Exit.IsNone && not child.Reaped && not child.Unknown ->
            let result = Native.waitid(3, uint32 fd, child.Info, 4 ||| 1 ||| 16777216)
            if result <> 0 then
                child.Unknown <- true
                child.Diagnostics <- child.Diagnostics @ chargeDiagnostics budget [ diagnostic "catalog.exitUnknown" ("Original pidfd waitid failed, errno=" + string (Marshal.GetLastPInvokeError())) ]
            else
                let observed = Marshal.ReadInt32(child.Info, 16)
                if observed <> 0 then
                    if observed <> pid then child.Unknown <- true
                    else child.Exit <- Some(Marshal.ReadInt32(child.Info, 8), Marshal.ReadInt32(child.Info, 24))
        | _ -> ()
        snapshot budget child
    let private sessionSettled (pid: int) =
        let mutable leader = false
        let mutable additional = false
        for path in Directory.EnumerateDirectories("/proc") do
            match Int32.TryParse(Path.GetFileName path) with
            | true, candidate ->
                try
                    let text = File.ReadAllText(Path.Combine(path, "stat"))
                    let closing = text.LastIndexOf(')')
                    if closing < 0 then unknown "catalog.sessionUnknown" "A process census row is malformed."
                    let fields = text.Substring(closing + 2).Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    if Int32.Parse(fields[3]) = pid then
                        if candidate = pid && fields[0] = "Z" && Int32.Parse(fields[2]) = pid then leader <- true
                        else additional <- true
                with
                | :? FileNotFoundException | :? DirectoryNotFoundException -> ()
                | error -> unknown "catalog.sessionUnknown" ("The sampled session census is unreadable: " + error.Message)
            | _ -> ()
        leader && not additional
    let settleChild (budget: PhaseBudget) (child: ChildCustody) : ChildObservation =
        let owner = unbox<Custody> child.Owner
        try
            checkCleanup budget
            match child.Launch with
            | Some task -> awaitCleanup budget task |> ignore
            | None -> unknown "catalog.launchUnknown" "An original launch task is unavailable."
            let initial = observeChild budget child
            if initial.Settlement <> KnownTerminal && not child.Reaped && not child.Unknown then
                match child.Pidfd, child.Pid, child.WaitPid with
                | Some fd, Some pid, Some waitPid ->
                    let readersComplete = child.Readers |> Option.exists (fun (a, b) -> a.Task.IsCompleted && b.Task.IsCompleted)
                    if child.Exit.IsNone || not readersComplete then
                        checkCleanup budget
                        let result = Native.pidfd_send_signal(fd, 15, nativeint 0, 4u)
                        if result <> 0 && Marshal.GetLastPInvokeError() <> 3 then unknown "catalog.signalUnknown" "Signalling through the held pidfd failed."
                    let mutable ready = false
                    while not ready do
                        checkCleanup budget
                        observeChild budget child |> ignore
                        if child.Unknown then unknown "catalog.exitUnknown" "Original exit custody became unknown."
                        let readersComplete = child.Readers |> Option.exists (fun (a, b) -> a.Task.IsCompleted && b.Task.IsCompleted)
                        ready <- child.Exit.IsSome && readersComplete && cleanupIo owner budget (fun () -> sessionSettled pid)
                        if not ready then Thread.Sleep(1)
                    checkCleanup budget
                    zero "waitid-reap" (Native.waitid(3, uint32 fd, child.Info, 4))
                    if Marshal.ReadInt32(child.Info, 16) <> waitPid then unknown "catalog.reapUnknown" "The original reap did not report the exact held child."
                    child.Reaped <- true
                    closeFd budget owner fd
                    match child.Readers with
                    | Some _ -> () // Reader pipe descriptors close only during known operation release.
                    | None -> unknown "catalog.readerUnknown" "Original reader custody is unavailable."
                | _ -> unknown "catalog.launchUnknown" "The original successful launch has no complete pidfd identity."
            changed owner
            snapshot budget child
        with
        | CustodyUnknown diagnostics ->
            child.Unknown <- true
            child.Diagnostics <- child.Diagnostics @ chargeDiagnostics budget diagnostics
            changed owner
            snapshot budget child
        | error ->
            child.Unknown <- true
            child.Diagnostics <- child.Diagnostics @ chargeDiagnostics budget [ diagnostic "catalog.retirementUnknown" error.Message ]
            changed owner
            snapshot budget child
    let settled (custody: Custody) =
        lock custody.Gate (fun () ->
            not custody.Unknown
            && (custody.Tasks |> Seq.forall (fun task -> task.Status = TaskStatus.Created || task.IsCompleted))
            && (custody.Children |> Seq.forall (fun child -> not(childUnsettled child))))
    let childIdentities (custody: Custody) =
        lock custody.Gate (fun () ->
            custody.Children |> Seq.map (fun child -> child.Pidfd, child.Pid, child.Reaped, child.Unknown) |> Seq.toList)
    let childLaunchObservations (custody: Custody) =
        lock custody.Gate (fun () ->
            custody.Children |> Seq.map(fun child -> child.DispatchAttempted, child.DispatchResult, child.Pidfd, child.Pid, child.Reaped, child.Unknown) |> Seq.toList)
    let remainingSeconds (budget: PhaseBudget) =
        checkWork budget
        max 1 (int (Math.Ceiling(float (budget.WorkEnd - ticks()) / float Stopwatch.Frequency)))
    let waitForChange (custody: Custody) : Task = lock custody.Gate (fun () -> custody.Change.Task :> Task)
    let releaseKnown (budget: PhaseBudget) (custody: Custody) =
        if not (settled custody) then unknown "catalog.releaseUnsettled" "Native resources remain owned and cannot be disposed."
        lock custody.Gate (fun () ->
            if not custody.Released then
                try
                    for dispose in custody.Disposals do
                        checkCleanup budget
                        dispose budget
                        checkCleanup budget
                    if custody.Unknown then unknown "catalog.releaseUnknown" "An original opaque native object failed retirement."
                    for fd in custody.Descriptors.ToArray() do closeFd budget custody fd
                    for pointer in custody.Utf8.ToArray() do
                        checkCleanup budget
                        custody.UncertainUtf8.Add pointer
                        custody.Utf8.Remove pointer |> ignore
                        checkCleanup budget
                        Marshal.FreeCoTaskMem pointer
                        custody.UncertainUtf8.Remove pointer |> ignore
                        checkCleanup budget
                    for pointer in custody.Pointers.ToArray() do
                        checkCleanup budget
                        custody.UncertainPointers.Add pointer
                        custody.Pointers.Remove pointer |> ignore
                        checkCleanup budget
                        Marshal.FreeHGlobal pointer
                        custody.UncertainPointers.Remove pointer |> ignore
                        checkCleanup budget
                    checkCleanup budget
                    custody.Released <- true
                with error ->
                    // No uncertain/consumed slot is reattached or retried. Actual remaining
                    // descriptors/allocations and the original owner stay held after refusal.
                    custody.Unknown <- true
                    raise error)
