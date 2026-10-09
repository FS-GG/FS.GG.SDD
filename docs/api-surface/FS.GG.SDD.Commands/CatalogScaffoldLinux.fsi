namespace FS.GG.SDD.Commands

/// Internal first-host interop shape; declarations alone establish no ABI or native acceptance.
module internal CatalogScaffoldLinux =
    type AbiLayout =
        {
            SpawnAttributesBytes: int
            SpawnAttributesAlignment: int
            SpawnFileActionsBytes: int
            SpawnFileActionsAlignment: int
            SignalInfoBytes: int
            SignalInfoAlignment: int
            PollDescriptorBytes: int
            StatxBytes: int
        }

    type AbiObservation =
        {
            LibcVersion: string
            Layout: AbiLayout
            RequiredExports: (string * bool) list
        }

    /// Holds an opened physical directory object, separately from observed pathname binding.
    type DirectoryCustody
    /// Holds pidfd, launch/read tasks and buffers; no numeric PID reacquisition or implicit disposal.
    type ChildCustody

    type Settlement =
        | Pending
        | KnownTerminal
        | Unknown

    /// One caller-held resource inventory, constructed before any native dispatch.
    type Custody
    type PhaseBudget

    type ObjectIdentity =
        {
            DeviceMajor: uint32
            DeviceMinor: uint32
            Inode: uint64
            Mount: uint64
            Kind: int
        }

    type CapturedFile =
        {
            Path: string
            Bytes: byte array
            Sha256: string
        }

    type BoundArgument =
        | Literal of string
        | DirectoryPath of DirectoryCustody * string

    type ChildObservation =
        {
            ExitCode: int option
            Stdout: byte array
            Stderr: byte array
            StdoutEof: bool
            StderrEof: bool
            Settlement: Settlement
            Diagnostics: Fsgg.ProviderCatalog.Diagnostic list
        }

    /// Refusal remains separate from unknown custody; neither is positive execution evidence.
    exception Refused of Fsgg.ProviderCatalog.Diagnostic list
    exception CustodyUnknown of Fsgg.ProviderCatalog.Diagnostic list
    val createCustody: unit -> Custody

    /// Caller owns the preheld library slot before probing; original phase governs its retirement.
    /// This probe is permitted only after the exact first-host ABI fixture qualification.
    val observeAbi:
        custody: Custody -> budget: PhaseBudget -> Result<AbiObservation, Fsgg.ProviderCatalog.Diagnostic list>

    /// Whole-phase ceiling includes cleanup; no child obtains a new deadline.
    val beginPhase: seconds: int -> cancellation: System.Threading.CancellationToken -> PhaseBudget

    /// Charges bounded retained diagnostics against the same aggregate original phase output ceiling.
    val chargeDiagnostics:
        budget: PhaseBudget -> diagnostics: Fsgg.ProviderCatalog.Diagnostic list -> Fsgg.ProviderCatalog.Diagnostic list

    val stopUsefulWork: budget: PhaseBudget -> unit
    val checkWork: budget: PhaseBudget -> unit
    val checkCleanup: budget: PhaseBudget -> unit
    val openDirectory: custody: Custody -> budget: PhaseBudget -> absolute: string -> DirectoryCustody
    val identity: directory: DirectoryCustody -> ObjectIdentity
    val revalidateDirectory: budget: PhaseBudget -> directory: DirectoryCustody -> unit

    val createDirectory:
        custody: Custody -> budget: PhaseBudget -> parent: DirectoryCustody -> name: string -> DirectoryCustody

    val readFile:
        custody: Custody ->
        budget: PhaseBudget ->
        root: DirectoryCustody ->
        relative: string ->
        maximumBytes: int ->
            CapturedFile

    val writeFile:
        custody: Custody ->
        budget: PhaseBudget ->
        root: DirectoryCustody ->
        relative: string ->
        bytes: byte array ->
        replaceKnown: bool ->
            unit

    /// Adds executable bits to an already captured singly linked regular file through its held fd.
    /// Caller selects only newly owned composition files; identity, binding and original budget are rechecked.
    val setExecutable: custody: Custody -> budget: PhaseBudget -> root: DirectoryCustody -> relative: string -> unit

    val files:
        custody: Custody -> budget: PhaseBudget -> root: DirectoryCustody -> maximumBytes: int -> CapturedFile list

    /// Exactly one syscall. Success is publication into the held parent object, not pathname locking.
    val commitNoReplace:
        budget: PhaseBudget ->
        sourceParent: DirectoryCustody ->
        source: DirectoryCustody ->
        sourceLeaf: string ->
        targetParent: DirectoryCustody ->
        targetLeaf: string ->
            CatalogScaffoldWorkflow.CommitOutcome

    /// Establishes first identities for newly created private staging entries only after
    /// actual child/task settlement. Existing identities are never adopted or reset.
    /// The caller must serialize same-custody producers, stop every relevant original
    /// useful-work budget, and establish previous child/read/task settlement first.
    /// The snapshot check does not exclude concurrent task registration or Start.
    /// Metadata capture uses the original cleanup authority; it reads no payload and
    /// authorizes no unlink. Unknown or substituted entries retain original custody.
    val captureRetirementInventory: custody: Custody -> budget: PhaseBudget -> root: DirectoryCustody -> unit

    /// Descriptor-relative retirement refuses substituted entries and unknown children/tasks.
    val retireDirectory:
        custody: Custody ->
        budget: PhaseBudget ->
        parent: DirectoryCustody ->
        name: string ->
        root: DirectoryCustody ->
            unit

    /// Registers pidfd/start/read tasks and buffers before dispatch; arguments stay ordinal.
    val startChild:
        custody: Custody ->
        budget: PhaseBudget ->
        executable: string ->
        arguments: BoundArgument list ->
        environment: (string * BoundArgument) list ->
        cwd: DirectoryCustody ->
            ChildCustody

    val observeChild: budget: PhaseBudget -> child: ChildCustody -> ChildObservation
    val settleChild: budget: PhaseBudget -> child: ChildCustody -> ChildObservation

    /// Exact held slots; missing identity or unknown retirement stays visible, never reacquired.
    /// Raw one-use rename facts survive task timeout; the original Unknown flag never upgrades.
    val commitObservations:
        custody: Custody -> (bool * int option * int option * CatalogScaffoldWorkflow.CommitOutcome * bool) list

    val childIdentities: custody: Custody -> (int option * int option * bool * bool) list
    /// Dispatch attempt/result are distinct from scheduling, slot settlement and physical child/reap.
    val childLaunchObservations: custody: Custody -> (bool * int option * int option * int option * bool * bool) list
    /// Remaining original useful-work time, rounded up for canonical invocation metadata.
    val remainingSeconds: budget: PhaseBudget -> int
    val settled: custody: Custody -> bool
    val waitForChange: custody: Custody -> System.Threading.Tasks.Task
    val releaseKnown: budget: PhaseBudget -> custody: Custody -> unit
