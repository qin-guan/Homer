$secrets_raw = dotnet user-secrets list | Select-String -Pattern "HomeAssistant:Token"
$secret = ($secrets_raw -split " = ")[1]

Write-Output "Using secret: $secret"

dotnet tool run nd-codegen -o "Entities/Entities.cs" -ns "Homer.NetDaemon.Entities" -token $secret

Remove-Item -Recurse NetDaemonCodeGen