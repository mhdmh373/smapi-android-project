using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace SmartKeyboard
{
    /// <summary>
    /// زر كيبورد عائم قرب أزرار القائمة: ضغط وإمساك وتحرير حقيقي كما تراه المودات
    /// (SButton عبر SInputState.OverrideButton)، مع دعم Shift/Ctrl/Alt.
    /// اللمسات داخل أزرارنا فقط هي ما يُكتم (Suppress) فلا يتحرك اللاعب بالخطأ.
    /// </summary>
    internal class ModEntry : Mod
    {
        private ModConfig config;
        private readonly Overlay overlay = new Overlay();
        private readonly List<ParsedKey> keys = new List<ParsedKey>();

        private bool panelOpen;
        private ParsedKey heldKey;
        private bool layoutDone;

        // اختبار ذاتي: نتذكر آخر مفتاح حقنّاه، وعندما يعيده SMAPI إلينا
        // كحدث ButtonPressed/Released نطبع سطرًا في السجل (إثبات الذهاب والإياب).
        private SButton? expectPress;
        private SButton? expectRelease;
        private bool pressSeen;

        private struct ParsedKey
        {
            public string Label;
            public SButton[] Modifiers;
            public SButton Main;
        }

        public override void Entry(IModHelper helper)
        {
            config = helper.ReadConfig<ModConfig>() ?? new ModConfig();
            helper.WriteConfig(config);
            ParseKeys();

            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.Input.ButtonReleased += OnButtonReleased;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Display.Rendered += OnRendered;

            helper.ConsoleCommands.Add(
                "smartkeyboard",
                "smartkeyboard [show|hide|toggle] - show or hide the floating keyboard button.",
                OnConsoleCommand);
        }

        private void ParseKeys()
        {
            keys.Clear();
            if (config.Keys == null)
                return;
            foreach (string raw in config.Keys)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    continue;
                try
                {
                    string[] parts = raw.Split('+');
                    List<SButton> mods = new List<SButton>();
                    for (int i = 0; i < parts.Length - 1; i++)
                        mods.Add((SButton)Enum.Parse(typeof(SButton), parts[i].Trim(), true));
                    SButton main = (SButton)Enum.Parse(typeof(SButton), parts[parts.Length - 1].Trim(), true);
                    keys.Add(new ParsedKey { Label = raw.Trim(), Modifiers = mods.ToArray(), Main = main });
                }
                catch
                {
                    Monitor.Log($"Smart Keyboard: ignored unknown key '{raw}'. Use SButton names like F1 or LeftShift+F5.", LogLevel.Warn);
                }
            }
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            try
            {
                // اختبار ذاتي: هل أعاد SMAPI مفتاحنا المحقون إلينا؟
                if (e.Button != SButton.MouseLeft)
                {
                    if (expectPress.HasValue && e.Button == expectPress.Value && !pressSeen)
                    {
                        pressSeen = true;
                        Monitor.Log($"Smart Keyboard self-test: SMAPI delivered {e.Button} press (injection works).", LogLevel.Info);
                    }
                    return;
                }
                if (!TryCursor(e, out float x, out float y))
                    return;
                EnsureLayout();
                if (overlay.ToggleRect.Contains(x, y))
                {
                    panelOpen = !panelOpen;
                    layoutDone = false;
                    Helper.Input.Suppress(SButton.MouseLeft);
                    return;
                }
                int hit = overlay.HitKey(x, y);
                if (hit >= 0 && hit < keys.Count)
                {
                    heldKey = keys[hit];
                    Inject(heldKey, true);
                    Helper.Input.Suppress(SButton.MouseLeft);
                }
            }
            catch { }
        }

        private void OnButtonReleased(object sender, ButtonReleasedEventArgs e)
        {
            try
            {
                // اختبار ذاتي: وصول التحرير يعني دورة ضغط/إمساك/تحرير كاملة.
                if (e.Button != SButton.MouseLeft)
                {
                    if (expectRelease.HasValue && e.Button == expectRelease.Value)
                    {
                        expectRelease = null;
                        Monitor.Log($"Smart Keyboard self-test: SMAPI delivered {e.Button} release (full cycle works).", LogLevel.Info);
                    }
                    return;
                }
                if (heldKey.Label != null)
                {
                    Inject(heldKey, false);
                    heldKey = default(ParsedKey);
                }
            }
            catch { }
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            try
            {
                // الإمساك: نعيد الحقن كل إطار ليبقى الزر مضغوطًا (تراه المودات IsDown)
                if (heldKey.Label != null)
                    Inject(heldKey, true);
            }
            catch { }
        }

        private void OnRendered(object sender, RenderedEventArgs e)
        {
            try
            {
                object font = GameRef.GetStaticMember("dialogueFont");
                if (font == null)
                    return;
                EnsureLayout();
                object batch = null;
                try { batch = e.GetType().GetProperty("SpriteBatch")?.GetValue(e); } catch { }
                if (batch == null)
                    return;
                List<string> labels = new List<string>();
                foreach (ParsedKey k in keys)
                    labels.Add(k.Label);
                if (GameRef.TryGetViewport(out int w, out int h))
                    overlay.Layout(w, h, config.ButtonSize, labels, panelOpen);
                int heldIndex = -1;
                if (heldKey.Label != null)
                {
                    for (int i = 0; i < keys.Count; i++)
                    {
                        if (keys[i].Label == heldKey.Label)
                        {
                            heldIndex = i;
                            break;
                        }
                    }
                }
                overlay.Draw(batch, font, config.Opacity, heldIndex);
            }
            catch { }
        }

        private void EnsureLayout()
        {
            if (layoutDone)
                return;
            layoutDone = true;
        }

        /// <summary>ضغط (true) أو تحرير (false) لمفتاح مع معدِّلاته.</summary>
        private void Inject(ParsedKey key, bool down)
        {
            object state = GameRef.GetInputState();
            if (state == null)
                return;
            if (down)
            {
                foreach (SButton m in key.Modifiers)
                    GameRef.OverrideButton(state, m, true);
                GameRef.OverrideButton(state, key.Main, true);
                expectPress = key.Main;
                pressSeen = false;
            }
            else
            {
                GameRef.OverrideButton(state, key.Main, false);
                for (int i = key.Modifiers.Length - 1; i >= 0; i--)
                    GameRef.OverrideButton(state, key.Modifiers[i], false);
                expectPress = null;
                expectRelease = key.Main;
            }
        }

        /// <summary>يقرأ موضع المؤشر عبر Reflection (لتجنب مرجع MonoGame وقت الترجمة).</summary>
        private static bool TryCursor(ButtonPressedEventArgs e, out float x, out float y)
        {
            x = 0;
            y = 0;
            try
            {
                object cursor = e.GetType().GetProperty("Cursor")?.GetValue(e);
                if (cursor == null)
                    return false;
                object pos = GameRef.Invoke(cursor, "GetScaledScreenPixels", new object[0], Type.EmptyTypes);
                if (pos == null)
                    return false;
                x = GameRef.GetFieldFloat(pos, "X", 0);
                y = GameRef.GetFieldFloat(pos, "Y", 0);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void OnConsoleCommand(string name, string[] args)
        {
            string sub = args != null && args.Length > 0 ? args[0].ToLowerInvariant() : "toggle";
            if (sub == "show")
                panelOpen = true;
            else if (sub == "hide")
                panelOpen = false;
            else
                panelOpen = !panelOpen;
            layoutDone = false;
            Monitor.Log($"Smart Keyboard: panel {(panelOpen ? "shown" : "hidden")}.", LogLevel.Info);
        }
    }
}
