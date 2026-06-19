namespace FS.GG.SDD.Artifacts

module SchemaVersion =
    type SchemaVersion = { Major: int; Minor: int option; Raw: string }
    type SourceDigest = { Algorithm: string; Value: string }
    type OutputDigest = { Algorithm: string; Value: string }
    type GeneratorVersion = { Id: string; Version: string }

    val create: major: int -> SchemaVersion
    val parse: value: string -> Result<SchemaVersion, string>
    val isSupported: version: SchemaVersion -> bool
    val createSourceDigest: algorithm: string -> value: string -> Result<SourceDigest, string>
    val createOutputDigest: algorithm: string -> value: string -> Result<OutputDigest, string>
    val sha256Text: text: string -> SourceDigest
    val outputSha256Text: text: string -> OutputDigest
    val createGeneratorVersion: id: string -> version: string -> Result<GeneratorVersion, string>
