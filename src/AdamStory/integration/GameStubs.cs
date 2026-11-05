using System;
using System.Collections.Generic;
using System.Linq;

namespace HarmonyLib {
    [AttributeUsage(AttributeTargets.Class)] public sealed class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string method) { } }
    [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyPrefix : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyPostfix : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyFinalizer : Attribute { }
}
namespace UnityEngine {
    public class Object { }
    public struct Color { public float r,g,b,a; public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;} public static Color white=>new(1,1,1,1); }
    public class Texture2D : Object { public int width = 100, height = 100; }
    public struct Rect { public Rect(float a,float b,float c,float d) { } }
    public struct Vector2 { public float x,y; public Vector2(float a,float b) { x=a;y=b; } }
    public class RectTransform { public Vector2 sizeDelta; }
    public class Sprite : Object { public Texture2D texture = new(); public float Ppu; public static Sprite Create(Texture2D t, Rect r, Vector2 v, float p) => new() { texture = t, Ppu=p }; }
    public class Transform : Object { public GameObject gameObject; public Transform Find(string name)=>null; }
    public class GameObject : Object { public UnityEngine.UI.Image Image=new(); public bool Active; public bool activeSelf=>Active; public Transform transform=new(); public T GetComponent<T>() where T:class => Image as T; public void SetActive(bool active)=>Active=active; }
    public static class Debug { public static void Log(object x) { } public static void LogError(object x) { } public static void LogWarning(object x) { } }
}
namespace UnityEngine.UI { public class Image { public enum Type { Simple } public Type type; public UnityEngine.Sprite sprite; public UnityEngine.Color color; public bool preserveAspect; public float alphaHitTestMinimumThreshold=0.00001f; public UnityEngine.RectTransform rectTransform=new(); } }
public interface IMod { void Init(ModManifest m); void OnEnable(); void OnDisable(); }
public class ModManifest { public string ID; }
public class LoadedMod { public bool IsEnabled; public ModManifest Manifest; public Dictionary<string,UnityEngine.Sprite> Sprites = new(); }
public static class ModLoader { public static List<LoadedMod> Mods = new(); }
public class SpriteDict {
    public static SpriteDict Instance = new();
    public Dictionary<string,UnityEngine.Sprite> spriteDictionary = new();
    public UnityEngine.Sprite GetSprite(string key) => spriteDictionary.TryGetValue(key,out var s) ? s : new();
}
public static class ModHook {
    public static event Action<StoreClientManager> OnGenerateCustomerVeryLate;
    public static event Action<ModItemDirectory> OnModItemDirectoryInit;
    public static event Action<RichTextBuilder,GameItem> OnCreateTooltipLate;
    public static void Tooltip(RichTextBuilder b,GameItem i)=>OnCreateTooltipLate?.Invoke(b,i);
    public static event Action OnWakingUpLate, OnGameLoadedLate;
    public static void Queue(StoreClientManager m) => OnGenerateCustomerVeryLate?.Invoke(m);
    public static void Items(ModItemDirectory m) => OnModItemDirectoryInit?.Invoke(m);
    public static void Wake() => OnWakingUpLate?.Invoke();
    public static void Loaded() => OnGameLoadedLate?.Invoke();
}
public static class ModHelper {
    public static Dictionary<string,object> Data = new();
    public static ModConfig Config = new(); public static ModConfig GetConfig(string id)=>Config;
    public static int GetModDataInt(string m,string k,int d) => Data.TryGetValue(m+":"+k,out var v) ? (int)v : d;
    public static bool GetModDataBool(string m,string k,bool d) => Data.TryGetValue(m+":"+k,out var v) ? (bool)v : d;
    public static void SetModDataInt(string m,string k,int v) => Data[m+":"+k]=v;
    public static void SetModDataBool(string m,string k,bool v) => Data[m+":"+k]=v;
    public static GameItem SetItemOwnerShip(GameItem item,bool owned){item.Owned=owned;return item;}
}
public class ConfigEntry<T> { public T Value; public void Set(T value){Value=value;} }
public class ModConfig { public bool Saved; public ConfigEntry<bool> Entry; public ConfigEntry<bool> Define(string key,bool val,string section,Func<string> label,Func<string> tooltip,bool restart)=>Entry ??= new(){Value=val}; public void Save()=>Saved=true; }
public class StoreStation { public static int Day; public static int GetDayCounter()=>Day; }
public static class StoreClientListDict { public static Dictionary<string,Func<StoreClient>> storeClientDict = new(); }
public class StoreClientManager { public List<StoreClient> clientStack = new(); public void AddClient(StoreClient c) => clientStack.Add(c); }
public class StoreClient {
    public enum ClientIntent { DIALOGUE, BUY, SELL, BARTER }
    public string identifier,sourceModId,displayName,spriteName;
    public ClientIntent clientIntent;
    public bool dismissable,arrestable,isBusinessComplete,isIntroduced;
    public List<string> clientBuyingTagList = new();
    public Dialogue mainDialogue = new(), acceptDealDialogue, allDoneDialogue;
    public Action OnArrival, OnDismiss;
    public BarterOffer barterOffer;
    public void CompleteClientCreation(bool x) { }
    public void SetBarterOffer(BarterOffer b) { barterOffer=b; clientIntent=ClientIntent.BARTER; }
    public void OnIntroduced() => isIntroduced=true;
    public void AddBasicDialog() { }
}
public class Dialogue {
    public string title,plainText; public Dialogue nextDialogue; public Action endAction;
    public List<(GameItem item,string caption,Dialogue next,Action action)> choices = new();
    public Dialogue SetText(string a,string b){title=a;plainText=b;return this;}
    public Dialogue SetEndAction(Action a){endAction=a;return this;}
    public Dialogue SetNextDialogue(Dialogue next){nextDialogue=next;return this;}
    public Dialogue NextDialogue()=>nextDialogue ??= new();
    public Dialogue AddChoice(GameItem i,string n,string d,Dialogue x,Action a,Func<bool> e){choices.Add((i,n,x,a));return this;}
}
public class BarterResult { public bool isAccepted; public string response; }
public class BarterOffer {
    public string barterTextBody,acceptedResponse,deniedResponse; public Func<BarterResult> getBarterResult; public Action onInstance;
    public void InstanciateOffer() => onInstance?.Invoke();
}
public class StoreClientInstance { public StoreClient client; public StoreClient GetClientBlueprint()=>client; }
public class PlayerStore {
    public static PlayerStore Instance = new(); public StoreClientInstance currentClientInstance;
    public int NativeEndCalls; public string LastEnding;
    public void DismissCurrentClient(bool a,bool b,bool c) { currentClientInstance?.client?.OnDismiss?.Invoke(); currentClientInstance=null; }
    public bool TryAddDirectSellingItemToTable(string id,bool a,bool b,bool c,int d)=>true;
    public void AcceptBarter(){currentClientInstance.client.isBusinessComplete=true; EmporiumEntry.Instance.backInvinvElement.childItems.Clear();}
    public void ExecuteGameOver(string id){NativeEndCalls++;LastEnding=id;GameOverUIManager.Instance.SetEnding(id);GameOverUIManager.Instance.DisplayUI("rent","fallback",false);}
}
public class RichTextBuilder { public List<string> lines=new(); public RichTextBuilder AddLine(string text){lines.Add(text);return this;} }
public class GameItem {
    public string identifier,name,shortDescription; public bool activateDefault,Owned; public int unitCount=1; public GameInventory parentInventory; public GridShape shape,modifiedShape; public int modifiedXOrigin,modifiedYOrigin; public object modifiedState; public Action<GameItem> onLoaded;
    public Dictionary<string,int> visibleTags; public HashSet<string> visibleEnabled;
    public Dictionary<string,int> tags = new(); public HashSet<string> enabledTags = new(), types = new(); public int purity;
    public Func<GameItem,GameInventory,SlotMarker,bool> mayActivateSlotItemFunc,canActivateSlotItemFunc;
    public Action<GameItem,GameInventory,SlotMarker> onActivateSlotItemFunc;
    public bool IsTag(string k)=>(visibleEnabled ?? enabledTags).Contains(k); public TagState GetTagReadonly(string k)=>new(this,k,true);
    public GameItem ModifyTag(string k,Action<TagState> a,bool skipValidate){if(skipValidate && visibleTags==null){visibleTags=new(tags);visibleEnabled=new(enabledTags);} a(new(this,k,false)); if(!skipValidate){visibleTags=null;visibleEnabled=null;} return this;}
    public bool IsGameItemType(string s)=>types.Contains(s);
    public GameItem SetSpriteAndShapeFromMod(string a,string b)=>this;
    public GameItem SetShape(GridShape a){shape=a;modifiedShape=a;return this;}
    public GameItem SetStack(int n){unitCount=n;return this;}
}
public class TagState { private readonly GameItem item; private readonly string key; private readonly bool readOnly; public TagState(GameItem i,string k,bool r=false){item=i;key=k;readOnly=r;} public int GetInt()=>(readOnly ? item.visibleTags ?? item.tags : item.tags).GetValueOrDefault(key); public TagState Enable(){item.enabledTags.Add(key);return this;} public TagState SetInt(int v){item.tags[key]=v;return this;} }
public static class FoodItemHelper { public const string CALORIE_VALUE_TAG="CALORIE_VALUE_TAG"; }
public static class WaterHelper { public const string LIQUID_CONTAINER_TAG="LIQUID_CONTAINER_TAG"; public static int GetWaterPurity(GameItem i)=>i.purity; }
public class GameInventory {
    public List<GameItem> childItems = new(); public int transferLimit=100, slotsLeft=100;
    public SlotMarker TryFindOneValidInventorySlot(GameItem i)=>transferLimit>0 && slotsLeft>0 ? new SlotMarker{numTransfer=transferLimit,item=i,inventory=this} : null;
}
public class GameGridInventory : GameInventory {
    public bool Fits=true; public int Width=10,Height=10;
    public bool CheckInventorySlotStillValid(GameItem item,GridShape shape,object state) {
        if(!Fits || shape.minX<0 || shape.minY<0 || shape.minX+shape.width>Width || shape.minY+shape.height>Height) return false;
        return childItems.Where(x=>x!=item && x.modifiedShape!=null).All(x=>shape.minX+shape.width<=x.modifiedShape.minX || x.modifiedShape.minX+x.modifiedShape.width<=shape.minX || shape.minY+shape.height<=x.modifiedShape.minY || x.modifiedShape.minY+x.modifiedShape.height<=shape.minY);
    }
}
public class SlotMarker {
    public int numTransfer; public GameItem item,targetItem; public GameInventory inventory;
    public int TryAcceptOnce(int n){int count=Math.Min(n,numTransfer);if(count<=0)return 0;item.unitCount=count;item.parentInventory=inventory;inventory.childItems.Add(item);inventory.slotsLeft--;return count;}
}
public class EmporiumEntry {
    public static EmporiumEntry Instance = new(); public GameGridInventory invElement = new(), backInvinvElement = new();
    public List<GameItem> GetOwnedItemsBack()=>backInvinvElement.childItems.Where(x=>x.Owned).ToList();
    public List<GameItem> GetAllItems()=>invElement.childItems.Concat(backInvinvElement.childItems).ToList();
}
public class GridShape { public int width,height,orientation,minX,minY; public bool flipped; public int globalWidth=>width; public int globalHeight=>height; public byte fill; }
public class GridShapeBuilder { private GridShape shape=new(); public GridShapeBuilder SetDataFill(int w,int h,byte v){shape.width=w;shape.height=h;shape.fill=v;return this;} public GridShapeBuilder SetTransform(GridShape old){shape.orientation=old.orientation;shape.flipped=old.flipped;shape.minX=old.minX;shape.minY=old.minY;return this;} public GridShape Build()=>shape; }
public class TreeNodeRender { public UnityEngine.UI.Image image=new(); }
public class GameItemElement : GameItem { public TreeNodeRender background=new(), fakeBackground=new(); }
public static class ItemDirectory { public static GameItem CreateEmptyItem(string id)=>new GameItemElement(){identifier=id}; }
public class ModItemDirectory { public bool Add(string id,Func<GameItem> f)=>DirectoryMaster.factories.TryAdd(id,f); }
public static class DirectoryMaster {
    public static Dictionary<string,Func<GameItem>> factories=new();
    public static bool Has<T>(string id)=>factories.ContainsKey(id);
    public static GameItem Item(string id,bool x)=>factories.TryGetValue(id,out var f) ? f() : new GameItem{identifier=id};
}
public class CustomUIManager {
    public const string LAYER_OVERLAY="overlay"; public static CustomUIManager Instance=new(); public static string LastImage,LastTitle;
    public static CustomUIBuilder Window(string id,string title,string layer){LastTitle=title;return new();}
    public void CloseWindow(string id) { }
}
public class CustomUIBuilder {
    public CustomUIBuilder SetSize(float x,float y)=>this; public CustomUIBuilder Center()=>this; public CustomUIBuilder SetCloseOnEscape(bool x)=>this;
    public CustomUIBuilder AddImage(string k,float x,float y){CustomUIManager.LastImage=k;return this;}
    public CustomUIBuilder AddLabel(string s)=>this; public CustomUIBuilder AddButton(string s,Action a)=>this; public void Show(){ }
}
public class GameOverUIManager {
    public static GameOverUIManager Instance=new(); public UnityEngine.GameObject darkScreen=new(),continuePlayButton=new(),uiPanel=new(); public string Ending,Title,Description; public bool Continue;
    public void SetEnding(string id)=>Ending=id;
    public void DisplayUI(string a,string b,bool c){Title=a;Description=b;Continue=c;darkScreen.SetActive(true);}
    public void OnMenuButtonClick() { }
    public void ContinuePlayClick(){darkScreen.SetActive(false);}
}
public class RunSummaryUI { }
public static class MachineCassettePlayer { public static void PauseForGameOver() { } }
public class DialogUIManager { public const string PLAYER="PLAYER"; }
public class NegociationUIManager { }
public class UISettings { public static UISettings current=new(); public int gridSize=16; }
public static class AudioHelper { public const string TURBO="turbo"; }
public class AudioManager { public static AudioManager Instance=new(); public string LastSound; public bool Play2(string key){LastSound=key;return true;} }
public static class GeneralHelper { public static bool IsItemEffectivelyOwned(GameItem item)=>item.Owned; }
