# Plan: Physical Generation Source Snapshot

**Source:** [spec.md](spec.md). **Route:** provisional internal Commands adapter.

Add one internal module in Commands, after its Artifacts reference, with a closed-root capture function and defensive snapshot type. Keep the public package API and Artifacts comparator unchanged. Tests use disposable physical trees and independent negative controls. Validate the Commands test project with locked restore, focused tests, and its public-surface gate.

The adapter owns reads only. A future producer must select the closed root and digest policy, then re-read current sources before calling the pure comparator from #1000. Output staging and rollback require a separate effect owner and qualification.

The Linux adapter pins directory descriptors from `/` through the caller's workspace and closed root. It enumerates through `/proc/self/fd/<dirfd>`, resolves each entry with `openat` and `O_NOFOLLOW`, checks the opened descriptor with `statx(AT_EMPTY_PATH)`, and reads bytes from that descriptor. `O_NONBLOCK` keeps a raced FIFO from blocking before type inspection. Keep the old path-based reader only as a characterization control. Controlled swaps verify that a link installed before descriptor open refuses, and a link installed after open leaves the pinned read on original bytes while the old reader demonstrably yields foreign bytes. Windows retains the path-based implementation and no installed contract is claimed. A future producer must define whether concurrent regular-file replacement or in-place mutation is permissible and qualify a work-model multi-root selection before publication.
