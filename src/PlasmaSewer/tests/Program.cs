using System.Reflection;
using PlasmaSewer;
static void Check(bool condition,string label) { if(!condition) throw new Exception(label); }
static GameItem Container(int water,int contaminant=0) { var i=new GameItem{identifier="bottle"};i.flags.Add(WaterHelper.LIQUID_CONTAINER_TAG);i.ModifyTag("WATER",t=>t.SetInt(water),false);if(contaminant>0){i.ModifyTag("ZYANIDE",t=>t.SetInt(contaminant),false);i.EnableTag(PlasmaSewerMod.PlasmaTag,false);}return i; }
static void SetWater(GameItem item,int n)=>item.ModifyTag("WATER",t=>t.SetInt(n),false);
static int Part(GameItem item,string key)=>item.GetTagReadonly(key).GetInt();
static (MethodInfo prefix,MethodInfo postfix) Patch() { var p=typeof(PlasmaSewerMod).Assembly.GetType("PlasmaSewer.FaucetFillPatch");return(p.GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic),p.GetMethod("Postfix",BindingFlags.Static|BindingFlags.NonPublic)); }
static object Begin(MethodInfo prefix,GameItem item,GameItem target){object[] args={item,target,null};prefix.Invoke(null,args);return args[2];}
static void Finish(MethodInfo postfix,GameItem item,object state)=>postfix.Invoke(null,new[]{(object)item,state});

Check(PipeFxLayout.SupportsTexture(1774,887) && !PipeFxLayout.SupportsTexture(160,80),"effect crop only accepts its intended image");
Check(PipeFxLayout.CropX>=0 && PipeFxLayout.CropY>=0 && PipeFxLayout.CropX+PipeFxLayout.CropWidth<=PipeFxLayout.TextureWidth && PipeFxLayout.CropY+PipeFxLayout.CropHeight<=PipeFxLayout.TextureHeight,"effect crop fits source image");
Check(PipeFxLayout.MinX==27f/160f && PipeFxLayout.MaxX==120f/160f && PipeFxLayout.MinY==44f/80f && PipeFxLayout.MaxY==75f/80f,"overlay anchors stay inside faucet water chamber");
int eventCount=0,lastEnd=0;
for(int d=1;d<=3000;d++){
    var w=EventPolicy.ForDay("save-A",d);
    Check(w.Start==EventPolicy.ForDay("save-A",d).Start && w.EndExclusive==EventPolicy.ForDay("save-A",d).EndExclusive,"deterministic reload");
    if(w.Active(d)) {Check(w.EndExclusive-w.Start>=2 && w.EndExclusive-w.Start<=4,"duration 2 to 4");if(w.Start!=lastEnd){eventCount++;lastEnd=w.Start;}}
}
Check(eventCount>50 && eventCount<250,"event frequency within broad sanity bounds");
Check(Enumerable.Range(1,200).Any(d=>EventPolicy.IsActive("save-A",d)!=EventPolicy.IsActive("save-B",d)),"different runs have different schedules");
Check(EventPolicy.PlannedContamination(1000,1000,1000,1000)==0,"full container unchanged");
Check(EventPolicy.PlannedContamination(100,1100,100,1100)==100,"dose uses newly added water");
Check(EventPolicy.PlannedContamination(100,1100,900,902)==2,"dose never consumes prior water");
Check(EventPolicy.PlannedContamination(100,1100,900,899)==0,"no new water no contamination");
Liquid.Liquids.Add(new Liquid{identifier="water",currentPart="WATER"});Liquid.Liquids.Add(new Liquid{identifier="zyanide",currentPart="ZYANIDE",lethality=1.66});
Liquid.LiquidDict.Add("water",Liquid.Liquids[0]);Liquid.LiquidDict.Add("zyanide",Liquid.Liquids[1]);
var mod=new PlasmaSewerMod(); mod.OnEnable(); ModHook.LoadInit(); ModHook.LoadInit();
Check(Liquid.Liquids.Count==2 && Liquid.LiquidDict["zyanide"].lethality>0,"uses existing toxic liquid without adding a new liquid type");
PlayerStore.Instance.runID="save-A";StoreStation.Day=Enumerable.Range(1,1000).First(d=>EventPolicy.IsActive("save-A",d));NetworkUpgrade.Gutter=true;NetworkUpgrade.Rust=false;
var (prefix,postfix)=Patch(); var faucet=new GameItem{identifier="faucet"};var item=Container(500);var state=Begin(prefix,item,faucet);SetWater(item,1500);Finish(postfix,item,state);
Check(Part(item,"WATER")==1400 && Part(item,"ZYANIDE")==100 && WaterHelper.GetTotalVolume(item)==1500,"faucet adds toxin while conserving total and old contents");
var tip=new RichTextBuilder();ModHook.Tooltip(tip,item);Check(tip.Lines.Any(x=>x.Contains("劇毒")),"contaminated container has warning");
var full=Container(1000);state=Begin(prefix,full,faucet);Finish(postfix,full,state);Check(Part(full,"WATER")==1000 && Part(full,"ZYANIDE")==0,"full container unchanged");
var other=Container(100);state=Begin(prefix,other,new GameItem{identifier="other"});SetWater(other,1100);Finish(postfix,other,state);Check(Part(other,"ZYANIDE")==0,"non-faucet water untouched");
NetworkUpgrade.Rust=true;var rust=Container(0);state=Begin(prefix,rust,faucet);SetWater(rust,1000);Finish(postfix,rust,state);Check(Part(rust,"ZYANIDE")==100,"upgraded rust tap can experience the same leak");
NetworkUpgrade.Gutter=false;var rustOnly=Container(0);state=Begin(prefix,rustOnly,faucet);SetWater(rustOnly,1000);Finish(postfix,rustOnly,state);Check(Part(rustOnly,"ZYANIDE")==100,"rust upgrade remains supported without assuming gutter flag");
NetworkUpgrade.Rust=false;NetworkUpgrade.Gutter=false;var locked=Container(0);state=Begin(prefix,locked,faucet);SetWater(locked,1000);Finish(postfix,locked,state);Check(Part(locked,"ZYANIDE")==0,"locked tap untouched");
NetworkUpgrade.Gutter=true;WaterHelper.Filter=new GameItem();var filtered=Container(200);state=Begin(prefix,filtered,faucet);SetWater(filtered,1200);Finish(postfix,filtered,state);Check(Part(filtered,"WATER")==1100 && Part(filtered,"ZYANIDE")==50 && WaterHelper.GetTotalVolume(filtered)==1150,"filter removes part of incoming contaminant without replacing existing water");WaterHelper.Filter=null;
StoreStation.Day=Enumerable.Range(1,1000).First(d=>!EventPolicy.IsActive("save-A",d));var normal=Container(0);state=Begin(prefix,normal,faucet);SetWater(normal,1000);Finish(postfix,normal,state);Check(Part(normal,"ZYANIDE")==0,"inactive event leaves ordinary water unchanged");
Check(Part(item,"ZYANIDE")==100,"previously collected contaminant remains after event");
ModHelper.Config.Entry.Value=true;ModHook.ConfigChanged(PlasmaSewerMod.ModId);Check(PlasmaSewer.PlasmaVisual.Visible,"preview shows visual on inactive day");
var previewWater=Container(0);state=Begin(prefix,previewWater,faucet);SetWater(previewWater,1000);Finish(postfix,previewWater,state);
Check(Part(previewWater,"ZYANIDE")==0,"visual-only preview does not contaminate water");
ModHelper.Config.Entry.Value=false;ModHook.ConfigChanged(PlasmaSewerMod.ModId);Check(!PlasmaSewer.PlasmaVisual.Visible,"preview can be turned off");
var savedPlayer=PlayerStore.Instance;PlayerStore.Instance=null;PlasmaSewerMod.RefreshVisual();PlayerStore.Instance=savedPlayer;
StoreStation.Day=Enumerable.Range(1,1000).First(d=>EventPolicy.IsActive("save-A",d));
var liter=Container(0);state=Begin(prefix,liter,faucet);SetWater(liter,1000000);Finish(postfix,liter,state);
Check(Part(liter,"ZYANIDE")*Liquid.LiquidDict["zyanide"].lethality>=750,"one-liter spill exceeds native toxic threshold");
var transfer=typeof(PlasmaSewerMod).Assembly.GetType("PlasmaSewer.PlasmaTransferPatch");
var transferPre=transfer.GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic);var transferPost=transfer.GetMethod("Postfix",BindingFlags.Static|BindingFlags.NonPublic);
var noTransfer=Container(100);object[] none={item,noTransfer,null};transferPre.Invoke(null,none);transferPost.Invoke(null,new[]{(object)item,noTransfer,none[2]});Check(!noTransfer.IsTag(PlasmaSewerMod.PlasmaTag),"zero transfer does not mark target");
var stale=Container(100);stale.EnableTag(PlasmaSewerMod.PlasmaTag,false);none=new object[]{new GameItem(),stale,null};transferPre.Invoke(null,none);transferPost.Invoke(null,new[]{none[0],none[1],none[2]});Check(!stale.IsTag(PlasmaSewerMod.PlasmaTag),"empty stale destination marker is cleared");
var target=Container(200);object[] ta={item,target,null};transferPre.Invoke(null,ta);item.ModifyTag("ZYANIDE",t=>t.SetInt(50),false);target.ModifyTag("ZYANIDE",t=>t.SetInt(50),false);transferPost.Invoke(null,new[]{(object)item,target,ta[2]});
Check(target.IsTag(PlasmaSewerMod.PlasmaTag) && Part(target,"ZYANIDE")==50,"plasma appearance follows actual transferred toxin");
var colorPatch=typeof(PlasmaSewerMod).Assembly.GetType("PlasmaSewer.PlasmaColorPatch").GetMethod("Postfix",BindingFlags.Static|BindingFlags.NonPublic);object[] colorArgs={target,"000000"};colorPatch.Invoke(null,colorArgs);Check((string)colorArgs[1]=="38DDE4","plasma uses cyan container color");
item.ModifyTag("ZYANIDE",t=>t.SetInt(0),false);ta=new object[]{item,target,null};transferPre.Invoke(null,ta);transferPost.Invoke(null,new[]{(object)item,target,ta[2]});Check(!item.IsTag(PlasmaSewerMod.PlasmaTag),"empty source clears stale plasma marker");
mod.OnDisable();Check(Part(target,"ZYANIDE")==50 && Liquid.LiquidDict.ContainsKey("zyanide"),"native toxicity and volume remain recognized when mod is disabled");
Console.WriteLine("PASS: deterministic event timing, scoped faucet behavior, mixture conservation, filter, warning, and disabled-state simulation. Real game and visuals require in-game validation.");
