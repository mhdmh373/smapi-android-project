using StardewModdingAPI;

namespace BigInventory
{
    /// <summary>إعدادات المود: حجم الحقيبة بال multiples of 12.</summary>
    internal class ModConfig
    {
        /// <summary>العدد المطلوب من الخانات (من 12 إلى 120، مقرّب لأعلى مضاعف لـ 12).</summary>
        public int InventorySize { get; set; } = 36;

        /// <summary>السماح بالتصغير حتى لو كانت هناك عناصر لا تسع في العدد المطلوب.</summary>
        public bool AllowShrinkWithItems { get; set; } = true;
    }
}
