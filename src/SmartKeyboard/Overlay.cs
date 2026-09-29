using System;
using System.Collections.Generic;
using System.Reflection;

namespace SmartKeyboard
{
    /// <summary>
    /// رسم الأزرار العائمة عبر Reflection فقط. كل أنواع MonoGame (Color وRectangle
    /// وVector2 وTexture2D وSpriteBatch وSpriteFont) تُحل وقت التشغيل من الكائنات
    /// نفسها، فلا يوجد أي مرجع ترجمة لها.
    /// </summary>
    internal class Overlay
    {
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static
            | BindingFlags.Public | BindingFlags.NonPublic;

        private object pixelTexture;
        private Type lastBatchType;
        private MethodInfo drawMethod;
        private MethodInfo drawStringMethod;

        internal struct Rect
        {
            public int X;
            public int Y;
            public int W;
            public int H;
            public bool Contains(float x, float y)
            {
                return x >= X && x < X + W && y >= Y && y < Y + H;
            }
        }

        internal Rect ToggleRect;
        internal List<Rect> KeyRects = new List<Rect>();
        internal List<string> KeyLabels = new List<string>();

        /// <summary>يعيد حساب مواضع الأزرار حسب أبعاد الشاشة الحالية.</summary>
        internal void Layout(int viewW, int viewH, int size, IList<string> labels, bool panelOpen)
        {
            int gap = 8;
            int tx = (int)(viewW * 0.955f) - size;
            int ty = (int)(viewH * 0.62f);
            if (tx < 0) tx = 0;
            if (ty < 0) ty = 0;
            ToggleRect = new Rect { X = tx, Y = ty, W = size, H = size };
            KeyRects.Clear();
            KeyLabels.Clear();
            if (!panelOpen)
                return;
            int y = ty + size + gap;
            foreach (string label in labels)
            {
                if (y + size > viewH)
                    break;
                KeyRects.Add(new Rect { X = tx, Y = y, W = size, H = size });
                KeyLabels.Add(label);
                y += size + gap;
            }
        }

        internal int HitKey(float x, float y)
        {
            for (int i = 0; i < KeyRects.Count; i++)
            {
                if (KeyRects[i].Contains(x, y))
                    return i;
            }
            return -1;
        }

        /// <summary>يرسم الزر العائم ولوحة المفاتيح. يعيد false إن تعذر الرسم (تُتجاهل الدعوة).</summary>
        internal bool Draw(object spriteBatch, object font, float opacity, int heldIndex)
        {
            try
            {
                if (spriteBatch == null || font == null)
                    return false;
                Type batchType = spriteBatch.GetType();
                if (batchType != lastBatchType)
                {
                    lastBatchType = batchType;
                    drawMethod = null;
                    drawStringMethod = null;
                    pixelTexture = null;
                }
                Assembly mono = batchType.Assembly;
                Type colorType = mono.GetType("Microsoft.Xna.Framework.Color");
                Type rectType = mono.GetType("Microsoft.Xna.Framework.Rectangle");
                Type vec2Type = mono.GetType("Microsoft.Xna.Framework.Vector2");
                Type texType = mono.GetType("Microsoft.Xna.Framework.Graphics.Texture2D");
                if (colorType == null || rectType == null || vec2Type == null || texType == null)
                    return false;

                if (pixelTexture == null)
                {
                    object device = GameRef.GetGraphicsDevice();
                    if (device == null)
                        return false;
                    pixelTexture = Activator.CreateInstance(texType, new object[] { device, 1, 1 });
                    MethodInfo setData = null;
                    foreach (MethodInfo m in texType.GetMethods(Any))
                    {
                        if (m.Name != "SetData" || !m.IsGenericMethodDefinition)
                            continue;
                        ParameterInfo[] ps = m.GetParameters();
                        if (ps.Length == 1 && ps[0].ParameterType.IsArray)
                        {
                            setData = m;
                            break;
                        }
                    }
                    if (setData == null)
                        return false;
                    object white = colorType.GetProperty("White", BindingFlags.Static | BindingFlags.Public)?.GetValue(null);
                    Array arr = Array.CreateInstance(colorType, 1);
                    arr.SetValue(white, 0);
                    setData.MakeGenericMethod(colorType).Invoke(pixelTexture, new object[] { arr });
                }

                if (drawMethod == null)
                    drawMethod = batchType.GetMethod("Draw", new Type[] { texType, rectType, colorType });
                if (drawStringMethod == null)
                {
                    Type fontType = font.GetType();
                    drawStringMethod = batchType.GetMethod("DrawString", new Type[] { fontType, typeof(string), vec2Type, colorType });
                }
                if (drawMethod == null || drawStringMethod == null)
                    return false;

                object whiteColor = colorType.GetProperty("White", BindingFlags.Static | BindingFlags.Public)?.GetValue(null);
                object dimColor = MultiplyColor(colorType, whiteColor, opacity);
                object holdColor = MultiplyColor(colorType, whiteColor, 1.0f);

                // زر التبديل (أغمق قليلًا) ثم المفاتيح
                FillRect(spriteBatch, rectType, colorType, ToggleRect, dimColor);
                Text(spriteBatch, font, vec2Type, colorType, "KB", ToggleRect, holdColor);
                for (int i = 0; i < KeyRects.Count; i++)
                {
                    FillRect(spriteBatch, rectType, colorType, KeyRects[i], i == heldIndex ? holdColor : dimColor);
                    Text(spriteBatch, font, vec2Type, colorType, ShortLabel(KeyLabels[i]), KeyRects[i], holdColor);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void FillRect(object sb, Type rectType, Type colorType, Rect r, object color)
        {
            object rect = Activator.CreateInstance(rectType, new object[] { r.X, r.Y, r.W, r.H });
            drawMethod.Invoke(sb, new object[] { pixelTexture, rect, color });
        }

        private void Text(object sb, object font, Type vec2Type, Type colorType, string text, Rect r, object color)
        {
            try
            {
                object size = GameRef.Invoke(font, "MeasureString", new object[] { text }, new Type[] { typeof(string) });
                float tw = size != null ? GameRef.GetFieldFloat(size, "X", 0) : 0;
                float th = size != null ? GameRef.GetFieldFloat(size, "Y", 0) : 0;
                float x = r.X + Math.Max(0, (r.W - tw) / 2);
                float y = r.Y + Math.Max(0, (r.H - th) / 2);
                object pos = Activator.CreateInstance(vec2Type, new object[] { x, y });
                drawStringMethod.Invoke(sb, new object[] { font, text, pos, color });
            }
            catch { }
        }

        private static object MultiplyColor(Type colorType, object color, float factor)
        {
            try
            {
                MethodInfo op = colorType.GetMethod("op_Multiply", new Type[] { colorType, typeof(float) });
                if (op != null)
                    return op.Invoke(null, new object[] { color, factor });
            }
            catch { }
            return color;
        }

        private static string ShortLabel(string key)
        {
            if (string.IsNullOrEmpty(key))
                return "?";
            // "LeftShift+F5" -> "S+F5"، أسماء طويلة تُختصر لأول حرفين
            string[] parts = key.Split('+');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 3)
                    parts[i] = parts[i].Substring(0, 2);
            }
            return string.Join("+", parts);
        }
    }
}
