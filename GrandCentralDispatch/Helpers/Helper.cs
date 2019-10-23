using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace GrandCentralDispatch.Helpers
{
    internal class Helper
    {
        public static string GetVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            return fvi.FileVersion;
        }

        public static void ExtractResource(string embeddedFileName, string destinationPath)
        {
            var currentAssembly = Assembly.GetExecutingAssembly();
            using (var resourceToSave = currentAssembly.GetManifestResourceStream(embeddedFileName))
            {
                File.WriteAllBytes(destinationPath, ReadFully(resourceToSave));
            }
        }

        private static byte[] ReadFully(Stream input)
        {
            var buffer = new byte[16 * 1024];
            using (var ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }

                return ms.ToArray();
            }
        }
    }
}