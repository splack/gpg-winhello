{
  description = "GPG Windows Hello - Use Windows Hello fingerprint authentication to unlock GPG SSH agent";

  inputs.flakelight.url = "github:nix-community/flakelight";

  outputs = { flakelight, self, ... }:
    flakelight ./. ({ lib, ... }: {
      license = "MIT";

      apps.update-deps =
        pkgs:
        let
          inherit (pkgs) system;
        in
        {
          type = "app";
          program = "${pkgs.writeShellScript "update-deps" ''
            set -euo pipefail
            echo "Fetching NuGet dependencies..."
            echo "Updating nix/packages/deps.json..."
            ${self.packages.${system}.default.fetch-deps} ./nix/packages/deps.json
            echo "✓ nix/packages/deps.json updated successfully"
          ''}";
        };

      devShell = {
        packages = pkgs:
          let
            dotnetVersion = import ./nix/dotnet-version.nix {
              inherit (pkgs) lib runCommand xq-xml dotnetCorePackages;
            };
          in
          builtins.attrValues
            {
              inherit (pkgs)
                # C# Language Server for IDE support
                csharp-ls

                # Development tools
                direnv
                ;
            } ++ [ dotnetVersion.dotnetSdk ];

        shellHook = ''
          echo "🔐 GPG Windows Hello development environment"
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
      # Configure formatters for different file types
      formatters = pkgs: {
        "*.cs" = "${pkgs.writeShellScript "format-csharp" ''
          exec ${lib.getExe pkgs.csharpier} format "$@"
        ''}";
      };
    });
}
