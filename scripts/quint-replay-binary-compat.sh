#!/usr/bin/env bash
# Compile once against the previous public CLR API, then run unchanged against the candidate.
set -euo pipefail
version=${1:?candidate SDD version required}
source=${2:-https://api.nuget.org/v3/index.json}
scratch=$(mktemp -d)
trap 'rm -rf "$scratch"' EXIT
mkdir -p "$scratch/old" "$scratch/host"
cat > "$scratch/NuGet.Config" <<CONFIG
<configuration><packageSources><clear/><add key="candidate" value="$source"/><add key="public" value="https://api.nuget.org/v3/index.json"/></packageSources></configuration>
CONFIG
cat > "$scratch/old/OldClient.fsproj" <<'PROJECT'
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><DisableImplicitFSharpCoreReference>true</DisableImplicitFSharpCoreReference></PropertyGroup><ItemGroup><PackageReference Include="FSharp.Core" Version="10.1.302"/><PackageReference Include="FS.GG.SDD.Artifacts" Version="2.0.1"/><Compile Include="Library.fs"/></ItemGroup></Project>
PROJECT
cat > "$scratch/old/Library.fs" <<'FS'
module OldClient
open FS.GG.SDD.Artifacts.TypedSpecifications
let unwrap = function Ok x -> x | Error e -> failwithf "%A" e
let run () =
    let values = Record ["a", Set [Integer "2";Integer "+0001"]; "b", Sequence [Null;Boolean true;Text "λ😀"]]
    let draft = { Identity="";Bindings=["value",values] }
    let state = {draft with Identity=QuintReplay.stateFingerprint draft |> unwrap}
    let sha = String.replicate 64 "a"
    let env = {Seed="42";Bounds=["steps",1L];ToolFingerprint=sha;ProfileFingerprint=sha;ContractFingerprint=sha;AdapterFingerprint=sha;ImplementationFingerprint=sha}
    let source = {Path="old.qnt";Line=1;Column=1}
    let draft = {SchemaVersion=1;TraceIdentity="";Environment=env;Initial=state;Steps=[{Index=1;Action="same";Source=source;Expected=state}]}
    let trace = {draft with TraceIdentity=QuintReplay.traceFingerprint draft |> unwrap}
    if not (QuintReplay.validateTrace trace).IsEmpty then failwith "Invalid trace"
    QuintReplay.encodeValue values |> unwrap |> ignore
    QuintReplay.encodeState state |> unwrap |> ignore
    let observation = {Index=1;Action="same";Source=source;Actual=state}
    if QuintReplay.compare trace [observation] <> Ok QuintReplayResult.Equivalent then failwith "Not equivalent"
    match QuintReplay.compare trace [] |> unwrap with
    | QuintReplayResult.Diverged d when d.Expected=Some state && d.Actual=None -> ()
    | _ -> failwith "Lost divergence CLR union/record shape"
    let context = {Environment=env;Steps=[]}
    let raw = "{\"#meta\":{\"format\":\"ITF\",\"format-description\":\"ITF\",\"source\":\"old.qnt\",\"status\":\"ok\"},\"vars\":[\"x\"],\"states\":[{\"#meta\":{\"index\":0},\"x\":1}]}"
    QuintReplay.decodeItf context raw |> unwrap |> ignore
    printfn "Previous compiled API consumer passed: %s" typeof<QuintReplayState>.Assembly.FullName
FS
export NUGET_PACKAGES="$scratch/packages"
export NUGET_HTTP_CACHE_PATH="$scratch/http"
dotnet build "$scratch/old/OldClient.fsproj" -c Release
cat > "$scratch/host/Host.fsproj" <<PROJECT
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><DisableImplicitFSharpCoreReference>true</DisableImplicitFSharpCoreReference></PropertyGroup><ItemGroup><PackageReference Include="FSharp.Core" Version="10.1.302"/><PackageReference Include="FS.GG.SDD.Artifacts" Version="$version"/><Reference Include="OldClient"><HintPath>../old/bin/Release/net10.0/OldClient.dll</HintPath></Reference><Compile Include="Program.fs"/></ItemGroup></Project>
PROJECT
printf 'OldClient.run ()\n' > "$scratch/host/Program.fs"
dotnet run --project "$scratch/host/Host.fsproj" -c Release
