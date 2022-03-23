using System;
using System.IO;
using Newtonsoft.Json;

namespace AFK
{
    public class AFKConfigFile
    {
        public bool afkkick = true;
        public int afkkicktime = 900;
        public int afkspam = 20;
        public string returnMessage = "{player} is back from AFK";

        public static AFKConfigFile Read(string path)
        {
            if (!File.Exists(path))
                return new AFKConfigFile();
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return Read(fs);
            }
        }

        public static AFKConfigFile Read(Stream stream)
        {
            using (var sr = new StreamReader(stream))
            {
                var cf = JsonConvert.DeserializeObject<AFKConfigFile>(sr.ReadToEnd());
                if (ConfigRead != null)
                    ConfigRead(cf);
                return cf;
            }
        }

        public void Write(string path)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Write))
            {
                Write(fs);
            }
        }

        public void Write(Stream stream)
        {
            var str = JsonConvert.SerializeObject(this, Formatting.Indented);
            using (var sw = new StreamWriter(stream))
            {
                sw.Write(str);
            }
        }

        public static Action<AFKConfigFile> ConfigRead;
    }
}
