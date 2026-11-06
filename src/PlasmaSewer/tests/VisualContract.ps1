param([Parameter(Mandatory=$true)][string]$FaucetSprite)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$source=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '..\PipeFxLayout.cs'),[Text.UTF8Encoding]::new($false,$true))
function Number($name) {
    $m=[regex]::Match($source,"\b$name\s*=\s*(\d+)f\s*/\s*(\d+)f")
    if(-not $m.Success){throw "Missing anchor $name"}
    return [double]$m.Groups[1].Value / [double]$m.Groups[2].Value
}
$minX=Number 'MinX';$maxX=Number 'MaxX';$minY=Number 'MinY';$maxY=Number 'MaxY'
$im=[Drawing.Bitmap]::new((Resolve-Path -LiteralPath $FaucetSprite).Path)
try {
    if($im.Width -ne 160 -or $im.Height -ne 80){throw 'Installed faucet sprite dimensions changed'}
    $found=0;$inside=0
    for($y=0;$y -lt 40;$y++) {for($x=0;$x -lt $im.Width;$x++) {
        $c=$im.GetPixel($x,$y)
        if($c.A -gt 200 -and $c.G -gt $c.R*1.15 -and $c.B -gt $c.R*1.05 -and $c.G -gt 45) {
            $found++
            if($x -ge $minX*$im.Width -and $x -lt $maxX*$im.Width -and $y -ge (1-$maxY)*$im.Height -and $y -lt (1-$minY)*$im.Height){$inside++}
        }
    }}
    if($found -lt 100 -or ($inside/$found) -lt .98){throw "Overlay misses glass pixels: $inside of $found"}
    $policyAssembly=Join-Path $PSScriptRoot 'bin\newspaper-qa\PlasmaTests.dll'
    if(-not (Test-Path -LiteralPath $policyAssembly)){throw 'Build the newspaper-qa test suite before the visual contract.'}
    $assembly=[Reflection.Assembly]::LoadFrom($policyAssembly)
    $policy=$assembly.GetType('PlasmaSewer.PipeMaskPolicy',$true).GetMethod('IsWater',[Reflection.BindingFlags]'Static,NonPublic')
    $water=0;$metal=0
    for($y=0;$y -lt $im.Height;$y++) {for($x=0;$x -lt $im.Width;$x++) {
        $c=$im.GetPixel($x,$y)
        $selected=[bool]$policy.Invoke($null,@($x,$y,$c.R,$c.G,$c.B,$c.A))
        if($selected) {
            $water++
            if(($y -ge 24 -and $y -le 33 -and (($x -ge 47 -and $x -le 56) -or ($x -ge 84 -and $x -le 93))) -or $y -ge 34 -or $y -lt 14 -or $x -lt 27 -or $x -ge 120){$metal++}
        }
    }}
    if($water -lt 1200 -or $metal -ne 0){throw "Mask classification invalid: water=$water metal=$metal"}
    Write-Output "PASS: real faucet sprite coverage $inside/$found; $water masked water pixels, $metal cap/brace/highlight pixels."
} finally {$im.Dispose()}
