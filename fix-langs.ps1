param(
    [Parameter(Mandatory = $true)][string]$BlpPath,
    [Parameter(Mandatory = $true)][string]$OverrideDir
)

if (-not (Test-Path $BlpPath)) {
    Write-Host "[OverrideBetterLyricsLangs] .blp not found at '$BlpPath', skipping."
    exit 0
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$zip = [System.IO.Compression.ZipFile]::Open($BlpPath, [System.IO.Compression.ZipArchiveMode]::Update)
try {
    Get-ChildItem -Path $OverrideDir -Filter "*.json" | ForEach-Object {
        $entryName = "Langs/$($_.Name)"
        $existing = $zip.GetEntry($entryName)
        if ($existing) { $existing.Delete() }

        $entry = $zip.CreateEntry($entryName)
        $stream = $entry.Open()
        $bytes = [System.IO.File]::ReadAllBytes($_.FullName)
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Close()

        Write-Host "[OverrideBetterLyricsLangs] Replaced $entryName"
    }
}
finally {
    $zip.Dispose()
}