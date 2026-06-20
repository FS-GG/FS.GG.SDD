# Refresh Performance Evidence

Wall-clock for three consecutive `fsgg-sdd refresh` runs over the disposable
shipped project (local dev machine), each well under the 3s/run harness budget
(plan Performance Goals). Times include dotnet host startup overhead, so the
command-only cost is lower. The in-process command-test harness runs the same
scenarios faster still (see full-suite.txt: 223 command tests in ~9s).

```
run1 real=976ms
run2 real=1001ms
run3 real=1026ms
```
