using System.Reflection;
using AlienStory;

static void Check(bool v, string s) { if (!v) throw new Exception(s); }
static Dialogue Last(Dialogue d) { while (d.nextDialogue != null) d = d.nextDialogue; return d; }
static void Finish(Dialogue d) { for (; d != null; d = d.nextDialogue) d.endAction?.Invoke(); }
static void Select(Dialogue d, int n) { var c = Last(d).choices[n]; c.action?.Invoke(); Finish(c.next); }
static void Current(StoreClient c) => PlayerStore.Instance.currentClientInstance = new StoreClientInstance { client = c };
static StoreClient Queue(int day, string id) {
    StoreStation.Day = day; var m = new StoreClientManager(); ModHook.Queue(m);
    return m.clientStack.SingleOrDefault(c => c.identifier == id);
}
static bool Barter(StoreClient c, params GameItem[] items) {
    Current(c); EmporiumEntry.Instance.backInvinvElement.childItems.Clear();
    EmporiumEntry.Instance.backInvinvElement.childItems.AddRange(items);
    var patch = typeof(StoryState).Assembly.GetType("AlienStory.StoryBarterPatch");
    var prefix = patch.GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic);
    var postfix = patch.GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic);
    object[] args = { PlayerStore.Instance, -1 };
    bool proceed = (bool)prefix.Invoke(null, args);
    if (!proceed) return false;
    PlayerStore.Instance.AcceptBarter();
    postfix.Invoke(null, new[] { args[1] });
    return true;
}
static GameItem Food(int kcal,int count=1) { var x=new GameItem {identifier="meal",unitCount=count,Owned=true}; x.types.Add("FOOD");x.tags[FoodItemHelper.CALORIE_VALUE_TAG]=kcal;x.enabledTags.Add(FoodItemHelper.CALORIE_VALUE_TAG);return x; }
static GameItem Water(){var x=new GameItem{identifier="water",Owned=true};x.types.Add("WATER");return x;}
static GameItem Part(int n)=>new(){identifier="common_electronic",unitCount=n,Owned=true};
static GameItem Battery()=>new(){identifier="energy_credit_breeder",Owned=true};

var sprites = new LoadedMod { IsEnabled=true, Manifest=new ModManifest{ID=PortraitRegistry.ModId} };
foreach (string k in new[]{"adam_normal","adam_hunger1","adam_hunger2","adam_hunger3","adam_scavenger","alien_god","ending_bad","ending_space","ore_device"}) sprites.Sprites[k]=new();
ModLoader.Mods.Add(sprites);
StoreClientListDict.storeClientDict.Add("personal.dog-customer.visitor",()=>new StoreClient{identifier="personal.dog-customer.visitor",spriteName="personal.dog-customer.portrait"});
var mod=new AlienStoryMod(); mod.OnEnable(); ModHook.Items(new ModItemDirectory());
Check(!ModHelper.Config.Entry.Value,"formal release defaults to no preview visits");
ModHelper.Config.Entry.Set(true); // Explicit opt-in for this isolated test only.
StoreStation.Day=1;
var queue=new StoreClientManager(); queue.clientStack.Add(new StoreClient{identifier="original"}); ModHook.Queue(queue);
Check(queue.clientStack.Count==8 && queue.clientStack[0].identifier=="original", "preview adds seven and preserves original");
Check(queue.clientStack.Single(c=>c.identifier=="personal.dog-customer.visitor").clientIntent==StoreClient.ClientIntent.DIALOGUE,"dog preview does not require real trading");
foreach (var expected in new[] {
    (AlienStoryMod.AdamPreviewId,"adam_normal"), (AlienStoryMod.Hunger1PreviewId,"adam_hunger1"),
    (AlienStoryMod.Hunger2PreviewId,"adam_hunger2"), (AlienStoryMod.Hunger3PreviewId,"adam_hunger3"),
    (AlienStoryMod.ScavPreviewId,"adam_scavenger"), (AlienStoryMod.GodPreviewId,"alien_god") })
    Check(queue.clientStack.Single(c=>c.identifier==expected.Item1).spriteName==PortraitRegistry.Id(expected.Item2),"preview portrait "+expected.Item2);
var dogPreview=queue.clientStack.Single(c=>c.identifier=="personal.dog-customer.visitor");
dogPreview.OnArrival?.Invoke();
Check((ModHelper.GetModDataInt(PortraitRegistry.ModId,"preview_arrivals_v2",0)&PreviewPlanner.Dog)==0,"missing dog picture is not counted as viewed");
SpriteDict.Instance.spriteDictionary["personal.dog-customer.portrait"]=new UnityEngine.Sprite();
foreach (var c in queue.clientStack.Skip(1)) c.OnArrival?.Invoke();
Check(ModHelper.GetModDataInt(PortraitRegistry.ModId,"preview_arrivals_v2",0)==127, "all seven arrivals stored");
var previewAdam=queue.clientStack.Single(c=>c.identifier==AlienStoryMod.AdamPreviewId);
Current(previewAdam); Select(previewAdam.mainDialogue,0);
Check(StoryRuntime.Load().stage==Stage.Aid && StoryRuntime.Load().refusals==0,"preview aid dialogue requires no resources and leaves story intact");
var previewGod=queue.clientStack.Single(c=>c.identifier==AlienStoryMod.GodPreviewId);
Current(previewGod);
var godChoice=Last(previewGod.mainDialogue).choices[0];
Check(godChoice.next.plainText==StoryText.Apologize,"god preview presents the apology as the player choice");
godChoice.action?.Invoke(); var restitutionPreview=godChoice.next.nextDialogue;
Check(Last(restitutionPreview).choices[0].next.plainText=="我把食物準備好了。","preview restitution presents natural player dialogue");
Select(restitutionPreview,0); Check(StoryRuntime.Load().stage==Stage.Aid,"preview restitution leaves formal story unchanged");
Check(ModHelper.Config.Entry.Value && !ModHelper.Config.Saved, "preview remains enabled until the run can save");
Check(ModHelper.GetModDataInt(PortraitRegistry.ModId,"preview_decision_due",-1)==4,"quick decision scheduled three days after last preview");
StoreStation.Day=2; ModHook.Wake();
Check(ModHelper.Config.Entry.Value && !ModHelper.Config.Saved, "test mode remains enabled until the third-day decision");
Check(StoryRuntime.Load().stage==Stage.Aid && StoryRuntime.Load().refusals==0, "preview did not advance story");
Check(Queue(3,AlienStoryMod.QuickTestId)==null,"quick decision not early");
var quick=Queue(4,AlienStoryMod.QuickTestId); Check(quick!=null,"quick decision on third day"); Current(quick);
Select(quick.mainDialogue,0);
Check(ModHelper.GetModDataBool(PortraitRegistry.ModId,"preview_decision_done",false) && StoryRuntime.Load().stage==Stage.Aid && StoryEndings.PreviewActive && PlayerStore.Instance.NativeEndCalls==0 && ReferenceEquals(GameOverUIManager.Instance.darkScreen.Image.sprite,sprites.Sprites["ending_space"]),"full-screen test ending does not end the run");
var continuePatch=typeof(StoryState).Assembly.GetType("AlienStory.PreviewContinuePatch");
GameOverUIManager.Instance.ContinuePlayClick(); continuePatch.GetMethod("Postfix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
Check(!StoryEndings.PreviewActive && GameOverUIManager.Instance.darkScreen.Image.sprite==null,"test ending restores native backdrop on return");
StoreStation.Day=5; ModHook.Wake();
Check(!ModHelper.Config.Entry.Value && ModHelper.Config.Saved,"completed test mode disables after the next save");
ModHelper.Data.Clear(); // Simulate a fresh run after the global preview was completed.
StoreStation.Day=1; var freshQueue=new StoreClientManager(); ModHook.Queue(freshQueue);
Check(freshQueue.clientStack.Count==0,"formal release does not enqueue test characters on a new run");
Check(Queue(22,StoryRuntime.AdamId)==null, "no story before day 23");

for(int day=23;day<=26;day++) {
    var visitor=Queue(day,StoryRuntime.AdamId); Check(visitor!=null,"aid visitor "+day); Current(visitor);
    Check(visitor.spriteName==PortraitRegistry.Id(day==23?"adam_normal":"adam_hunger"+(day-23)), "progressive dedicated portrait");
    Select(visitor.mainDialogue,1);
}
Check(StoryRuntime.Load().stage==Stage.Judgment, "four explicit refusals lead to judgment");
Check(Queue(26,StoryRuntime.GodId)==null, "god not same day");
var god=Queue(27,StoryRuntime.GodId); Check(god!=null,"god next day"); Current(god);
Select(god.mainDialogue,0); Check(StoryRuntime.Load().restitutionDeadline==34,"apology seven days");
Check(!Barter(god,Food(99)) && EmporiumEntry.Instance.backInvinvElement.childItems.Count==1,"99 kcal does not consume food");
Check(Barter(god,Food(100)) && StoryRuntime.Load().stage==Stage.Introduction,"100 kcal redeems");
var intro=Queue(28,StoryRuntime.AdamId); Check(intro!=null && intro.displayName=="外星人","unrevealed name"); Current(intro); Finish(intro.mainDialogue);
Check(StoryRuntime.Load().revealed && StoryRuntime.Load().stage==Stage.Recovery,"name reveal recorded");

ModHelper.Data.Clear(); ModHelper.SetModDataInt(PortraitRegistry.ModId,"preview_arrivals_v2",127);
var aid=Queue(23,StoryRuntime.AdamId); Check(aid!=null,"normal aid"); Current(aid);
Check(Last(aid.mainDialogue).choices[0].next.plainText=="我有食物或水，請收下。" && Last(aid.mainDialogue).choices[1].next.plainText=="抱歉，我不會給你。", "choice bubble shows natural player actions");
Check(aid.barterOffer.barterTextBody.Contains("食物") && aid.barterOffer.barterTextBody.Contains("水") && aid.barterOffer.barterTextBody.Contains("或"), "food-or-water requirement displayed");
Check(aid.barterOffer.getBarterResult().response=="", "no generic complaint is spoken");
EmporiumEntry.Instance.backInvinvElement.childItems.Clear(); EmporiumEntry.Instance.backInvinvElement.childItems.Add(Water());
Check(aid.barterOffer.getBarterResult().isAccepted,"water alone is accepted");
var demandPatch=typeof(StoryState).Assembly.GetType("AlienStory.DemandTagPatch");
var uiState=Activator.CreateInstance(demandPatch.GetNestedType("State",BindingFlags.NonPublic));
object[] tagArgs={aid,uiState};
demandPatch.GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,tagArgs);
Check(aid.clientIntent==StoreClient.ClientIntent.BUY && aid.clientBuyingTagList.Contains("FOOD") && aid.clientBuyingTagList.Contains("WATER"),"native demand chips see food and water during UI build");
demandPatch.GetMethod("Postfix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,tagArgs);
Check(aid.clientIntent==StoreClient.ClientIntent.BARTER,"UI tag rendering restores real barter intent");
var unrelated=new StoreClient{identifier="original",clientIntent=StoreClient.ClientIntent.BARTER}; unrelated.clientBuyingTagList.Add("FOOD");
object[] otherArgs={unrelated,Activator.CreateInstance(demandPatch.GetNestedType("State",BindingFlags.NonPublic))};
demandPatch.GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,otherArgs);
Check(unrelated.clientIntent==StoreClient.ClientIntent.BARTER,"native tag patch leaves other customers unchanged");
Select(aid.mainDialogue,0);
Check(!Barter(aid,new GameItem{identifier="junk",Owned=true}) && StoryRuntime.Load().stage==Stage.Aid,"unrelated item does not progress");
Check(Barter(aid,Food(100)) && StoryRuntime.Load().stage==Stage.Introduction,"food alone progresses");
Check(aid.displayName=="外星人", "name not revealed before introduction");
Current(aid); Finish(aid.acceptDealDialogue); Check(StoryRuntime.Load().revealed && aid.displayName=="亞當","aid introduction reveals and immediately renames");
var scav=Queue(25,StoryRuntime.AdamId); Check(scav!=null && scav.spriteName==PortraitRegistry.Id("adam_scavenger"),"scavenger appearance"); Current(scav); Finish(scav.mainDialogue); scav.OnDismiss?.Invoke();
Check(StoryRuntime.Load().partsReadyDay==28,"three day parts wait");
scav=Queue(27,StoryRuntime.AdamId); Current(scav); Finish(scav.mainDialogue); scav.OnDismiss?.Invoke();
scav=Queue(29,StoryRuntime.AdamId); Current(scav); Finish(scav.mainDialogue); scav.OnDismiss?.Invoke();
scav=Queue(31,StoryRuntime.AdamId); Check(scav!=null && scav.clientIntent==StoreClient.ClientIntent.BARTER,"parts request repeats"); Current(scav); Finish(scav.mainDialogue);
Check(!Barter(scav,Part(2)) && StoryRuntime.Load().stage==Stage.Scavenger,"insufficient parts retained");
Check(Barter(scav,Part(3)) && StoryRuntime.Load().stage==Stage.Battery,"parts accepted");
var battery=Queue(33,StoryRuntime.AdamId); Check(battery!=null,"battery visitor"); Current(battery);
Check(Barter(battery,Battery()) && StoryRuntime.Load().stage==Stage.Decision,"battery opens decision");
Current(battery); Select(battery.acceptDealDialogue,1); Check(StoryRuntime.Load().stage==Stage.Stayed,"stay selected");
var device=EmporiumEntry.Instance.invElement.childItems.Single(i=>i.identifier=="personal.adam-story.ore-device");
Check(device.Owned,"reward device belongs to the player");
Check(device.shape.width==4 && device.shape.height==3 && device.shape.fill==1,"reward device occupies 4 by 3 cells");
Check(Math.Abs(sprites.Sprites["ore_device"].Ppu-(100f*100/(4f*16)))<0.01f,"device icon scaled to four inventory cells");
Check(device.IsTag("personal.adam-story.next-day"),"device countdown tag enabled");
Check(device.shortDescription.EndsWith("來自亞當的答謝"),"description ends with Adam's thanks");
var tip=new RichTextBuilder(); ModHook.Tooltip(tip,device);
Check(tip.lines.Any(x=>x.Contains("3 天")),"native tooltip displays countdown");
var saved=DirectoryMaster.Item("personal.adam-story.ore-device",true);
saved.SetShape(new GridShapeBuilder().SetDataFill(2,2,1).Build());
saved.shortDescription="舊的描述";
var temporaryGrid=new GameGridInventory { Fits=true }; saved.parentInventory=temporaryGrid;
saved.onLoaded(saved);
Check(saved.shape.width==2 && saved.shape.height==2 && saved.shortDescription.EndsWith("來自亞當的答謝"),"per-item callback updates description without resizing in temporary container");
var originalGrid=EmporiumEntry.Instance.invElement; var oldGrid=new GameGridInventory(); EmporiumEntry.Instance.invElement=oldGrid; saved.parentInventory=oldGrid; oldGrid.childItems.Add(saved);
var blocker=new GameItem(); blocker.SetShape(new GridShape {width=1,height=1,fill=1,minX=2,minY=1}); oldGrid.childItems.Add(blocker);
ModHook.Loaded();
Check(saved.shape.width==2 && blocker.shape.minX==2,"neighbor collision defers migration without moving either item");
oldGrid.childItems.Remove(blocker); oldGrid.Width=2; ModHook.Loaded();
Check(saved.shape.width==2,"inventory boundary defers migration");
oldGrid.Width=10; saved.modifiedShape.orientation=1; ModHook.Loaded();
Check(saved.shape.width==2,"rotated legacy item remains unchanged");
saved.modifiedShape.orientation=0; oldGrid.ParentsFit=false; ModHook.Loaded();
Check(saved.shape.width==2,"parent inventory constraint defers migration");
oldGrid.ParentsFit=true; ModHook.Loaded();
Check(saved.shape.width==4 && saved.shape.height==3,"old device expands after a valid placement is available");
oldGrid.childItems.Remove(saved); EmporiumEntry.Instance.invElement=originalGrid;
var skipped=new GameItem(); skipped.ModifyTag("charge",x=>x.Enable().SetInt(7),true);
Check(!skipped.IsTag("charge"),"negative control models stale modified state when validation is skipped");
skipped.ModifyTag("charge",x=>x.Enable().SetInt(7),false);
Check(skipped.IsTag("charge") && skipped.GetTagReadonly("charge").GetInt()==7,"validated state is immediately visible");
var hitPatch=typeof(StoryState).Assembly.GetType("AlienStory.OreDeviceHitTargetPatch");
var element=(GameItemElement)device; element.background.image.sprite=sprites.Sprites["ore_device"]; element.background.image.type=UnityEngine.UI.Image.Type.Tiled; element.fakeBackground.image.type=UnityEngine.UI.Image.Type.Tiled;
hitPatch.GetMethod("Postfix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{element});
Check(element.background.image.alphaHitTestMinimumThreshold==0f && element.background.image.rectTransform.sizeDelta.x==64 && element.background.image.rectTransform.sizeDelta.y==48 && element.background.image.type==UnityEngine.UI.Image.Type.Simple && element.fakeBackground.image.type==UnityEngine.UI.Image.Type.Simple,"device visual does not tile and fills the 4 by 3 grid");

StoreStation.Day=35; device.onActivateSlotItemFunc(device,device.parentInventory,null);
Check(!device.parentInventory.childItems.Any(i=>i.identifier=="common_ore"),"ore device not charged early");
Check(AudioManager.Instance.LastSound==null,"no turbo sound when uncharged");
StoreStation.Day=36; device.onActivateSlotItemFunc(device,device.parentInventory,null);
Check(device.parentInventory.childItems.Where(i=>i.identifier=="common_ore").Sum(i=>i.unitCount)==10,"ten vanilla metal ore after three days");
Check(device.parentInventory.childItems.Where(i=>i.identifier=="common_ore").All(i=>i.Owned),"generated ore belongs to the player");
Check(AudioManager.Instance.LastSound=="turbo","successful ore output uses turbo booster sound");
device.onActivateSlotItemFunc(device,device.parentInventory,null);
Check(device.parentInventory.childItems.Where(i=>i.identifier=="common_ore").Sum(i=>i.unitCount)==10,"no duplicate ore same day");
StoreStation.Day=39; device.parentInventory.transferLimit=0; AudioManager.Instance.LastSound=null;
device.onActivateSlotItemFunc(device,device.parentInventory,null);
Check(device.parentInventory.childItems.Where(i=>i.identifier=="common_ore").Sum(i=>i.unitCount)==10 && AudioManager.Instance.LastSound==null,"full inventory retains charge and stays silent");
device.parentInventory.transferLimit=4; device.parentInventory.slotsLeft=4;
device.onActivateSlotItemFunc(device,device.parentInventory,null);
Check(device.parentInventory.childItems.Where(i=>i.identifier=="common_ore").Sum(i=>i.unitCount)==14,"partial payout records remaining units");
device.parentInventory.slotsLeft=4;
device.onActivateSlotItemFunc(device,device.parentInventory,null);
device.parentInventory.slotsLeft=4;
device.onActivateSlotItemFunc(device,device.parentInventory,null);
Check(device.parentInventory.childItems.Where(i=>i.identifier=="common_ore").Sum(i=>i.unitCount)==20,"partial placements sum to exactly one ten-ore charge");
device.onActivateSlotItemFunc(device,device.parentInventory,null);
Check(device.parentInventory.childItems.Where(i=>i.identifier=="common_ore").Sum(i=>i.unitCount)==20,"partial payout cannot be repeated");

ModHelper.Data.Clear(); ModHelper.SetModDataInt(PortraitRegistry.ModId,"preview_arrivals_v2",127);
ModHelper.SetModDataInt(PortraitRegistry.ModId,"stage",(int)Stage.Restitution);
ModHelper.SetModDataInt(PortraitRegistry.ModId,"restitution_deadline",40);
StoreStation.Day=41;
var lateGod=StoryRuntime.MakeGod(); Current(lateGod);
EmporiumEntry.Instance.backInvinvElement.childItems.Clear(); EmporiumEntry.Instance.backInvinvElement.childItems.Add(Food(100));
Check(!lateGod.barterOffer.getBarterResult().isAccepted,"expired food cannot trigger success dialogue");
var offerGuard=typeof(StoryState).Assembly.GetType("AlienStory.OfferGuardPatch");
Check(!(bool)offerGuard.GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null),"expired offer click is stopped before success dialogue");
Check(!Barter(lateGod,Food(100)) && EmporiumEntry.Instance.backInvinvElement.childItems.Count==1,"expired donation cannot consume food");
ModHook.Loaded();
Check(StoryRuntime.Load().stage==Stage.BadEnding && PlayerStore.Instance.NativeEndCalls==1 && PlayerStore.Instance.LastEnding==StoryEndings.BadId && ReferenceEquals(GameOverUIManager.Instance.darkScreen.Image.sprite,sprites.Sprites["ending_bad"]),"expired promise invokes native bad ending with art");
ModHook.Loaded(); Check(PlayerStore.Instance.NativeEndCalls==1,"reopening a recorded ending does not duplicate native run statistics");
ModHelper.Data.Clear(); ModHelper.SetModDataInt(PortraitRegistry.ModId,"preview_arrivals_v2",127);
ModHelper.SetModDataInt(PortraitRegistry.ModId,"stage",(int)Stage.Decision);
ModHelper.SetModDataInt(PortraitRegistry.ModId,"next_visit",23);
StoreStation.Day=42; var departing=Queue(42,StoryRuntime.AdamId); Current(departing); Select(departing.mainDialogue,0);
Check(StoryRuntime.Load().stage==Stage.Departed && PlayerStore.Instance.NativeEndCalls==2 && PlayerStore.Instance.LastEnding==StoryEndings.SpaceId && ReferenceEquals(GameOverUIManager.Instance.darkScreen.Image.sprite,sprites.Sprites["ending_space"]),"leave invokes native space ending with art");
var titlePatch=typeof(StoryState).Assembly.GetType("AlienStory.NativeEndingSummaryPatch"); object[] titleArgs={StoryEndings.SpaceId,null};
Check(!(bool)titlePatch.GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,titleArgs) && (string)titleArgs[1]==StoryEndings.Title(StoryEndings.SpaceId),"run summary uses the custom ending title");
var nativeText=typeof(StoryState).Assembly.GetType("AlienStory.NativeEndingTextPatch"); StoryEndings.NativeContext=StoryEndings.SpaceId;
object[] nativeArgs={"原版預設", "原版預設", true}; nativeText.GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,nativeArgs); StoryEndings.NativeContext=null;
Check((string)nativeArgs[0]==StoryEndings.Title(StoryEndings.SpaceId) && (string)nativeArgs[1]==StoryEndings.Description(StoryEndings.SpaceId) && !(bool)nativeArgs[2],"native ending has custom text and cannot continue the run");
Check(PortraitRegistry.Bind(SpriteDict.Instance,PortraitRegistry.Id("adam_normal")),"portrait registration");
SpriteDict.Instance.spriteDictionary.Clear();
Check(PortraitRegistry.Bind(SpriteDict.Instance,PortraitRegistry.Id("adam_normal")),"portrait rebind after dictionary reset");
mod.OnDisable();
Console.WriteLine("PASS: integration simulation covers same-day preview, original preservation, both aid paths, 100 kcal, timing, trade, portraits, device and bad ending. Game APIs are stubs, not real in-game acceptance.");
