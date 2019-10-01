using System.Collections.Generic;

namespace GrandCentralDispatch.Sample.Web.Helpers
{
    public static class HashHelper
    {
        public static bool IsDefault<T>(this T @object)
            => EqualityComparer<T>.Default.Equals(@object, default(T));

        private const int PrimeNumber = 486187739;

        private const int DefaultHashValue = 31;

        public static int GetHashCode<T>(T param) => param.IsDefault() ? 0 : param.GetHashCode();

        public static int GetHashCode<T1, T2, T3>(T1 param1, T2 param2, T3 param3)
        {
            unchecked
            {
                var hash = DefaultHashValue;
                hash = hash * PrimeNumber + GetHashCode(param1);
                hash = hash * PrimeNumber + GetHashCode(param2);
                return hash * PrimeNumber + GetHashCode(param3);
            }
        }
    }
}
