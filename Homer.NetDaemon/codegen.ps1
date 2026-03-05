$ErrorActionPreference = "Stop"

Push-Location Homer.NetDaemon
try {
    $secretsRaw = dotnet user-secrets list | Select-String "HomeAssistant:Token"
    $secret = ($secretsRaw -split '=', 2)[1].Trim()

    Write-Host "Using secret: $secret"

    dotnet tool run nd-codegen -o "Entities/Entities.cs" -ns "Homer.NetDaemon.Entities" -token "$secret" -host "homeassistant.qinguan.me" -port "443" -ssl true
    if ($LASTEXITCODE -ne 0) { throw "nd-codegen failed with exit code $LASTEXITCODE" }

    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue NetDaemonCodegen
}
finally {
    Pop-Location
}
