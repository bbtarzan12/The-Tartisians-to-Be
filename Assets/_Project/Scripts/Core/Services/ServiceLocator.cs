using System;
using System.Collections.Generic;

namespace Tartisians.Core.Services
{
    /// <summary>
    /// 전역 서비스 등록/조회. 싱글톤 남발을 대체한다.
    /// 부트스트랩에서 등록하고, 시스템은 여기서 의존성을 가져온다.
    /// </summary>
    public static class ServiceLocator
    {
        static readonly Dictionary<Type, object> Services = new();

        public static void Register<T>(T service) where T : class
        {
            Services[typeof(T)] = service ?? throw new ArgumentNullException(nameof(service));
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            if (Services.TryGetValue(typeof(T), out object value))
            {
                service = (T)value;
                return true;
            }

            service = null;
            return false;
        }

        public static T Get<T>() where T : class
        {
            if (TryGet(out T service))
            {
                return service;
            }

            throw new InvalidOperationException($"서비스가 등록되지 않았습니다: {typeof(T).Name}");
        }

        public static void Unregister<T>() where T : class => Services.Remove(typeof(T));

        public static void Clear() => Services.Clear();
    }
}
