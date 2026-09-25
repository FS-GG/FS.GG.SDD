# Plan: Duplicate Work-ID Candidate Boundary

1. Copy the normalized valid work-model source fixture to a disposable directory and prove #1024's pinned physical preview passes.
2. Add a separately read sibling spec that claims the selected logical work ID. Verify the existing command diagnostic and producer's empty output-effect plan while the source-only preview still passes.
3. Change that sibling to its own work ID as an independent negative control.
4. Record the required producer-owned candidate-inventory closure before any live use; run focused and full Commands tests and warning-clean Release build.

No production generation effect, staging write or receiver action is introduced.
