# Plan: Physical Generation Source Snapshot

**Source:** [spec.md](spec.md). **Route:** provisional internal Commands adapter.

Add one internal module in Commands, after its Artifacts reference, with a closed-root capture function and defensive snapshot type. Keep the public package API and Artifacts comparator unchanged. Tests use disposable physical trees and independent negative controls. Validate the Commands test project with locked restore, focused tests, and its public-surface gate.

The adapter owns reads only. A future producer must select the closed root and digest policy, then re-read current sources before calling the pure comparator from #1000. Output staging and rollback require a separate effect owner and qualification.

The provisional Linux `statx` and Windows attribute probes refuse symlink and special-file facts without following links. They are not a race-free handle binding; the future producer/adapter contract must close that gap before installation.
