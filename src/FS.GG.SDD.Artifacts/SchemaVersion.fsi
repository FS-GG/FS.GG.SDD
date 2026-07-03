namespace FS.GG.SDD.Artifacts

module SchemaVersion =
    type SchemaVersion =
        { Major: int
          Minor: int option
          Raw: string }

    type SourceDigest = { Algorithm: string; Value: string }
    type OutputDigest = { Algorithm: string; Value: string }
    type GeneratorVersion = { Id: string; Version: string }

    type SchemaCompatibilityStatus =
        | Current
        | Deprecated
        | Unsupported
        | Malformed
        | Future

    type SchemaCompatibility =
        { RawValue: string
          Version: SchemaVersion option
          Status: SchemaCompatibilityStatus
          SupportedRange: string
          MigrationHint: string option }

    val create: major: int -> SchemaVersion
    val parse: value: string -> Result<SchemaVersion, string>
    val isSupported: version: SchemaVersion -> bool
    val statusValue: status: SchemaCompatibilityStatus -> string
    val classifyRaw: value: string option -> SchemaCompatibility
    val isCurrent: compatibility: SchemaCompatibility -> bool
    val isDeprecated: compatibility: SchemaCompatibility -> bool
    val isBlocking: compatibility: SchemaCompatibility -> bool
    val createSourceDigest: algorithm: string -> value: string -> Result<SourceDigest, string>
    val createOutputDigest: algorithm: string -> value: string -> Result<OutputDigest, string>
    val sha256Text: text: string -> SourceDigest
    val outputSha256Text: text: string -> OutputDigest
    val createGeneratorVersion: id: string -> version: string -> Result<GeneratorVersion, string>
    val currentGeneratorVersion: unit -> GeneratorVersion
