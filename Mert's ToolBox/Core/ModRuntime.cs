using System;
using System.Linq;
using System.Reflection;
using Colossal.Logging;

namespace MertsToolBox.Core
{
    internal static class ModRuntime
    {
        public static ILog log = LogManager.GetLogger("MertsToolBox").SetShowsErrorsInUI(false);

        public static void Log(string message)
        {
            log.Info(message);
        }

        public static void Warn(string message)
        {
            log.Warn(message);
        }

        public static void Error(string message)
        {
            log.Error(message);
        }

        public static FieldInfo GetFieldRecursive(Type type, string fieldName)
        {
            while (type != null)
            {
                FieldInfo fi = type.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                if (fi != null)
                    return fi;

                type = type.BaseType;
            }

            return null;
        }

        public static object GetFieldValue(object target, string fieldName)
        {
            if (target == null)
                return null;

            FieldInfo fi = GetFieldRecursive(target.GetType(), fieldName);
            if (fi == null)
                return null;

            return fi.GetValue(target);
        }

        public static bool TrySetField(object target, string fieldName, object value)
        {
            if (target == null)
                return false;

            FieldInfo fi = GetFieldRecursive(target.GetType(), fieldName);
            if (fi == null)
                return false;

            fi.SetValue(target, value);
            return true;
        }

        public static object TryInvokeParameterless(object target, string methodName)
        {
            if (target == null)
                return null;

            Type t = target.GetType();
            while (t != null)
            {
                MethodInfo mi = t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == 0);

                if (mi != null)
                    return mi.Invoke(target, null);

                t = t.BaseType;
            }

            return null;
        }
        public static bool TryGetFieldValue<T>(object obj, string fieldName, out T value)
        {
            value = default;

            if (obj == null)
                return false;

            try
            {
                var field = obj.GetType().GetField(fieldName,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public);

                if (field == null)
                    return false;

                object raw = field.GetValue(obj);

                if (raw is T cast)
                {
                    value = cast;
                    return true;
                }

                if (raw != null)
                {
                    value = (T)raw;
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }
    }
}