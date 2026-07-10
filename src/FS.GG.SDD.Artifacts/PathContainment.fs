namespace FS.GG.SDD.Artifacts

open System

module PathContainment =
    let escapesRoot (raw: string) =
        let trimmed = raw.Trim().Replace('\\', '/')

        String.IsNullOrWhiteSpace trimmed
        || IO.Path.IsPathRooted trimmed // on the RAW string, before any TrimStart('/')
        || (trimmed.Split('/') |> Array.contains "..")
