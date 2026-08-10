using System;
using System.Reflection;
using MelonLoader;

namespace sxtg2_saver_test.Helpers
{
    public static class ReflectionUtils
    {
        public static MethodInfo FindMethod(Type[] candidateTypes, string[] candidateMethodNames, Type[] parameterTypes = null)
        {
            foreach (var type in candidateTypes)
            {
                if (type == null) continue;
                foreach (var methodName in candidateMethodNames)
                {
                    try
                    {
                        MethodInfo method = null;
                        if (parameterTypes != null)
                        {
                            method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance, null, parameterTypes, null);
                        }
                        else
                        {
                            method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                        }

                        if (method != null)
                        {
                            MelonLogger.Msg($"[ReflectionUtils] Resolved method '{type.FullName}.{method.Name}'");
                            return method;
                        }
                    }
                    catch
                    {
                        // Fallback to next candidate
                    }
                }
            }
            MelonLogger.Warning($"[ReflectionUtils] Could not find any method matching specified candidates.");
            return null;
        }

        public static T GetFieldValue<T>(object instance, Type targetType, params string[] fieldNames)
        {
            Type type = targetType ?? instance?.GetType();
            if (type == null) return default;

            foreach (var name in fieldNames)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                if (field != null)
                {
                    try
                    {
                        return (T)field.GetValue(instance);
                    }
                    catch { }
                }

                PropertyInfo prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                if (prop != null && prop.CanRead)
                {
                    try
                    {
                        return (T)prop.GetValue(instance, null);
                    }
                    catch { }
                }
            }
            return default;
        }

        public static bool SetFieldValue(object instance, Type targetType, object value, params string[] fieldNames)
        {
            Type type = targetType ?? instance?.GetType();
            if (type == null) return false;

            foreach (var name in fieldNames)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                if (field != null)
                {
                    try
                    {
                        field.SetValue(instance, value);
                        return true;
                    }
                    catch { }
                }

                PropertyInfo prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                if (prop != null && prop.CanWrite)
                {
                    try
                    {
                        prop.SetValue(instance, value, null);
                        return true;
                    }
                    catch { }
                }
            }
            return false;
        }

        public static bool InvokeMethod(object instance, Type targetType, string methodName, object[] parameters = null)
        {
            Type type = targetType ?? instance?.GetType();
            if (type == null) return false;

            MethodInfo method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            if (method != null)
            {
                try
                {
                    method.Invoke(instance, parameters);
                    return true;
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[ReflectionUtils] Failed to invoke {type.Name}.{methodName}: {ex}");
                }
            }
            return false;
        }
    }
}
