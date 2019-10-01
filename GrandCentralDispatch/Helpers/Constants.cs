using System.IO;

namespace GrandCentralDispatch.Helpers
{
    internal class Constants
    {
        public static readonly string GCDCachePath = $@"{Path.GetTempPath()}GrandCentralDispatch-Cache";

        public static readonly string PathToSqliteDbFile =
            $@"{GCDCachePath}{Path.DirectorySeparatorChar}GCDCache.db";
    }
}