{ lib
, buildDotnetModule
, dotnetCorePackages
, runCommand
, xq-xml
}:

let
  dotnetVersion = import ../dotnet-version.nix {
    inherit lib runCommand xq-xml dotnetCorePackages;
  };
  inherit (dotnetVersion) props dotnetSdk;

  # Filter source to include only files needed for build
  src = lib.cleanSourceWith {
    src = ../..;
    filter = path: type:
      let
        baseName = baseNameOf path;
        relPath = lib.removePrefix (toString ../..) (toString path);
      in
      # Include specific files and directories needed for build
      (
        # C# source files
        lib.hasSuffix ".cs" baseName
        # Project file
        || lib.hasSuffix ".csproj" baseName
        # Documentation
        || baseName == "LICENSE"
        || baseName == "README.md"
        || baseName == "CHANGELOG.md"
        # Nix build files
        || baseName == "flake.nix"
        || baseName == "flake.lock"
        || baseName == ".envrc"
        || baseName == ".gitignore"
        # Nix directory
        || lib.hasPrefix "/nix" relPath
        # PowerShell build script (optional, for reference)
        || baseName == "build.ps1"
      );
  };
in
buildDotnetModule {
  pname = "gpg-winhello";
  version = "0.2.0";

  src = src;
  projectFile = "GpgWinHello.csproj";
  nugetDeps = ./deps.json;

  dotnet-sdk = dotnetSdk;
  dotnet-runtime = dotnetSdk;

  # Use configuration from .csproj
  selfContainedBuild = props.SelfContained == "true";
  runtimeId = props.RuntimeIdentifier;

  # Pass Windows targeting and AssemblyName to all dotnet commands
  dotnetFlags = [
    "-p:EnableWindowsTargeting=true"
    "-p:AssemblyName=gpg-winhello"
  ];
  dotnetBuildFlags = lib.optional (props.PublishSingleFile == "true") "-p:PublishSingleFile=true";

  # Don't try to create wrappers for Windows exe
  executables = [ ];

  # Skip dotnet fixup since we're building a Windows exe
  dontDotnetFixup = true;

  meta = {
    description = "Use Windows Hello fingerprint authentication to unlock GPG SSH agent";
    license = lib.licenses.mit;
    platforms = [ "x86_64-linux" ];
  };
}
