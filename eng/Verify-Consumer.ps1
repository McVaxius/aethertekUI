$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Enter-RepoEnv.ps1')
Push-Location $repoRoot
try {
    & dotnet build samples/ConsumerContract/ConsumerContract.csproj -c Debug --no-restore --disable-build-servers -m:1 -v:minimal *> artifacts/consumer-project.log
    if ($LASTEXITCODE -ne 0) { Get-Content artifacts/consumer-project.log; throw 'Independent project-reference consumer failed' }
    $output = Join-Path $repoRoot 'samples/ConsumerContract/bin/Debug/net10.0-windows'
    foreach ($name in @('ConsumerContract','AethertekUI','AethertekUI.Dalamud')) {
        $path = Join-Path $output "$name.dll"
        if ([Reflection.AssemblyName]::GetAssemblyName($path).Name -ne $name) { throw "Consumer assembly identity differs: $name" }
    }
    $dependencies = Get-Content (Join-Path $output 'ConsumerContract.deps.json') -Raw | ConvertFrom-Json
    foreach ($name in @('AethertekUI','AethertekUI.Dalamud')) {
        if (-not $dependencies.libraries.PSObject.Properties["$name/0.3.2"]) { throw "Consumer dependency missing: $name/0.3.2" }
    }
    foreach ($name in @('Dalamud.dll','Dalamud.Bindings.ImGui.dll','cimgui.dll')) {
        if (Test-Path -LiteralPath (Join-Path $output $name)) { throw "Host-owned assembly copied into consumer: $name" }
    }
    Get-Content artifacts/consumer-project.log -Tail 6
    Write-Output 'Verified independent consumer, AethertekUI assembly identities/dependencies and host-owned assembly boundaries.'
}
finally { Pop-Location }
