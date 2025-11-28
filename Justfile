codegen:
    #!/usr/bin/env bash
    set -euo pipefail
    
    cd Homer.NetDaemon

    secrets_raw=$(dotnet user-secrets list | grep "HomeAssistant:Token")
    secret=$(echo "$secrets_raw" | cut -d'=' -f2 | xargs)

    echo "Using secret: $secret"
    
    dotnet tool run nd-codegen -o "Entities/Entities.cs" -ns "Homer.NetDaemon.Entities" -token "$secret" -host "homeassistant.qinguan.me" -port "443" -ssl true

    # Only runs if the previous command succeeded (due to set -e)
    rm -rf NetDaemonCodegen
