$ErrorActionPreference = 'Stop'
$managed = 'C:\Program Files (x86)\Steam\steamapps\common\Probably Stolen\Probably Stolen_Data\Managed'
Add-Type -Path (Join-Path $managed 'Unity.Burst.Cecil.dll')
$a=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $managed 'Assembly-CSharp.dll'))
function Method($type,$name) {
    $t=$a.MainModule.Types | Where-Object Name -eq $type
    $m=@($t.Methods | Where-Object Name -eq $name)
    if($m.Count -ne 1 -or -not $m[0].HasBody){throw "Unexpected method $type.$name"}; return $m[0]
}
function Has($m,$pattern){return @($m.Body.Instructions | Where-Object {$_.ToString() -match $pattern}).Count -gt 0}
$target=Method 'GameItem' 'Target'
if(-not (Has $target 'get_onThisTargetItemFunc')){throw 'Item.Target no longer invokes source target action'}
$init=Method 'WaterHelper' 'InitLiquidContainerItem'
if(-not (Has $init 'InitLiquidContainerItem>b__9') -or -not (Has $init 'set_onThisTargetItemFunc')){throw 'Water action is no longer wired to source target'}
$water=$a.MainModule.Types | Where-Object Name -eq 'WaterHelper'
$closure=$water.NestedTypes | Where-Object Name -eq '<>c__DisplayClass10_0'
$fill=$closure.Methods | Where-Object Name -eq '<InitLiquidContainerItem>b__9'
if(-not $fill -or -not (Has $fill 'WaterHelper::IsFaucet') -or -not (Has $fill 'GUTTERFLOW_TAP') -or -not (Has $fill 'RUSTWATER_TAP') -or -not (Has $fill 'WaterHelper::AddWater')){throw 'Native faucet branch changed'}
$transfer=Method 'WaterHelper' 'TransferLiquid'
if(-not (Has $transfer 'Liquid::Liquids') -or -not (Has $transfer 'WaterHelper::HandleFilter') -or -not (Has $transfer 'GameItem::ModifyTag')){throw 'Native transfer chemistry changed'}
$color=Method 'WaterHelper' 'GetLiquidContainerColor'
if(-not (Has $color 'Liquid::color')){throw 'Native liquid coloring changed'}
$start=Method 'PlayerStore' 'LoadGame'
if(-not (Has $start 'FireOnGameLoadedLate')){throw 'Load event changed'}
$liquid=$a.MainModule.Types | Where-Object Name -eq 'Liquid'
if(-not @($liquid.Fields | Where-Object Name -eq 'LIQUID_ZYANIDE')){throw 'Native toxin unavailable'}
$news=Method 'NewsUIManager' 'PopulateUI'
if(-not (Has $news 'StoreEventManager::GetActiveEvents') -or -not (Has $news 'SelectLayout') -or -not (Has $news 'NewsUIElement::PopulateUI')){throw 'Native newspaper population changed'}
$events=Method 'StoreEventManager' 'GetActiveEvents'
if(-not (Has $events 'List`1<StoreEvent>::.ctor')){throw 'Active events no longer returns a copied list'}
$ui=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $managed 'UnityEngine.UI.dll'))
$stencil=$ui.MainModule.Types | Where-Object FullName -eq 'UnityEngine.UI.StencilMaterial'
$add=$stencil.Methods | Where-Object {$_.Name -eq 'Add' -and $_.Parameters.Count -eq 7}
if(-not $add -or -not (Has $add 'UNITY_UI_ALPHACLIP')){throw 'UI mask alpha clipping contract changed'}
$ui.Dispose()
$a.Dispose()
Write-Output 'PASS: native source-target, faucet upgrade branch, chemistry, coloring, newspaper, mask and load contracts in installed build.'
