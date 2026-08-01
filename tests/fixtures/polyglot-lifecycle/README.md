# Polyglot lifecycle acceptance fixture

This fixture deliberately has two independently executable lanes: an F# server
test project that produces TRX through `dotnet test`, and a Node test lane that
produces JUnit through Node's built-in reporter. It has no provider identity or
language classification in SDD; the acceptance test supplies their reports to
one ordinary lifecycle work item.

Run the lanes from this directory:

```sh
dotnet test server.tests/Polyglot.Server.Tests.fsproj --logger "trx;LogFileName=server.trx" --results-directory results
npm --prefix client test > results/client.junit.xml
```
