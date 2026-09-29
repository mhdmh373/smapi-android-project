using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace BigInventory
{
    /// <summary>
    /// مود تكبير حقيبة اللاعب على أندرويد.
    /// يستخدم Reflection فقط ولا يعتمد على <c>StardewValley.dll</c> مباشرة.
    /// </summary>
    internal class ModEntry : Mod
    {
        /// <summary>أصغر حجم مسموح (multiple of 12).</summary>
        private const int MinSize = 12;

        /// <summary>أكبر حجم مسموح (multiple of 12).</summary>
        private const int MaxSize = 120;

        private IModHelper modHelper;
        private ModConfig Config;
        private bool refreshingMenu;

        /// <inheritdoc />
        public override void Entry(IModHelper helper)
        {
            this.modHelper = helper;
            this.Config = helper.ReadConfig<ModConfig>() ?? new ModConfig();
            this.Config.InventorySize = Sanitize(this.Config.InventorySize);
            helper.WriteConfig(this.Config);

            // 1) تشغيل الاكتشاف مبكراً (حتى لو لم تكن الحقيبة جاهزة بعد)
            this.RunDiagnosticsOnce("startup");

            // 2) إعادة تطبيق الحجم عند تحميل الحفظ أو بدء يوم جديد
            helper.Events.GameLoop.SaveLoaded += (s, e) => this.ApplySize("save loaded");
            helper.Events.GameLoop.DayStarted += (s, e) => this.ApplySize("day started");

            // 3) تحديث الواجهة عند فتح/تغيير القائمة
            helper.Events.Display.MenuChanged += this.OnMenuChanged;

            // 4) أمر كونسول: biginventory get | set <slots> | discover
            helper.ConsoleCommands.Add(
                "biginventory",
                "biginventory [get | set <slots> | discover] - show, change, or diagnose the backpack size.",
                this.OnConsoleCommand);
        }

        /// <summary>يقرّب العدد إلى مضاعف 12 ضمن النطاق المسموح.</summary>
        private static int Sanitize(int size)
        {
            if (size < MinSize)
                size = MinSize;
            if (size > MaxSize)
                size = MaxSize;

            if (size % MinSize != 0)
                size = ((size / MinSize) + 1) * MinSize;

            return size;
        }

        /// <summary>يطبع اكتشاف أعضاء Farmer مرة واحدة فقط.</summary>
        private void RunDiagnosticsOnce(string reason)
        {
            if (GameAccess.DiagnosticsLogged)
                return;

            object player = GameAccess.GetPlayer();
            if (player == null)
            {
                this.Monitor.LogOnce(
                    $"Big Inventory: can't run diagnostics yet ({reason}): the player isn't loaded.",
                    LogLevel.Debug);
                return;
            }

            GameAccess.DiagnosticsLogged = true;
            this.Monitor.Log(
                $"Big Inventory: running reflection diagnostics ({reason}).",
                LogLevel.Info);
            GameAccess.DumpFarmerInventoryMembers(this.Monitor, player);
        }

        /// <summary>يطبّق الحجم المطلوب على حقيبة اللاعب.</summary>
        private void ApplySize(string reason)
        {
            if (!Context.IsWorldReady || this.refreshingMenu)
                return;

            // محاولة الاكتشاف إن لم يُنفّذ بعد
            this.RunDiagnosticsOnce(reason);

            object player = GameAccess.GetPlayer();
            if (player == null)
                return;

            object inventory = GameAccess.GetInventory(player);
            if (inventory == null)
            {
                this.Monitor.LogOnce(
                    "Big Inventory: couldn't find the player inventory, skipping.",
                    LogLevel.Warn);
                return;
            }

            int target = this.Config.InventorySize;
            int current = GameAccess.GetCapacity(inventory, out string source);

            if (current < 0)
            {
                // لم يُعثر على عضو سعة: لا نعدّل شيئاً، الاكتشاف فقط
                this.Monitor.LogOnce(
                    "Big Inventory: couldn't find any capacity member on the inventory, " +
                    "so no changes were made. See the diagnostics above.",
                    LogLevel.Warn);
                GameAccess.ReportCandidates(
                    this.Monitor,
                    inventory.GetType().Name,
                    inventory.GetType(),
                    inventory,
                    "Capacity", "capacity", "MaxItems", "Size");
                return;
            }

            if (current == target)
                return;

            IList items = GameAccess.GetItems(inventory);

            if (target < current && items != null && items.Count > target)
            {
                List<string> lost = GameAccess.GetItemNames(items.Cast<object>().Skip(target));

                if (!this.Config.AllowShrinkWithItems)
                {
                    this.Monitor.Log(
                        $"Big Inventory: the backpack still holds {items.Count} items, so it can't shrink to {target} slots. " +
                        "Store items in a chest and try again.",
                        LogLevel.Warn);
                    return;
                }

                this.Monitor.Log(
                    $"Big Inventory: shrinking {current} -> {target} slots via {source}. " +
                    $"{lost.Count} item(s) won't fit: {string.Join(", ", lost.Take(8))}{(lost.Count > 8 ? ", ..." : "")}",
                    LogLevel.Warn);
            }

            if (!GameAccess.SetCapacity(inventory, target))
            {
                this.Monitor.LogOnce(
                    "Big Inventory: found a capacity member but couldn't write it, skipping.",
                    LogLevel.Warn);
                return;
            }

            this.Monitor.Log($"Big Inventory: backpack is now {target} slots (via {source}, {reason}).", LogLevel.Info);

            if (GameAccess.GetActiveClickableMenu() != null)
                this.RefreshMenu();
        }

        /// <summary>يعيد بناء واجهة الحقيبة المفتوحة لتظهر الخانات الجديدة.</summary>
        private void RefreshMenu()
        {
            this.refreshingMenu = true;
            try
            {
                object menu = GameAccess.GetActiveClickableMenu();
                if (menu == null)
                    return;

                Type menuType = menu.GetType();
                if (menuType.FullName != "StardewValley.Menus.InventoryMenu")
                    return;

                // نبحث عن constructor بلا معاملات أولاً (سلوك الإصدار القديم)
                System.Reflection.ConstructorInfo ctor =
                    menuType.GetConstructors()
                        .FirstOrDefault(c => c.GetParameters().Length == 0)
                    ?? menuType.GetConstructors().FirstOrDefault();

                if (ctor == null)
                    return;

                object newMenu;
                try
                {
                    // نمرر قيمة افتراضية للوسيط إن وُجد
                    object[] args = ctor.GetParameters().Length == 0
                        ? Array.Empty<object>()
                        : new object[] { Enum.Parse(ctor.GetParameters()[0].ParameterType, "All") };
                    newMenu = ctor.Invoke(args);
                }
                catch
                {
                    return;
                }

                if (newMenu != null)
                    GameAccess.SetActiveMenu(newMenu);
            }
            finally
            {
                this.refreshingMenu = false;
            }
        }

        /// <summary>عند تغيير القائمة: إذا كانت واجهة الحقيبة، طبّق الحجم.</summary>
        private void OnMenuChanged(object sender, MenuChangedEventArgs e)
        {
            // نقرأ NewMenu عبر Reflection لتجنب أي اعتماد على أنواع اللعبة
            object newMenu = ReadMenuProperty(e, "NewMenu");
            if (newMenu == null)
                return;

            if (newMenu.GetType().FullName != "StardewValley.Menus.InventoryMenu")
                return;

            if (this.refreshingMenu)
                return;

            this.ApplySize("inventory opened");
        }

        /// <summary>يقرأ خاصية من كائن بدون توليد أي اعتماد على نوعها.</summary>
        private static object ReadMenuProperty(object instance, string propertyName)
        {
            if (instance == null)
                return null;

            try
            {
                System.Reflection.PropertyInfo prop = instance.GetType().GetProperty(
                    propertyName,
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic);

                return prop?.GetValue(instance);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>أمر الكونسول: get | set | discover</summary>
        private void OnConsoleCommand(string name, string[] args)
        {
            string subCommand = args != null && args.Length > 0 ? args[0]?.ToLowerInvariant() : "get";

            switch (subCommand)
            {
                case "discover":
                    GameAccess.DiagnosticsLogged = false;
                    this.RunDiagnosticsOnce("console discover");
                    return;

                case "set":
                    this.HandleSet(args);
                    return;

                default:
                    this.HandleGet();
                    return;
            }
        }

        private void HandleGet()
        {
            object player = GameAccess.GetPlayer();
            int current = -1;
            string source = null;

            if (Context.IsWorldReady && player != null)
            {
                object inventory = GameAccess.GetInventory(player);
                if (inventory != null)
                    current = GameAccess.GetCapacity(inventory, out source);
            }

            if (current < 0)
            {
                this.Monitor.Log(
                    $"Big Inventory: configured size = {this.Config.InventorySize} slots (no world loaded).",
                    LogLevel.Info);
                this.Monitor.Log("Usage: biginventory [get | set <slots> | discover]", LogLevel.Info);
                return;
            }

            this.Monitor.Log(
                $"Big Inventory: backpack is {current} slots (via {source}), configured size is {this.Config.InventorySize} slots.",
                LogLevel.Info);
        }

        private void HandleSet(string[] args)
        {
            if (args == null || args.Length < 2 || !int.TryParse(args[1], out int value))
            {
                this.Monitor.Log(
                    "Usage: biginventory set <slots>   (12-120, rounded up to a multiple of 12)",
                    LogLevel.Error);
                return;
            }

            value = Sanitize(value);
            this.Config.InventorySize = value;

            this.Monitor.Log(
                $"Big Inventory: setting backpack size to {value} slots. " +
                "If a slot row isn't shown, restart the game.",
                LogLevel.Info);

            if (!Context.IsWorldReady)
            {
                this.modHelper.WriteConfig(this.Config);
                this.Monitor.Log("No world loaded, so the setting was only saved for next launch.", LogLevel.Info);
                return;
            }

            this.ApplySize("console command");
            this.modHelper.WriteConfig(this.Config);
        }
    }
}
