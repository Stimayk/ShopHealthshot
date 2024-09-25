using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using ShopAPI;

namespace ShopHealthshot
{
    public class ShopHealthshot : BasePlugin
    {
        public override string ModuleName => "[SHOP] Healthshot";
        public override string ModuleDescription => "";
        public override string ModuleAuthor => "E!N";
        public override string ModuleVersion => "v1.0.1";

        private IShopApi? SHOP_API;
        private const string CategoryName = "Healthshots";
        public static JObject? JsonHealthshot { get; private set; }
        private readonly PlayerHealthshots[] playerHealthshots = new PlayerHealthshots[65];

        public override void OnAllPluginsLoaded(bool hotReload)
        {
            SHOP_API = IShopApi.Capability.Get();
            if (SHOP_API == null) return;

            LoadConfig();
            InitializeShopItems();
            SetupTimersAndListeners();
        }

        private void LoadConfig()
        {
            string configPath = Path.Combine(ModuleDirectory, "../../configs/plugins/Shop/Healthshots.json");
            if (File.Exists(configPath))
            {
                JsonHealthshot = JObject.Parse(File.ReadAllText(configPath));
            }
        }

        private void InitializeShopItems()
        {
            if (JsonHealthshot == null || SHOP_API == null) return;

            SHOP_API.CreateCategory(CategoryName, "Шприцы");

            var sortedItems = JsonHealthshot
                .Properties()
                .Select(p => new { Key = p.Name, Value = (JObject)p.Value })
                .OrderBy(p => (int)p.Value["healthshots"]!)
                .ToList();

            foreach (var item in sortedItems)
            {
                Task.Run(async () =>
                {
                    int itemId = await SHOP_API.AddItem(item.Key, (string)item.Value["name"]!, CategoryName, (int)item.Value["price"]!, (int)item.Value["sellprice"]!, (int)item.Value["duration"]!);
                    SHOP_API.SetItemCallbacks(itemId, OnClientBuyItem, OnClientSellItem, OnClientToggleItem);
                }).Wait();
            }
        }

        private void SetupTimersAndListeners()
        {
            RegisterListener<Listeners.OnClientDisconnect>(playerSlot => playerHealthshots[playerSlot] = null!);
        }

        public HookResult OnClientBuyItem(CCSPlayerController player, int itemId, string categoryName, string uniqueName, int buyPrice, int sellPrice, int duration, int count)
        {
            if (TryGetNumberOfHealthshots(uniqueName, out int Healthshots))
            {
                playerHealthshots[player.Slot] = new PlayerHealthshots(Healthshots, itemId);
            }
            else
            {
                Logger.LogError($"{uniqueName} has invalid or missing 'healthshots' in config!");
            }
            return HookResult.Continue;
        }

        public HookResult OnClientToggleItem(CCSPlayerController player, int itemId, string uniqueName, int state)
        {
            if (state == 1 && TryGetNumberOfHealthshots(uniqueName, out int Healthshots))
            {
                playerHealthshots[player.Slot] = new PlayerHealthshots(Healthshots, itemId);
            }
            else if (state == 0)
            {
                OnClientSellItem(player, itemId, uniqueName, 0);
            }
            return HookResult.Continue;
        }

        public HookResult OnClientSellItem(CCSPlayerController player, int itemId, string uniqueName, int sellPrice)
        {
            playerHealthshots[player.Slot] = null!;
            return HookResult.Continue;
        }

        [GameEventHandler]
        public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
        {
            var player = @event.Userid;
            if (player != null && !player.IsBot && playerHealthshots[player.Slot] != null)
            {
                GiveHealthshots(player);
            }
            return HookResult.Continue;
        }

        private void GiveHealthshots(CCSPlayerController player)
        {
            var playerPawnValue = player.PlayerPawn.Value;
            var weaponServices = playerPawnValue?.WeaponServices;
            if (weaponServices == null) return;

            var curHealthshotCount = weaponServices.Ammo[20];
            var giveCount = playerHealthshots[player.Slot].Healthshots;

            for (var i = 0; i < giveCount - curHealthshotCount; i++)
            {
                player.GiveNamedItem("weapon_healthshot");
            }
        }

        private bool TryGetNumberOfHealthshots(string uniqueName, out int Healthshots)
        {
            Healthshots = 0;
            if (JsonHealthshot != null && JsonHealthshot.TryGetValue(uniqueName, out var obj) && obj is JObject jsonItem && jsonItem["healthshots"] != null && jsonItem["healthshots"]!.Type != JTokenType.Null)
            {
                Healthshots = (int)jsonItem["healthshots"]!;
                return true;
            }
            return false;
        }

        public record PlayerHealthshots(int Healthshots, int ItemID);
    }
}