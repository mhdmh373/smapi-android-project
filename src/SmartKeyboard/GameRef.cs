using System;
using System.Linq;
using System.Reflection;

namespace SmartKeyboard
{
    /// <summary>
    /// كل الوصول للعبة عبر Reflection فقط، وبأسماء موثقة من مصدر SMAPI-Android-1.6:
    /// Game1.input (كما في MobileInputTool) وGame1.graphics.GraphicsDevice
    /// (كما في SCore/AssetDataForImage) وGame1.viewport (كما في SInputState)
    /// وGame1.dialogueFont (كما في CoreAssetPropagator).
    /// لا مرجع ترجمة لأي تجميع لعبة.
    /// </summary>
    internal static class GameRef
    {
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static
            | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

        private static bool searchedGame1;
        private static Type game1Type;

        private static MethodInfo overrideButtonMethod;
        private static PropertyInfo inputProp;

        internal static Type Game1Type
        {
            get
            {
                if (searchedGame1)
                    return game1Type;
                searchedGame1 = true;
                try
                {
                    game1Type = Type.GetType("StardewValley.Game1, Stardew Valley");
                }
                catch { }
                if (game1Type == null)
                {
                    try
                    {
                        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            Type found = null;
                            try { found = asm.GetType("StardewValley.Game1"); } catch { }
                            if (found != null)
                            {
                                game1Type = found;
                                break;
                            }
                        }
                    }
                    catch { }
                }
                return game1Type;
            }
        }

        /// <summary>يحقن ضغطة/تحريرًا في حالة الإدخال كما تراها المودات (SInputState.OverrideButton).</summary>
        internal static bool OverrideButton(object inputState, object sButton, bool down)
        {
            try
            {
                if (inputState == null)
                    return false;
                if (overrideButtonMethod == null || overrideButtonMethod.DeclaringType != inputState.GetType())
                {
                    overrideButtonMethod = inputState.GetType().GetMethod(
                        "OverrideButton",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }
                if (overrideButtonMethod == null)
                    return false;
                overrideButtonMethod.Invoke(inputState, new object[] { sButton, down });
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static object GetInputState()
        {
            try
            {
                Type t = Game1Type;
                if (t == null)
                    return null;
                if (inputProp == null || inputProp.DeclaringType != t)
                    inputProp = t.GetProperty("input", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                return inputProp?.GetValue(null);
            }
            catch
            {
                return null;
            }
        }

        internal static object GetStaticMember(string name)
        {
            try
            {
                Type t = Game1Type;
                if (t == null)
                    return null;
                PropertyInfo p = t.GetProperty(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null)
                    return p.GetValue(null);
                FieldInfo f = t.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null)
                    return f.GetValue(null);
            }
            catch { }
            return null;
        }

        /// <summary>أبعاد الشاشة (العرض والارتفاع) من Game1.viewport.</summary>
        internal static bool TryGetViewport(out int width, out int height)
        {
            width = 1280;
            height = 720;
            try
            {
                object vp = GetStaticMember("viewport");
                if (vp == null)
                    return false;
                Type t = vp.GetType();
                PropertyInfo w = t.GetProperty("Width");
                PropertyInfo h = t.GetProperty("Height");
                if (w == null || h == null)
                    return false;
                width = Convert.ToInt32(w.GetValue(vp));
                height = Convert.ToInt32(h.GetValue(vp));
                return width > 0 && height > 0;
            }
            catch
            {
                return false;
            }
        }

        internal static object GetGraphicsDevice()
        {
            try
            {
                object gfx = GetStaticMember("graphics");
                if (gfx == null)
                    return null;
                Type t = gfx.GetType();
                PropertyInfo p = t.GetProperty("GraphicsDevice", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null)
                    return p.GetValue(gfx);
                FieldInfo f = t.GetField("GraphicsDevice", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return f?.GetValue(gfx);
            }
            catch
            {
                return null;
            }
        }

        internal static float GetFieldFloat(object obj, string fieldName, float fallback)
        {
            try
            {
                if (obj == null)
                    return fallback;
                Type t = obj.GetType();
                FieldInfo f = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null)
                    return Convert.ToSingle(f.GetValue(obj));
                PropertyInfo p = t.GetProperty(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null)
                    return Convert.ToSingle(p.GetValue(obj));
            }
            catch { }
            return fallback;
        }

        internal static object Invoke(object target, string methodName, object[] args, Type[] sig)
        {
            try
            {
                if (target == null)
                    return null;
                MethodInfo m = sig != null
                    ? target.GetType().GetMethod(methodName, Any, null, sig, null)
                    : target.GetType().GetMethod(methodName, Any);
                return m?.Invoke(target, args);
            }
            catch
            {
                return null;
            }
        }
    }
}
