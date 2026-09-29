using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StardewModdingAPI;

namespace BigInventory
{
    /// <summary>
    /// الوصول إلى كائنات اللعبة عبر Reflection فقط.
    /// لا يوجد أي مرجع مباشر إلى StardewValley.dll ولا Harmony.
    /// كل اسم عضو يستخدم هنا هو اسم مرشّح صريح، ولا يُخمَّن أي اسم جديد.
    /// </summary>
    internal static class GameAccess
    {
        /// <summary>Instance | Public | NonPublic.</summary>
        private const BindingFlags Public = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>Instance | Public | NonPublic | FlattenHierarchy.</summary>
        private const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

        /// <summary>Instance | NonPublic فقط.</summary>
        private const BindingFlags Internal = BindingFlags.Instance | BindingFlags.NonPublic;

        private static bool searchedForGame1;
        private static Type game1Type;
        private static PropertyInfo activeMenuProp;

        /// <summary>سجل الاكتشاف يُطبع مرة واحدة فقط لتفادي تكرار السجل.</summary>
        internal static bool DiagnosticsLogged { get; set; }

        #region Game1 and player

        /// <summary>يجد نوع <c>StardewValley.Game1</c> دون افتراض اسم التجميع.</summary>
        internal static Type Game1Type
        {
            get
            {
                if (searchedForGame1)
                    return game1Type;

                searchedForGame1 = true;
                game1Type = FindGame1Type();
                return game1Type;
            }
        }

        private static Type FindGame1Type()
        {
            // أسماء تجميعات اللعبة المحتملة (Windows / Linux / macOS)
            foreach (string assemblyName in new[] { "Stardew Valley", "StardewValley" })
            {
                try
                {
                    Type found = Type.GetType($"StardewValley.Game1, {assemblyName}");
                    if (found != null)
                        return found;
                }
                catch
                {
                    // تجاهل: نجرّب الاسم التالي
                }
            }

            // بحث احتياطي في التجميعات المحمّلة
            try
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type found = assembly.GetType("StardewValley.Game1");
                    if (found != null)
                        return found;
                }
            }
            catch
            {
                // تجاهل
            }

            return null;
        }

        /// <summary>يلتقط اللاعب الحالي عبر <c>Game1.player</c>.</summary>
        internal static object GetPlayer()
        {
            Type type = Game1Type;
            if (type == null)
                return null;

            try
            {
                PropertyInfo prop = type.GetProperty("player", All);
                if (prop != null)
                    return prop.GetValue(null);

                FieldInfo field = type.GetField("player", All);
                if (field != null)
                    return field.GetValue(null);
            }
            catch
            {
                // تجاهل
            }

            return null;
        }

        /// <summary>القائمة النشطة حالياً (نستخدمها لتحديث واجهة الحقيبة).</summary>
        internal static object GetActiveClickableMenu()
        {
            if (activeMenuProp == null)
            {
                Type type = Game1Type;
                if (type != null)
                    activeMenuProp = type.GetProperty("activeClickableMenu", All);
            }

            try
            {
                return activeMenuProp?.GetValue(null);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>يستبدل القائمة النشطة (لإعادة رسم واجهة الحقيبة بعد تغيير الحجم).</summary>
        internal static bool SetActiveMenu(object menu)
        {
            if (activeMenuProp == null || !activeMenuProp.CanWrite)
                return false;

            try
            {
                activeMenuProp.SetValue(null, menu);
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Inventory

        /// <summary>
        /// يبحث عن حقيبة اللاعب بالترتيب: <c>inventory</c> ثم مرشّحات اسمية ثم أي عضو من نوع قائمة.
        /// كل الأسماء المستخدمة هنا مرشّحات صريحة (مذكورة في الطلب أو معروفة من بنية SMAPI العامة).
        /// </summary>
        internal static object GetInventory(object player)
        {
            if (player == null)
                return null;

            Type playerType = player.GetType();

            // 1) المرشّح الأساسي
            object inventory = TryGetNamedValue(playerType, player, "inventory", All, Internal);
            if (inventory != null)
                return inventory;

            // 2) مرشّحات اسمية إضافية
            foreach (string name in new[] { "Items", "items", "Backpack", "backpack" })
            {
                object candidate = TryGetNamedValue(playerType, player, name, All, Internal);
                if (candidate is IList)
                    return candidate;
            }

            // 3) بحث عام: أي عضو من نوع IList يحمل كلمة من كلمات الحقيبة
            return FindListMember(playerType, player);
        }

        /// <summary>يقرأ قائمة عناصر الحقيبة كـ <see cref="IList"/>.</summary>
        internal static IList GetItems(object inventory)
        {
            if (inventory == null)
                return null;

            // قد تكون الحقيبة نفسها قائمة
            if (inventory is IList direct)
                return direct;

            Type type = inventory.GetType();

            // 1) خاصية تعيد IList مباشرة
            PropertyInfo itemsProp =
                type.GetProperty("Items", All)
                ?? type.GetProperty("items", All)
                ?? type.GetProperty("Items", Internal)
                ?? type.GetProperty("items", Internal);

            if (itemsProp != null && typeof(IList).IsAssignableFrom(itemsProp.PropertyType))
                return itemsProp.GetValue(inventory) as IList;

            // 2) خاصية تعيد IEnumerable مغلفة (قائمة strong-typed)
            if (itemsProp != null && typeof(IEnumerable).IsAssignableFrom(itemsProp.PropertyType))
            {
                object value = itemsProp.GetValue(inventory);
                if (value is IEnumerable enumerable)
                    return ToList(enumerable);
            }

            // 3) دالة getItems()
            MethodInfo getItems = type.GetMethod("getItems", Type.EmptyTypes);
            if (getItems != null && typeof(IEnumerable).IsAssignableFrom(getItems.ReturnType))
            {
                object value = getItems.Invoke(inventory, null);
                if (value is IEnumerable enumerable)
                    return ToList(enumerable);
            }

            // 4) حقل items
            FieldInfo itemsField =
                type.GetField("items", All)
                ?? type.GetField("Items", All);

            if (itemsField != null && typeof(IEnumerable).IsAssignableFrom(itemsField.FieldType))
            {
                object value = itemsField.GetValue(inventory);
                if (value is IEnumerable enumerable)
                    return ToList(enumerable);
            }

            return null;
        }

        /// <summary>يقرأ أسماء العناصر داخل قائمة بأمان.</summary>
        internal static List<string> GetItemNames(IEnumerable items)
        {
            List<string> names = new();
            if (items == null)
                return names;

            try
            {
                foreach (object item in items)
                {
                    if (item == null)
                        continue;

                    try
                    {
                        string name = item.GetType().GetProperty("Name", All)?.GetValue(item) as string;
                        names.Add(name ?? item.GetType().Name);
                    }
                    catch
                    {
                        names.Add("?");
                    }
                }
            }
            catch
            {
                // تجاهل
            }

            return names;
        }

        private static IList ToList(IEnumerable enumerable)
        {
            try
            {
                return enumerable.Cast<object>().ToList();
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Capacity

        /// <summary>
        /// يقرأ سعة الحقيبة الحالية. يعيد -1 إذا لم يُعثر على عضو سعة قابل للقراءة.
        /// </summary>
        internal static int GetCapacity(object inventory, out string source)
        {
            source = null;

            if (inventory == null)
                return -1;

            Type type = inventory.GetType();

            // 1) خاصية أو حقل سعة مباشر
            PropertyInfo capacityProp =
                type.GetProperty("Capacity", All)
                ?? type.GetProperty("capacity", All)
                ?? type.GetProperty("Size", All)
                ?? type.GetProperty("size", All);

            if (capacityProp != null && capacityProp.PropertyType == typeof(int) && capacityProp.CanRead)
            {
                source = $"{type.Name}.{capacityProp.Name} (property)";
                return (int)capacityProp.GetValue(inventory);
            }

            FieldInfo capacityField =
                type.GetField("capacity", All)
                ?? type.GetField("Capacity", All)
                ?? type.GetField("size", All);

            if (capacityField != null && capacityField.FieldType == typeof(int))
            {
                source = $"{type.Name}.{capacityField.Name} (field)";
                return (int)capacityField.GetValue(inventory);
            }

            // 2) مرشّحات السعة الاسمية (منها MaxItems وهي فرضية غير مؤكدة)
            foreach (string name in CapacityCandidates)
            {
                PropertyInfo prop = type.GetProperty(name, All) ?? type.GetProperty(name, Internal);
                if (prop != null && prop.PropertyType == typeof(int) && prop.CanRead)
                {
                    source = $"{type.Name}.{prop.Name} (property)";
                    return (int)prop.GetValue(inventory);
                }

                FieldInfo field = type.GetField(name, All) ?? type.GetField(name, Internal);
                if (field != null && field.FieldType == typeof(int))
                {
                    source = $"{type.Name}.{field.Name} (field)";
                    return (int)field.GetValue(inventory);
                }
            }

            // 3) قيمة IList.Count كبديل للقراءة فقط
            if (inventory is IList list)
            {
                source = $"{type.Name}.Count (IList, read-only)";
                return list.Count;
            }

            return -1;
        }

        /// <summary>يكتب سعة الحقيبة. يعيد true عند النجاح.</summary>
        internal static bool SetCapacity(object inventory, int value)
        {
            if (inventory == null)
                return false;

            Type type = inventory.GetType();

            PropertyInfo capacityProp =
                type.GetProperty("Capacity", All)
                ?? type.GetProperty("capacity", All)
                ?? type.GetProperty("Size", All)
                ?? type.GetProperty("size", All);

            if (capacityProp != null && capacityProp.PropertyType == typeof(int) && capacityProp.CanWrite)
            {
                capacityProp.SetValue(inventory, value);
                return true;
            }

            FieldInfo capacityField =
                type.GetField("capacity", All)
                ?? type.GetField("Capacity", All)
                ?? type.GetField("size", All);

            if (capacityField != null && capacityField.FieldType == typeof(int) && !capacityField.IsInitOnly)
            {
                capacityField.SetValue(inventory, value);
                return true;
            }

            foreach (string name in CapacityCandidates)
            {
                PropertyInfo prop = type.GetProperty(name, All) ?? type.GetProperty(name, Internal);
                if (prop != null && prop.PropertyType == typeof(int) && prop.CanWrite)
                {
                    prop.SetValue(inventory, value);
                    return true;
                }

                FieldInfo field = type.GetField(name, All) ?? type.GetField(name, Internal);
                if (field != null && field.FieldType == typeof(int) && !field.IsInitOnly)
                {
                    field.SetValue(inventory, value);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// مرشّحات اسم السعة. <c>MaxItems</c> فرضية غير مؤكدة طُلبت تجربتها،
        /// والباقي أسماء شائعة في بنية اللعبة.
        /// </summary>
        private static readonly string[] CapacityCandidates =
        {
            "MaxItems", "maxItems", "MaxSize", "maxSize", "InventorySize", "inventorySize"
        };

        #endregion

        #region Diagnostics

        /// <summary>
        /// الكلمات المفتاحية المطلوبة في المهمة: item, inventory, max, size, capacity, backpack.
        /// </summary>
        private static readonly string[] Keywords =
        {
            "item", "inventory", "max", "size", "capacity", "backpack"
        };

        /// <summary>
        /// يطبع في السجل كل أعضاء Farmer المتعلقة بالحقيبة والسعة مع الأنواع والقيم.
        /// هذه الدالة لا تعدّل أي شيء في اللعبة: قراءة فقط.
        /// </summary>
        internal static void DumpFarmerInventoryMembers(IMonitor monitor, object player)
        {
            if (monitor == null || player == null)
                return;

            try
            {
                Type type = player.GetType();
                monitor.Log($"Big Inventory diagnostics: Farmer type = {type.FullName}", LogLevel.Info);

                int properties = 0;
                int fields = 0;
                int methods = 0;

                // 1) الخصائص
                foreach (PropertyInfo prop in type.GetProperties(All).OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    if (!MatchesKeyword(prop.Name))
                        continue;

                    properties++;
                    monitor.Log(
                        $"  PROPERTY {prop.Name} : {FriendlyName(prop.PropertyType)}" +
                        $" | canRead={prop.CanRead} canWrite={prop.CanWrite}" +
                        $" | value = {DescribeValue(ReadProperty(prop, player))}",
                        LogLevel.Debug);
                }

                // 2) الحقول
                foreach (FieldInfo field in type.GetFields(All).OrderBy(f => f.Name, StringComparer.Ordinal))
                {
                    if (!MatchesKeyword(field.Name))
                        continue;

                    fields++;
                    monitor.Log(
                        $"  FIELD {field.Name} : {FriendlyName(field.FieldType)}" +
                        $" | static={field.IsStatic} initOnly={field.IsInitOnly}" +
                        $" | value = {DescribeValue(ReadField(field, player))}",
                        LogLevel.Debug);
                }

                // 3) التوابع بلا معاملات (قد تكون getters للحقيبة)
                foreach (MethodInfo method in type.GetMethods(All).OrderBy(m => m.Name, StringComparer.Ordinal))
                {
                    if (method.IsSpecialName || method.GetParameters().Length != 0)
                        continue;
                    if (!MatchesKeyword(method.Name))
                        continue;

                    methods++;
                    monitor.Log(
                        $"  METHOD {method.Name}() : returns {FriendlyName(method.ReturnType)}",
                        LogLevel.Debug);
                }

                monitor.Log(
                    $"Big Inventory diagnostics: matched {properties} properties, {fields} fields, {methods} methods.",
                    LogLevel.Info);

                // 4) تجربة المرشحين المذكورين في المهمة
                ReportCandidates(monitor, "Farmer", type, player, "inventory", "Items", "MaxItems");
            }
            catch (Exception ex)
            {
                monitor.Log($"Big Inventory diagnostics failed: {ex.Message}", LogLevel.Warn);
            }
        }

        /// <summary>
        /// يطبع حالة كل مرشّح: هل هو موجود، وما نوعه، وما قيمته.
        /// لا يكتب شيئاً في اللعبة.
        /// </summary>
        internal static void ReportCandidates(IMonitor monitor, string ownerName, Type type, object instance, params string[] names)
        {
            if (monitor == null || type == null)
                return;

            foreach (string name in names)
            {
                PropertyInfo prop = type.GetProperty(name, All) ?? type.GetProperty(name, Internal);
                if (prop != null)
                {
                    monitor.Log(
                        $"  CANDIDATE {ownerName}.{name} : PROPERTY of {FriendlyName(prop.PropertyType)}" +
                        $" | canRead={prop.CanRead} canWrite={prop.CanWrite}" +
                        $" | value = {DescribeValue(ReadProperty(prop, instance))}",
                        LogLevel.Debug);
                    continue;
                }

                FieldInfo field = type.GetField(name, All) ?? type.GetField(name, Internal);
                if (field != null)
                {
                    monitor.Log(
                        $"  CANDIDATE {ownerName}.{name} : FIELD of {FriendlyName(field.FieldType)}" +
                        $" | static={field.IsStatic} initOnly={field.IsInitOnly}" +
                        $" | value = {DescribeValue(ReadField(field, instance))}",
                        LogLevel.Debug);
                    continue;
                }

                MethodInfo method = type.GetMethod(name, Type.EmptyTypes);
                if (method != null)
                {
                    monitor.Log(
                        $"  CANDIDATE {ownerName}.{name} : METHOD returns {FriendlyName(method.ReturnType)}",
                        LogLevel.Debug);
                    continue;
                }

                monitor.Log($"  CANDIDATE {ownerName}.{name} : not found", LogLevel.Debug);
            }
        }

        private static object ReadProperty(PropertyInfo prop, object instance)
        {
            if (prop == null || !prop.CanRead)
                return null;

            try
            {
                return prop.GetValue(instance);
            }
            catch (Exception ex)
            {
                return $"<error: {ex.GetType().Name}>";
            }
        }

        private static object ReadField(FieldInfo field, object instance)
        {
            if (field == null)
                return null;

            try
            {
                return field.GetValue(instance);
            }
            catch (Exception ex)
            {
                return $"<error: {ex.GetType().Name}>";
            }
        }

        /// <summary>مطابقة اسم العضو مع كلمات الاكتشاف المطلوبة.</summary>
        private static bool MatchesKeyword(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            foreach (string keyword in Keywords)
            {
                if (name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        /// <summary>يحوّل قيمة إلى نص مقروء.</summary>
        private static string DescribeValue(object value)
        {
            if (value == null)
                return "null";

            if (value is string text)
                return $"\"{text}\"";

            if (value is IList list)
                return $"IList[{list.Count}]";

            if (value is ICollection collection)
                return $"{FriendlyName(value.GetType())} (Count={collection.Count})";

            if (value is IEnumerable enumerable)
            {
                int count = 0;
                try
                {
                    foreach (object _ in enumerable)
                    {
                        if (++count >= 1000)
                            break;
                    }
                }
                catch
                {
                    return $"IEnumerable (enumeration failed, {FriendlyName(value.GetType())})";
                }

                return $"IEnumerable (enumerated {count}, {FriendlyName(value.GetType())})";
            }

            try
            {
                return $"{value} ({FriendlyName(value.GetType())})";
            }
            catch
            {
                return FriendlyName(value.GetType());
            }
        }

        /// <summary>اسم النوع مع تبسيط الأسماء العامة.</summary>
        private static string FriendlyName(Type type)
        {
            if (type == null)
                return "?";

            if (!type.IsGenericType)
                return type.Name;

            string baseName = type.Name;
            int tick = baseName.IndexOf('`');
            if (tick > 0)
                baseName = baseName.Substring(0, tick);

            return $"{baseName}<{string.Join(",", type.GetGenericArguments().Select(FriendlyName))}>";
        }

        #endregion

        #region Helpers

        /// <summary>يقرأ قيمة عضو بالاسم مع مجموعتين من أعلام Reflection.</summary>
        private static object TryGetNamedValue(Type type, object instance, string name, BindingFlags primary, BindingFlags secondary)
        {
            PropertyInfo prop = type.GetProperty(name, primary) ?? type.GetProperty(name, secondary);
            if (prop != null && prop.CanRead)
            {
                object value = ReadProperty(prop, instance);
                if (value != null)
                    return value;
            }

            FieldInfo field = type.GetField(name, primary) ?? type.GetField(name, secondary);
            if (field != null)
            {
                object value = ReadField(field, instance);
                if (value != null)
                    return value;
            }

            return null;
        }

        /// <summary>يبحث عن أول عضو من نوع IList يحمل كلمة من كلمات الحقيبة.</summary>
        private static object FindListMember(Type type, object instance)
        {
            try
            {
                foreach (PropertyInfo prop in type.GetProperties(All))
                {
                    if (!typeof(IList).IsAssignableFrom(prop.PropertyType) || !prop.CanRead)
                        continue;
                    if (!MatchesKeyword(prop.Name))
                        continue;

                    object value = prop.GetValue(instance);
                    if (value != null)
                        return value;
                }
            }
            catch
            {
                // تجاهل
            }

            try
            {
                foreach (FieldInfo field in type.GetFields(All))
                {
                    if (!typeof(IList).IsAssignableFrom(field.FieldType))
                        continue;
                    if (!MatchesKeyword(field.Name))
                        continue;

                    object value = field.GetValue(instance);
                    if (value != null)
                        return value;
                }
            }
            catch
            {
                // تجاهل
            }

            return null;
        }

        #endregion
    }
}
