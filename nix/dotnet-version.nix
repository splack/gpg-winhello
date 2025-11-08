{ lib, runCommand, xq-xml, dotnetCorePackages }:

let
  # Extract configuration from .csproj
  csprojJson = lib.importJSON (runCommand "parse-csproj"
    {
      nativeBuildInputs = [ xq-xml ];
    } ''
    xq -j --compact ${../GpgWinHello.csproj} > $out
  '');

  # Extract properties from PropertyGroup
  props = csprojJson.Project.PropertyGroup;

  # Parse .NET version from TargetFramework (e.g., "net9.0-windows..." -> "9.0" -> "sdk_9_0")
  tfm = props.TargetFramework;
  versionPart = lib.removePrefix "net" tfm;
  version = lib.head (lib.splitString "-" versionPart);
  sdkName = "sdk_" + (lib.replaceStrings [ "." ] [ "_" ] version);
in
{
  inherit props;
  dotnetSdk = lib.getAttr sdkName dotnetCorePackages;
}
