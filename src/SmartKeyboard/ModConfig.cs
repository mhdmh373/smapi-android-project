namespace SmartKeyboard
{
    /// <summary>إعدادات مود الكيبورد العائم.</summary>
    internal class ModConfig
    {
        /// <summary>موضع زر التبديل كنسبة من أبعاد الشاشة (0-1). الافتراضي: يمين الشاشة تحت زر القائمة.</summary>
        public float ToggleX { get; set; } = 0.955f;

        /// <summary>موضع زر التبديل كنسبة من أبعاد الشاشة (0-1).</summary>
        public float ToggleY { get; set; } = 0.62f;

        /// <summary>حجم الزر بالبكسل.</summary>
        public int ButtonSize { get; set; } = 90;

        /// <summary>الشفافية (0-1).</summary>
        public float Opacity { get; set; } = 0.75f;

        /// <summary>
        /// المفاتيح المعروضة. كل عنصر اسم SButton (مثل F1) أو تركيبة بمعدِّلات
        /// مثل LeftShift+F5. الأسماء تُطابق تعداد SButton في SMAPI.
        /// </summary>
        public string[] Keys { get; set; } = new[] { "F1", "F5", "F9", "LeftShift", "LeftControl", "LeftAlt" };
    }
}
