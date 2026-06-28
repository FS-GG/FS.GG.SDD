namespace Fsgg

module Provider =

    type DeclaredCommand =
        { Executable: string
          Arguments: string list }

    type ProviderParameterSpec =
        { Key: string
          Required: bool
          Default: string option }

    type ProviderDescriptor =
        { Name: string
          ContractVersion: string
          TemplateId: string
          Source: string
          Parameters: ProviderParameterSpec list
          Build: DeclaredCommand option
          Test: DeclaredCommand option
          Run: DeclaredCommand option
          Verify: DeclaredCommand option
          NameParameter: string }

    let defaultNameParameter = "name"

    let resolveNameParameter (descriptor: ProviderDescriptor) =
        if System.String.IsNullOrWhiteSpace descriptor.NameParameter then
            defaultNameParameter
        else
            descriptor.NameParameter

    let isMalformed (command: DeclaredCommand) =
        System.String.IsNullOrWhiteSpace command.Executable
