using System;
using System.Linq;
using System.Reflection;
using MelonLoader;

namespace sxtg2_saver_test.Helpers
{
    /// <summary>패치 클래스들이 공유하는 작은 도우미들.</summary>
    public static class PatchHelpers
    {
        /// <summary>
        /// 서버 콜백(Action&lt;T&gt; 계열)을 "빈 응답"으로 호출한다.
        /// 콜백의 제네릭 인자 타입 T 를 런타임에 읽어서 기본 생성한 객체를 넘기므로,
        /// 게임 업데이트로 응답 타입이 바뀌어도 컴파일 타임 참조 없이 동작한다.
        ///
        /// 예외는 삼키고 로그만 남긴다. 서버 우회 패치에서 예외가 새면 게임 코루틴/UI 흐름이
        /// 통째로 끊기기 때문이다 (원본은 이 자리에서 서버 응답을 기다리고 있었다).
        /// </summary>
        /// <param name="callback">게임이 넘긴 완료 콜백</param>
        /// <param name="tag">로그용 이름</param>
        /// <param name="configure">기본 생성한 응답 객체를 손볼 기회 (null 가능)</param>
        public static void InvokeWithDefault(Delegate callback, string tag, Action<object, Type> configure = null)
        {
            if (callback == null) return;

            try
            {
                ParameterInfo[] ps = callback.GetType().GetMethod("Invoke").GetParameters();
                if (ps.Length == 0)
                {
                    callback.DynamicInvoke(new object[0]);
                    return;
                }

                Type argType = ps[0].ParameterType;
                object arg = CreateDefault(argType);
                if (arg != null && configure != null)
                {
                    configure(arg, argType);
                }

                object[] args = new object[ps.Length];
                args[0] = arg;
                callback.DynamicInvoke(args);
            }
            catch (Exception ex)
            {
                Exception root = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
                MelonLogger.Error($"[Bypass] {tag} 콜백 호출 실패: {root.GetType().Name}: {root.Message}");
            }
        }

        private static object CreateDefault(Type type)
        {
            if (type.IsValueType) return Activator.CreateInstance(type);
            if (type.GetConstructor(Type.EmptyTypes) != null) return Activator.CreateInstance(type);
            return null;
        }
    }
}
