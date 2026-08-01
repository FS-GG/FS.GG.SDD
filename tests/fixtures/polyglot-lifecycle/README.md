# Polyglot lifecycle acceptance fixture

This fixture deliberately has four independently executable lanes: an ASP.NET
Core F# server test project that produces TRX through `dotnet test`, a compiled
TypeScript/browser lane that produces JUnit through Node's built-in reporter, a
no-npm F# console, and a package-producing Fable bindings project whose npm
compile/runtime command runs its generated JavaScript. It has no provider identity
or language classification in SDD; the acceptance test supplies the server/client
reports to one ordinary lifecycle work item and drives it through verify, ship, and doctor.

Run the lanes from this directory:

```sh
dotnet test server.tests/Polyglot.Server.Tests.fsproj --logger "trx;LogFileName=server.trx" --results-directory results
npm --prefix client test > results/client.junit.xml
```
