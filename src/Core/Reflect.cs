using System;
using System.Reflection;
using HarmonyLib;

namespace SimpleWSO.Core
{
    /// <summary>
    /// Thin wrappers over Harmony AccessTools for reaching the game's private members.
    /// Cached MemberInfo so the hot paths (per-frame turret access) stay cheap.
    /// </summary>
    public static class Reflect
    {
        public static void SetField(object instance, string name, object value)
        {
            var f = AccessTools.Field(instance.GetType(), name);
            if (f == null) throw new MissingFieldException(instance.GetType().FullName, name);
            f.SetValue(instance, value);
        }

        public static bool TryGetField<T>(object instance, string name, out T value)
        {
            value = default;
            if (instance == null) return false;
            var f = AccessTools.Field(instance.GetType(), name);
            if (f == null) return false;
            value = (T)f.GetValue(instance);
            return true;
        }

        public static object Call(object instance, string name, params object[] args)
        {
            var m = AccessTools.Method(instance.GetType(), name);
            if (m == null) throw new MissingMethodException(instance.GetType().FullName, name);
            return m.Invoke(instance, args);
        }

        public static object Call(object instance, string name, Type[] parameterTypes, params object[] args)
        {
            var m = AccessTools.Method(instance.GetType(), name, parameterTypes);
            if (m == null) throw new MissingMethodException(instance.GetType().FullName, name);
            return m.Invoke(instance, args);
        }
    }
}
