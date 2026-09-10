{
  description = "GPG Windows Hello - Use Windows Hello fingerprint authentication to unlock GPG SSH agent";

  # Inputs go through the lanvise mirrors rather than github.com directly, so
  # evaluation does not depend on upstream availability. See the lanvise repo
  # (clanServices/mirror-sync/mirror-urls.txt, `mirror-manage add <url>`).
  inputs = {
    nixpkgs.url = "git+https://git.lanvise.com/lanvise/mirrors/github.com/NixOS/nixpkgs?ref=nixos-unstable";

    flake-parts = {
      url = "git+https://git.lanvise.com/lanvise/mirrors/github.com/hercules-ci/flake-parts";
      inputs.nixpkgs-lib.follows = "nixpkgs";
    };
  };

  outputs =
    inputs:
    inputs.flake-parts.lib.mkFlake { inherit inputs; } {
      systems = [ "x86_64-linux" ];

      perSystem =
        { pkgs, lib, self', ... }:
        let
          # Shared .NET SDK version, extracted from GpgWinHello.csproj so the
          # flake and the project cannot drift apart.
          dotnetVersion = import ./nix/dotnet-version.nix {
            inherit (pkgs) lib runCommand xq-xml dotnetCorePackages;
          };
        in
        {
          # The Windows executable, cross-compiled from Linux.
          packages.default = pkgs.callPackage ./nix/packages/_default.nix { };

          # `nix fmt` - CSharpier rather than `dotnet format`, which needs a
          # restored project and does not play well under Nix.
          formatter = pkgs.writeShellScriptBin "fmt" ''
            exec ${lib.getExe pkgs.csharpier} format "$@"
          '';

          # `nix run .#update-deps` - regenerate the NuGet lockfile.
          apps.update-deps = {
            type = "app";
            program = lib.getExe (
              pkgs.writeShellScriptBin "update-deps" ''
                set -euo pipefail
                echo "Fetching NuGet dependencies..."
                echo "Updating nix/packages/deps.json..."
                ${self'.packages.default.fetch-deps} ./nix/packages/deps.json
                echo "nix/packages/deps.json updated successfully"
              ''
            );
          };

          devShells.default = pkgs.mkShell {
            packages = [
              # C# language server for IDE support
              pkgs.csharp-ls
              pkgs.csharpier
              pkgs.direnv
              dotnetVersion.dotnetSdk
            ];

            shellHook = ''
              echo "GPG Windows Hello development environment"
              echo ""
              echo "Build commands:"
              echo "  nix build                   # Cross-compile Windows exe from Linux"
              echo "  nix log .#default           # View previous build log"
              echo "  dotnet build                # Local build (if on Windows)"
              echo ""
              echo "Dependency management:"
              echo "  nix run .#update-deps       # Update deps.json with latest NuGet dependencies"
              echo ""
            '';
          };
        };
    };
}
