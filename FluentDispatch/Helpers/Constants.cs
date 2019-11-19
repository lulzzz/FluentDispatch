using System.IO;

namespace FluentDispatch.Helpers
{
    internal class Constants
    {
        public static readonly string GCDCachePath = $@"{Path.GetTempPath()}FluentDispatch-Cache";

        public static readonly string PathToSqliteDbFile =
            $@"{GCDCachePath}{Path.DirectorySeparatorChar}GCDCache.db";
    }
}