# Feature Specification: Scaffold owns repo-init & script-executable post-instantiation steps

**Feature Branch**: `032-scaffold-repo-init-chmod`

**Created**: 2026-06-27

**Status**: Ready (spec + plan + tasks complete; analyze pass applied)

**Input**: User description: "next non blocked sdd item on the project coordination board" — resolved to Coordination board item **Issue [FS-GG/FS.GG.SDD#1](https://github.com/FS-GG/FS.GG.SDD/issues/1) — "[cross-repo] Scaffold path must own git-init/chmod after fs-gg-ui Feature 205 (side-effect-free generation)"** (status **Ready**, phase **P2 SDD**, the only non-blocked SDD-owned item on the board).

**Change Tier**: Tier 1 (contracted change) — this feature implements the SDD-side obligations (S1–S3) of the published, Accepted cross-repo contract `fs-gg-ui-template-generation` (§5/§6) under ADR-0002, recorded in the registry as `fs-gg-ui-template.behavior-break`. It adds new observable scaffold behavior (post-instantiation repository initialization and shell-script executability) and the report/diagnostic surfaces that make those steps observable. No change is made to the provider contract, the provider invocation protocol, or the `scaffold-provenance.json` schema; the new behavior is generic orchestration that runs on the scaffold path for **any** provider.

## Context & Boundary

`fsgg-sdd scaffold --provider <name> [--param key=value ...]` establishes the SDD
skeleton (reusing `init`'s effects), invokes an external template provider via a
generic `dotnet new` wrapper at the MVU `RunProcess` edge, and records
`.fsgg/scaffold-provenance.json` (produced paths marked `generatedProduct`).

The reference provider's template (`fs-gg-ui`, in the **FS.GG.Rendering** repo)
has become **side-effect-free by default** (Rendering Feature 205): generation
now spawns no process, creates no git repository, and makes no script executable.
The template's auto-run git-init/chmod post-actions were removed and the
`skipGitInit` opt-out deleted; a new opt-in (`initGit`) exists only for direct
callers and requires `--allow-scripts yes` non-interactively.

Consequently the convenience that used to arrive "for free" from the template —
a freshly generated product sitting in an initialized git repository with its
shell scripts already executable — no longer happens. Per ADR-0002
(lifecycle/orchestration ownership lives on the scaffold path), **SDD must now own
those steps itself**, as explicit, observable post-instantiation actions, after a
side-effect-free provider instantiation.

This feature does **not** add any rendering-, template-, or provider-specific
knowledge to generic SDD. Repository initialization operates on the scaffolded
product root; making scripts executable operates on the scaffolded shell scripts
themselves. The motivating provider is `rendering`, but the behavior is generic
and applies on the scaffold path regardless of which provider was named.

This closes the P2 SDD scaffold-path-parity gap (the contract's quickstart
Scenario H / SC-003) so that a one-command scaffold reaches a runnable,
version-controlled initial state without the author hand-running `git init` or
`chmod`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Scaffolded product lands in an initialized git repository (Priority: P1)

A product author scaffolds into an empty directory that is **not** already inside
a git work tree. After the provider produces the app tree, scaffold initializes a
git repository at the scaffolded product root as an explicit post-instantiation
step, so the author's first action can be a normal commit rather than `git init`.

**Why this priority**: This is the headline parity the contract requires. With the
template now side-effect-free, a scaffold that did nothing here would leave every
new product un-versioned — a visible regression from the prior one-command
experience and a blocker for the downstream P4 Templates work that assumes the
orchestrated product reaches its initial repository state.

**Independent Test**: Run scaffold in a temporary empty directory (not inside a
repo) with a repo-owned fixture provider; assert a git repository exists at the
product root afterward, that it contains the scaffolded tree, and that the report
states the repository was initialized.

**Acceptance Scenarios**:

1. **Given** an empty target directory that is not inside any git work tree and a
   registered provider, **When** the author runs scaffold, **Then** after the
   provider succeeds a git repository is initialized at the product root and the
   report records that the repository was initialized.
2. **Given** the initialized repository, **When** its contents are inspected,
   **Then** it spans the complete scaffolded tree — the SDD skeleton, the
   provider's product files, and the `scaffold-provenance.json` record — as the
   product's initial repository state.
3. **Given** a provider run that fails (non-zero exit, empty output treated as
   failure, or an SDD-tree intrusion), **When** scaffold finalizes, **Then** no
   repository is initialized over the incomplete scaffold and the existing failure
   outcome and diagnostic are reported unchanged.

---

### User Story 2 - Generated shell scripts are executable without manual chmod (Priority: P1)

After the provider produces the app tree, scaffold makes the scaffolded shell
scripts executable as an explicit post-instantiation step, so the author can run
the product's scripts immediately rather than first discovering and fixing
permission bits the side-effect-free template no longer sets.

**Why this priority**: The second half of the contract's S1 obligation. Without
it, scripts the product ships (build/run helpers) are non-executable on a fresh
scaffold, breaking the "build and run immediately" promise of scaffold.

**Independent Test**: With a fixture provider that emits one or more shell scripts,
run scaffold and assert each scaffolded shell script carries an executable bit and
that the report records how many scripts were made executable.

**Acceptance Scenarios**:

1. **Given** a provider that produces one or more shell scripts, **When** scaffold
   finalizes, **Then** each scaffolded shell script is left executable and the
   report records the count made executable.
2. **Given** a provider that produces no shell scripts, **When** scaffold
   finalizes, **Then** the make-executable step is a no-op, succeeds, and the
   report records that zero scripts were affected.
3. **Given** the make-executable step cannot be applied to a file (e.g.,
   read-only filesystem), **When** scaffold finalizes, **Then** the step is
   reported as skipped/partial without converting the scaffold's success outcome
   into a defect.

---

### User Story 3 - Safeguards keep the steps safe and non-fatal across environments (Priority: P2)

A product author may scaffold into a directory that already sits inside a git work
tree, or on a machine where git is not installed. The post-instantiation steps
must never harm the surrounding environment and must never turn an otherwise
successful scaffold into a failure: an existing repository is left untouched (no
nested repository), an absent git is skipped non-fatally, and every choice is
reported.

**Why this priority**: These are the S2 safeguards. They are essential for
correctness (never nest a repo, never hang/fail on missing git) but are
conditioning around the P1 happy path rather than the core value, so they sit at
P2.

**Independent Test**: Run scaffold (a) inside an existing git work tree and assert
no nested repository is created and the report says repo-init was skipped because a
repository already exists; (b) with git unavailable and assert scaffold still
reaches its normal success outcome with repo-init reported as skipped.

**Acceptance Scenarios**:

1. **Given** a target directory that is already inside a git work tree, **When**
   scaffold runs, **Then** no nested repository is initialized and the report
   records repo-init as skipped (repository already exists).
2. **Given** an environment where git is not available, **When** scaffold runs,
   **Then** repository initialization is skipped non-fatally, the scaffold reaches
   its normal success outcome, and the report records the skip with its reason.
3. **Given** a successful scaffold where a convenience step was skipped (existing
   repo, git absent, or no scripts), **When** the result is reported, **Then** the
   scaffold is **not** reported as failed or incomplete on account of the skip.

---

### User Story 4 - The steps are generic and leak no provider specifics (Priority: P2)

The post-instantiation behavior is part of generic SDD orchestration. It must work
for any provider and must not encode any rendering-, template-, or
provider-specific package id, template id, path, script name, or docs URL.

**Why this priority**: The boundary invariant that the whole scaffold design rests
on (scaffold FR-002 / SC-005). It is a P2 guardrail rather than P1 user value, but
a violation would corrupt the architecture, so it is verified explicitly.

**Independent Test**: Run scaffold against a non-rendering fixture provider and
assert the same repo-init and make-executable behavior occurs; scan the generic
SDD sources changed by this feature and assert they contain no provider-specific
identifier.

**Acceptance Scenarios**:

1. **Given** any registered provider (not the rendering reference), **When**
   scaffold runs, **Then** the repo-init and make-executable steps apply
   identically, driven only by the scaffolded tree.
2. **Given** the generic SDD sources implementing this feature, **When** they are
   inspected, **Then** they contain no provider-, template-, or rendering-specific
   package id, template id, path, script name, or docs URL.
3. **Given** the side-effect-free provider instantiation, **When** scaffold
   obtains git-init and executability, **Then** it performs them itself and does
   not pass provider-specific git options (e.g., `initGit`/`allow-scripts`) to the
   provider to obtain them.

---

### Edge Cases

- **Scaffolding inside an existing repository (subdirectory of a repo)**: existing
  work-tree detection short-circuits repo-init; no nested repository is created.
- **git absent / not on PATH**: repo-init is skipped non-fatally; scaffold still
  succeeds; the skip and its reason are reported.
- **Provider produced no shell scripts**: make-executable is a reported no-op.
- **Provider produced no files at all (empty-success outcome)**: repo-init still
  initializes a repository over the SDD skeleton + provenance (the scaffolded tree
  is non-empty); make-executable is a no-op for provider scripts.
- **Provider failed, produced nothing usable, or wrote into an SDD-owned tree**:
  post-instantiation steps do not run; the existing failure outcome/diagnostic and
  exit code are preserved.
- **Re-run / `--force` into an already-scaffolded directory**: existing-repo
  detection prevents nesting; re-applying executable bits is idempotent and safe.
- **Read-only or restricted filesystem**: a step that cannot be applied is reported
  as skipped/partial rather than failing the whole scaffold.
- **Dry run**: scaffold describes the planned post-instantiation steps but performs
  none of them.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: After a provider instantiation that succeeds (including the
  empty-but-successful outcome), scaffold MUST initialize a git repository at the
  scaffolded product root as an explicit post-instantiation step, no longer relying
  on the provider or its template to do so.
- **FR-002**: Scaffold MUST detect whether the target is already inside a git work
  tree and, when it is, MUST NOT initialize a nested repository; the step is skipped
  and reported.
- **FR-003**: When git is unavailable, repository initialization MUST be skipped
  non-fatally — the scaffold's success outcome and exit code MUST be unaffected —
  and the skip MUST be reported with its reason.
- **FR-004**: When a repository is initialized, it MUST capture the complete
  scaffolded tree — the SDD skeleton, the provider's product files, and the
  `scaffold-provenance.json` record — as the product's initial repository state.
- **FR-005**: Scaffold MUST make the scaffolded shell scripts executable as an
  explicit post-instantiation step; when there are no such scripts the step MUST be
  a reported no-op. "Shell scripts" are identified generically by `.sh` file shape
  (see FR-006 and Assumptions); scaffolded scripts that carry no `.sh` extension are
  out of scope for this step.
- **FR-006**: Identification of which scaffolded files are shell scripts, and of the
  product root, MUST be generic — derived from the scaffolded tree itself — and MUST
  NOT rely on any provider-, template-, or rendering-specific package id, template
  id, path, or script name.
- **FR-007**: Scaffold MUST perform these steps itself after a side-effect-free
  provider instantiation and MUST NOT pass provider-specific options (e.g.,
  `initGit`, `allow-scripts`) to the provider in order to obtain git-init or script
  executability.
- **FR-008**: The post-instantiation steps MUST run only on the real execution path.
  Under dry run, scaffold MUST describe the planned steps without performing any of
  them.
- **FR-009**: The post-instantiation steps MUST run only after a successful provider
  instantiation. On a provider-failure path (non-zero exit, empty output treated as
  failure, or SDD-tree intrusion) they MUST NOT run, and the existing failure
  outcome, diagnostic, and exit code MUST be preserved.
- **FR-010**: Skipping a convenience step (existing repository, git absent, or no
  scripts) MUST NOT cause the scaffold to be reported as failed or incomplete, and a
  successful scaffold MUST NOT be reported as complete if the underlying
  instantiation actually failed (no false complete/incomplete).
- **FR-011**: All three report projections (default/JSON, `--text`, `--rich`) MUST
  surface, for each run, the outcome of each post-instantiation step: whether the
  repository was initialized, skipped because a repository already exists, or skipped
  because git is unavailable; and how many shell scripts were made executable (or
  that none were). The JSON projection MUST remain the deterministic automation
  contract; `--rich` MUST add and drop no facts and change no JSON byte.
- **FR-012**: The post-instantiation steps MUST NOT introduce nondeterministic
  content into SDD's deterministic artifacts or JSON output. Creating the repository
  and adjusting file permissions are filesystem side-effects and MUST NOT alter the
  byte-determinism of `scaffold-provenance.json` or the report JSON.
- **FR-013**: Repository initialization MUST be idempotent with respect to re-runs:
  running scaffold again into a directory that is already inside a work tree MUST be
  treated as the existing-repository case (no nesting), and re-applying executable
  bits MUST be safe.

### Key Entities *(include if feature involves data)*

- **Repo-init step outcome**: the reported result of the repository-initialization
  step for a run — one of *initialized*, *skipped: repository already exists*, or
  *skipped: git unavailable* — surfaced in every report projection.
- **Make-executable step outcome**: the reported result of the make-executable step
  — the count of scaffolded shell scripts made executable (zero when none), plus any
  skipped/partial indication when a permission change could not be applied.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In 100% of runs where the target is not already inside a git work tree
  and git is available, the scaffolded product ends up in an initialized git
  repository at its root.
- **SC-002**: Scaffold creates a nested git repository in 0% of runs that occur
  inside an existing git work tree.
- **SC-003**: 100% of scaffolded `.sh` shell scripts are left executable, with no
  manual `chmod` performed by the author.
- **SC-004**: When git is unavailable, scaffold still completes with its normal
  success outcome in 100% of otherwise-successful runs; 0 hard failures are
  attributable to missing git.
- **SC-005**: This feature introduces 0 provider-, template-, or rendering-specific
  identifiers into generic SDD (the scaffold leak invariant continues to hold), and
  the repo-init and make-executable behavior is observed identically for a
  non-rendering provider.
- **SC-006**: For every run, a reader of any single report projection can determine
  which post-instantiation steps ran, which were skipped, and why — without
  inspecting the filesystem.

## Assumptions

- **"Initial repository state" means an initialized repository, not an initial
  commit.** Repository initialization creates a git repository at the product root
  containing the scaffolded working tree; making the first commit remains the
  author's responsibility. This matches the prior template behavior (a `git init`
  post-action) and the contract's "git-init" phrasing. (Revisit only if downstream
  consumers require a seeded initial commit.)
- **Existing-repository detection uses the standard git work-tree check**
  (`git rev-parse --is-inside-work-tree`), as named in the contract's S2.
- **"Shell scripts" are identified generically** from the scaffolded tree (e.g., by
  shell-script file shape such as a `.sh` extension); the exact discovery rule is a
  planning detail and carries no provider-specific knowledge.
- **The steps execute via the existing MVU process/permission edges** (consistent
  with the existing `dotnet new` `RunProcess` edge); the only new external tool is
  git, which is treated as optional.
- **The provider continues to be invoked with side-effect-free generation.** SDD
  does not pass `initGit`/`allow-scripts` to the provider; it performs the
  convenience steps itself, per the contract's S3 and prefer-self guidance.
- **Dependencies**: the published `fs-gg-ui-template-generation` contract (§5 S1–S3,
  §6 migration notes) and ADR-0002 in `FS-GG/.github`; the registry entry
  `fs-gg-ui-template.behavior-break` and its compatibility projection. This feature
  is the SDD-side execution of that contract; FS.GG.Rendering delivers the
  side-effect-free template and the published contract only.
