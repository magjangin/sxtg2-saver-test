using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MelonLoader;

namespace sxtg2_saver_test.Helpers
{
    public static class ReflectionUtils
    {
        private const BindingFlags AllMembers =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        /// <summary>
        /// 이름으로 타입을 찾는다. Type.GetType 은 다른 어셈블리의 타입을 어셈블리 한정 이름 없이는
        /// 찾지 못하므로(Assembly-CSharp 타입은 항상 null), 로드된 어셈블리 전체를 뒤진다.
        /// 전체 이름과 짧은 이름을 모두 받아들이고, 못 찾아도 로그를 남기지 않는다
        /// (후보를 순회하는 용도라 실패가 정상 흐름이다).
        /// </summary>
        public static Type FindType(params string[] typeNames)
        {
            foreach (var name in typeNames)
            {
                if (string.IsNullOrEmpty(name)) continue;

                Type direct = Type.GetType(name, throwOnError: false);
                if (direct != null) return direct;

                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try
                    {
                        types = asm.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        // 스트립된 어셈블리에서는 일부 타입만 로드된다.
                        types = ex.Types.Where(t => t != null).ToArray();
                    }
                    catch
                    {
                        continue;
                    }

                    Type hit = types.FirstOrDefault(t => t.FullName == name)
                            ?? types.FirstOrDefault(t => t.Name == name);
                    if (hit != null) return hit;
                }
            }
            return null;
        }

        public static MethodInfo FindMethod(Type[] candidateTypes, string[] candidateMethodNames, Type[] parameterTypes = null)
        {
            foreach (var type in candidateTypes)
            {
                if (type == null) continue;
                foreach (var methodName in candidateMethodNames)
                {
                    try
                    {
                        MethodInfo method = GetMethodSafe(type, methodName, parameterTypes);
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

            // 어느 패치가 대상을 못 찾았는지 알 수 있도록 후보 이름을 같이 남긴다.
            string types = string.Join("/", candidateTypes.Where(t => t != null).Select(t => t.Name).ToArray());
            string names = string.Join("/", candidateMethodNames);
            MelonLogger.Warning($"[ReflectionUtils] 후보 메서드를 찾지 못함: [{names}] in [{(types.Length == 0 ? "<타입 없음>" : types)}]");
            return null;
        }

        /// <summary>
        /// GetMethod 는 오버로드가 둘 이상이면 AmbiguousMatchException 을 던진다.
        /// 게임 업데이트로 오버로드가 늘어났다고 해서 패치가 통째로 사라지면 안 되므로,
        /// 모호한 경우 파라미터가 가장 적은 시그니처를 고른다(원본일 확률이 높다).
        /// </summary>
        private static MethodInfo GetMethodSafe(Type type, string methodName, Type[] parameterTypes)
        {
            if (parameterTypes != null)
            {
                return type.GetMethod(methodName, AllMembers, null, parameterTypes, null);
            }

            try
            {
                return type.GetMethod(methodName, AllMembers);
            }
            catch (AmbiguousMatchException)
            {
                MethodInfo[] overloads = type.GetMethods(AllMembers)
                    .Where(m => m.Name == methodName)
                    .OrderBy(m => m.GetParameters().Length)
                    .ToArray();

                if (overloads.Length == 0) return null;

                MethodInfo picked = overloads[0];
                string sig = string.Join(", ", picked.GetParameters().Select(p => p.ParameterType.Name).ToArray());
                MelonLogger.Warning(
                    $"[ReflectionUtils] '{type.Name}.{methodName}' 오버로드 {overloads.Length}개 발견. " +
                    $"파라미터가 가장 적은 시그니처 선택: ({sig})");
                return picked;
            }
        }

        public static T GetFieldValue<T>(object instance, Type targetType, params string[] fieldNames)
        {
            Type type = targetType ?? instance?.GetType();
            if (type == null) return default;

            foreach (var name in fieldNames)
            {
                FieldInfo field = type.GetField(name, AllMembers);
                if (field != null)
                {
                    try
                    {
                        return (T)field.GetValue(instance);
                    }
                    catch { }
                }

                PropertyInfo prop = type.GetProperty(name, AllMembers);
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
                FieldInfo field = type.GetField(name, AllMembers);
                if (field != null)
                {
                    try
                    {
                        field.SetValue(instance, value);
                        return true;
                    }
                    catch { }
                }

                PropertyInfo prop = type.GetProperty(name, AllMembers);
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

            MethodInfo method = null;
            try
            {
                method = GetMethodSafe(type, methodName, null);
            }
            catch { }

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
