using System.IO;
using System.Reflection;

namespace GrandCentralDispatch.Monitoring.Helpers
{
    public class Helper
    {
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
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
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