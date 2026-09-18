# Implementation plan

Keep QuintReplay.fsi byte-identical. Keep record/union definitions in QuintReplay.fs
in their existing assembly. Replace ReplayInternal and generic algorithms with typed
bidirectional conversions and calls into FsQuint. Conversions do not canonicalize,
parse JSON, hash, compare or validate. Pin FsQuint in the local central package file.

Use existing semantic tests as the before/after contract; add a package/facade parity
case and compile an old consumer before replacing the body. Run the artifact test
project and release gates. Publish the SDD coherent Artifacts/CLI set under the
existing release workflow only after the public upstream package is available.

No machine schema, lifecycle generated view, agent contract or optional Governance
integration changes. The authored spec/plan/tasks are authoritative for this work;
the upstream roadmap tracks cross-repository completion. Core logic remains pure,
so this translation layer requires no new MVU or I/O boundary.

FSharp.Core retains the organization floor. FsQuint itself uses the latest stable
.NET SDK 10.0.401, as explicitly requested by the user.
