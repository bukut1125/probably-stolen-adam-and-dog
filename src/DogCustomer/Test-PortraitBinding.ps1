$ErrorActionPreference = 'Stop'
$source = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'PortraitBinding.cs'), [Text.UTF8Encoding]::new($false, $true))
$tests = @'
namespace DogCustomer {
    public static class PortraitBindingTests {
        static void Check(bool condition, string detail) { if (!condition) throw new System.Exception(detail); }
        public static string Run() {
            const string dog = "personal.dog-customer.portrait";
            var original = new object();
            var portrait = new object();
            var other = new object();
            var entries = new System.Collections.Generic.Dictionary<string, object>();
            entries.Add("original.customer", original);
            Check(PortraitBinding.TryBind(entries, dog, portrait), "initial binding");
            Check(object.ReferenceEquals(entries[dog], portrait), "dog identity");
            Check(object.ReferenceEquals(entries["original.customer"], original), "original preserved");
            Check(PortraitBinding.TryBind(entries, dog, portrait), "idempotent binding");
            var recreated = new System.Collections.Generic.Dictionary<string, object>();
            recreated.Add("original.customer", original);
            Check(PortraitBinding.TryBind(recreated, dog, portrait), "dictionary recreation");
            Check(object.ReferenceEquals(recreated[dog], portrait), "recreated identity");
            recreated[dog] = other;
            Check(!PortraitBinding.TryBind(recreated, dog, portrait), "collision rejected");
            Check(object.ReferenceEquals(recreated[dog], other), "collision preserved");
            Check(!PortraitBinding.TryBind<object>(recreated, dog, null), "missing portrait rejected");
            Check(object.ReferenceEquals(recreated["original.customer"], original), "original preserved after failures");
            return "PASS: initial binding, rebind, dictionary recreation, collision, missing image and original identity";
        }
    }
}
'@
Add-Type -TypeDefinition ($source + "`n" + $tests)
[DogCustomer.PortraitBindingTests]::Run()

$managed = 'C:\Program Files (x86)\Steam\steamapps\common\Probably Stolen\Probably Stolen_Data\Managed'
Add-Type -Path (Join-Path $managed 'Unity.Burst.Cecil.dll')
$dll = Join-Path $PSScriptRoot 'bin\Release\netstandard2.1\DogCustomer.dll'
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($dll)
try {
    $patch = $assembly.MainModule.Types | Where-Object FullName -eq 'DogCustomer.DogPortraitPatch'
    if ($null -eq $patch -or $patch.CustomAttributes.AttributeType.FullName -notcontains 'HarmonyLib.HarmonyPatch') { throw 'Portrait patch is missing.' }
    $prefix = $patch.Methods | Where-Object Name -eq 'Prefix'
    $il = ($prefix.Body.Instructions | ForEach-Object { $_.ToString() }) -join "`n"
    if ($il -notmatch 'EnsurePortrait' -or $il -notmatch 'personal.dog-customer.portrait' -or $il -notmatch 'ldnull') { throw 'Portrait lookup guard is incomplete.' }
    Write-Output 'PASS: compiled portrait lookup contains rebinding and missing-portrait guard'
} finally { $assembly.Dispose() }
Write-Output 'These are isolated code checks; in-game appearance and restart behavior remain unverified.'
