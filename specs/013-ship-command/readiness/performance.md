# Performance Evidence — Ship Command

`ship create and rerun complete under local harness budget` asserts ship create and
rerun each complete in under 2 seconds through the command test harness on the local
development machine. The test passes (see `command-ship-tests.txt`).

- ship-create: < 2s (in-process harness)
- ship-rerun-current: < 2s (in-process harness)
- ship-refreshes-work-model: covered by the create path (work-model is refreshed each run)
