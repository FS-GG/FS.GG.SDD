# Physical Generation Source Snapshot

**Status:** provisional FSC-04 source preparation, stacked on draft #1000. **Owner:** FS.GG.SDD.

## Outcome

A caller can capture a closed directory of generation source files into immutable byte and digest snapshots. A missing, extra, aliased, escaping, symlink, or nonregular entry refuses before any snapshot is returned. The caller supplies an explicit digest policy; this adapter does not choose a producer contract or write a generated view.

## Requirements

- FR-001: Enumerate every entry under one declared closed root without following directory links; require an exact, nonempty set of repository-relative declared file paths.
- FR-002: Reject unsafe root/path spelling, duplicate or ordinal case-colliding declarations and filesystem entries, symlinks, nonregular entries, missing and extra files, and unreadable bytes.
- FR-003: Capture each file's raw bytes once and return defensive byte copies with a digest under the explicitly selected exact-byte or UTF-8 LF-text policy. A later file mutation changes a later capture, not the prior snapshot.
- FR-004: This adapter has no output path or mutation capability. Refusal cannot remove a prior generated view. A later output-owning adapter must separately prove atomic replacement and rollback.

## Boundary

The closed root and digest policy are caller-supplied because no generic producer contract yet selects them. This module does not construct or publish a generation manifest, pin a receiver, or claim installed parity. Adoption needs an exact producer contract, adapter/package qualification, and receiver pin.

Linux capture opens every workspace ancestor and closed-root directory through a descriptor-relative, non-following `openat`, then opens each source file through its pinned parent with `O_NOFOLLOW | O_NONBLOCK`. The opened descriptor's type is checked before its bytes are read; a controlled path swap after open leaves that descriptor bound to the original file, while the complete capture can refuse the observed directory mutation. Directory enumeration uses `/proc/self/fd/<directory>` and resolves each returned name through the pinned directory descriptor. A concurrent replacement between enumeration and open may produce a refusal or a different regular file observed at open; this does not establish a coherent point-in-time snapshot of a mutating tree. Windows retains path-based reparse-point and attribute checks and remains unqualified against concurrent swaps. Installed use still requires producer contract selection and platform-specific qualification.

The Linux reader now compares two name scans and supported inode, size, mtime, and ctime fields from each held directory, then rechecks the directory after visiting its children. This refuses observed additions and rename/restore mutations after the first scan. The check still cannot prove a simultaneous tree snapshot: mutation after the final check, ABA, and filesystem timestamp limits remain. The Windows path remains unqualified for this race.
