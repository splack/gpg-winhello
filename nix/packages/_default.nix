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
in
buildDotnetModule {
  pname = "gpg-winhello";
  version = "0.1.1";

  src = lib.cleanSource ../..;
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
