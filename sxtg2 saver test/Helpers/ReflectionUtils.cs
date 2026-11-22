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

        // 상속 계층을 직접 걸어 올라가며 찾을 때 쓴다 (부모의 private 필드는 GetField 로 보이지 않는다).
        private const BindingFlags DeclaredMembers = AllMembers | BindingFlags.DeclaredOnly;

        // FindType 은 패치 Prefix 안(= 런타임 핫패스)에서도 불리므로 결과를 캐시한다.
        private static readonly Dictionary<string, Type> TypeCache = new Dictionary<string, Type>();
        private static readonly Dictionary<Assembly, Type[]> AssemblyTypeCache = new Dictionary<Assembly, Type[]>();
        private static int _knownAssemblyCount;

        // 같은 경고가 프레임마다 쏟아지지 않도록 키별로 한 번만 남긴다.
        private static readonly HashSet<string> WarnedKeys = new HashSet<string>();

        /// <summary>
        /// 이름으로 타입을 찾는다. Type.GetType 은 다른 어셈블리의 타입을 어셈블리 한정 이름 없이는
        /// 찾지 못하므로(Assembly-CSharp 타입은 항상 null), 로드된 어셈블리 전체를 뒤진다.
        /// 전체 이름과 짧은 이름을 모두 받아들이고, 못 찾아도 로그를 남기지 않는다
        /// (후보를 순회하는 용도라 실패가 정상 흐름이다).
        ///
        /// 검색 순서: Assembly-CSharp 우선 → 전체 이름 일치 → 짧은 이름 일치.
        /// 짧은 이름이 다른 어셈블리(Unity 패키지 등)의 동명 타입에 잘못 걸리는 것을 막기 위해서다.
        /// 결과(못 찾은 경우 포함)는 캐시하며, 새 어셈블리가 로드되면 "못 찾음" 캐시만 비운다.
        /// </summary>
        public static Type FindType(params string[] typeNames)
        {
            foreach (var name in typeNames)
            {
                if (string.IsNullOrEmpty(name)) continue;

                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                if (assemblies.Length != _knownAssemblyCount)
                {
                    _knownAssemblyCount = assemblies.Length;
                    foreach (string stale in TypeCache.Where(kv => kv.Value == null).Select(kv => kv.Key).ToArray())
                    {
                        TypeCache.Remove(stale);
                    }
                }

                Type cached;
                if (!TypeCache.TryGetValue(name, out cached))
                {
                    cached = ResolveType(name, assemblies);
                    TypeCache[name] = cached;
                }
                if (cached != null) return cached;
            }
            return null;
        }

        private static Type ResolveType(string name, Assembly[] assemblies)
        {
            Type direct = null;
            try { direct = Type.GetType(name, throwOnError: false); } catch { }
            if (direct != null) return direct;

            Assembly[] ordered = assemblies
                .Where(a => !a.IsDynamic)
                .OrderBy(a => a.GetName().Name == "Assembly-CSharp" ? 0 : 1)
                .ToArray();

            foreach (Assembly asm in ordered)
            {
                Type hit = SafeGetTypes(asm).FirstOrDefault(t => t.FullName == name);
                if (hit != null) return hit;
            }
            foreach (Assembly asm in ordered)
            {
                Type hit = SafeGetTypes(asm).FirstOrDefault(t => t.Name == name);
                if (hit != null) return hit;
            }
            return null;
        }

        private static Type[] SafeGetTypes(Assembly asm)
        {
            Type[] types;
            if (AssemblyTypeCache.TryGetValue(asm, out types)) return types;

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
                types = new Type[0];
            }
            AssemblyTypeCache[asm] = types;
            return types;
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

        /// <summary>
        /// 호출 인자에 맞는 오버로드를 고른다. 인자 개수가 같고 각 인자가 대입 가능한 첫 메서드를 쓰고,
        /// 없으면 GetMethodSafe(파라미터 최소 오버로드)로 되돌아간다.
        /// </summary>
        private static MethodInfo ResolveInvokable(Type type, string methodName, object[] args)
        {
            int count = args == null ? 0 : args.Length;

            for (Type t = type; t != null; t = t.BaseType)
            {
                foreach (MethodInfo m in t.GetMethods(DeclaredMembers).Where(m => m.Name == methodName))
                {
                    ParameterInfo[] ps = m.GetParameters();
                    if (ps.Length != count) continue;

                    bool compatible = true;
                    for (int i = 0; i < count; i++)
                    {
                        object arg = args[i];
                        Type pt = ps[i].ParameterType;
                        if (arg == null ? pt.IsValueType : !pt.IsInstanceOfType(arg))
                        {
                            compatible = false;
                            break;
                        }
                    }
                    if (compatible) return m;
                }
            }

            return GetMethodSafe(type, methodName, null);
        }

        private static FieldInfo FindField(Type type, string name)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                FieldInfo f = t.GetField(name, DeclaredMembers);
                if (f != null) return f;
            }
            return null;
        }

        private static PropertyInfo FindProperty(Type type, string name)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                PropertyInfo p = t.GetProperty(name, DeclaredMembers);
                if (p != null) return p;
            }
            return null;
        }

        /// <summary>같은 키의 경고는 세션당 한 번만 출력한다.</summary>
        public static void WarnOnce(string key, string message)
        {
            if (WarnedKeys.Add(key))
            {
                MelonLogger.Warning(message);
            }
        }

        public static T GetFieldValue<T>(object instance, Type targetType, params string[] fieldNames)
        {
            Type type = targetType ?? instance?.GetType();
            if (type == null) return default;

            foreach (var name in fieldNames)
            {
                FieldInfo field = FindField(type, name);
                if (field != null)
                {
                    try
                    {
                        return (T)field.GetValue(instance);
                    }
                    catch (Exception ex)
                    {
                        WarnOnce("get:" + type.FullName + "." + name,
                            $"[ReflectionUtils] 필드 읽기 실패 {type.Name}.{name}: {ex.GetType().Name}");
                    }
                }

                PropertyInfo prop = FindProperty(type, name);
                if (prop != null && prop.CanRead)
                {
                    try
                    {
                        return (T)prop.GetValue(instance, null);
                    }
                    catch (Exception ex)
                    {
                        WarnOnce("getp:" + type.FullName + "." + name,
                            $"[ReflectionUtils] 프로퍼티 읽기 실패 {type.Name}.{name}: {ex.GetType().Name}");
                    }
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
                FieldInfo field = FindField(type, name);
                if (field != null)
                {
                    try
                    {
                        field.SetValue(instance, value);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        WarnOnce("set:" + type.FullName + "." + name,
                            $"[ReflectionUtils] 필드 쓰기 실패 {type.Name}.{name}: {ex.GetType().Name}");
                    }
                }

                PropertyInfo prop = FindProperty(type, name);
                if (prop != null && prop.CanWrite)
                {
                    try
                    {
                        prop.SetValue(instance, value, null);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        WarnOnce("setp:" + type.FullName + "." + name,
                            $"[ReflectionUtils] 프로퍼티 쓰기 실패 {type.Name}.{name}: {ex.GetType().Name}");
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 정적 캐시 필드에 값을 넣는다. 이름이 같아도 타입이 다른 엉뚱한 필드를 덮어쓰지 않도록
        /// 필드 타입이 값과 호환될 때만 쓴다.
        /// </summary>
        public static bool TrySetStaticCache(Type owner, object value, params string[] fieldNames)
        {
            if (owner == null || value == null) return false;

            foreach (var name in fieldNames)
            {
                FieldInfo field = FindField(owner, name);
                if (field == null || !field.IsStatic) continue;
                if (!field.FieldType.IsInstanceOfType(value)) continue;

                try
                {
                    field.SetValue(null, value);
                    return true;
                }
                catch (Exception ex)
                {
                    WarnOnce("cache:" + owner.FullName + "." + name,
                        $"[ReflectionUtils] 정적 캐시 쓰기 실패 {owner.Name}.{name}: {ex.GetType().Name}");
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
                method = ResolveInvokable(type, methodName, parameters);
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
