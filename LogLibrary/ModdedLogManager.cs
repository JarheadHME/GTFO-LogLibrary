using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using BepInEx;

namespace LogLibrary;
public static class ModdedLogManager
{
    public static List<uint> GottenLogs = new();
    public static List<uint> m_allLogs = new();
    public static readonly string DirPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GTFO-Modding", "LogLibrary");
    public static bool HasModdedLogs { get; set; } = false;


    public static void WriteGottenLogFiles()
    {
        // If Dev is enabled, don't track log progress
        if (CM_PageLogLibrary.LogInfos.Dev)
            return;

        // write file
        string path = RundownDataPath(Path.Join(DirPath, CM_PageLogLibrary.LogInfos.Name));
        // Logger.Info($"Writing {GottenLogs.Count} logs to {path}");
        using (var stream = File.Open(path, FileMode.Create))
        {
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, false))
            {
                writer.Write(GottenLogs.Count);
                foreach (uint id in GottenLogs)
                {
                    writer.Write(id);
                }
            }
        }

    }

    public static void ReadGottenLogFiles()
    {
        string path = RundownDataPath(Path.Join(DirPath, CM_PageLogLibrary.LogInfos.Name));
        if (File.Exists(path))
        {
            using (var stream = File.Open(path, FileMode.Open))
            {
                using (var reader = new BinaryReader(stream, Encoding.UTF8, false))
                {
                    int count = reader.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        uint id = reader.ReadUInt32();
                        if (!GottenLogs.Contains(id))
                            GottenLogs.Add(id);
                    }
                }
            }
            Logger.Info($"Read {GottenLogs.Count} logs from {path}");
        }
    }

    private static string RundownDataPath(string rundownName)
    {
        char[] invalidPathChars = Path.GetInvalidPathChars();

        foreach (char c in invalidPathChars)
        {
            rundownName = rundownName.Replace(c, '_');
        }

        return Path.Combine(DirPath, rundownName);
    }


    static ModdedLogManager()
    {
        if (!Directory.Exists(DirPath))
        {
            Directory.CreateDirectory(DirPath);
        }
        ReadGottenLogFiles();

        foreach (var Rundown in CM_PageLogLibrary.LogInfos.RundownLogs)
        {
            foreach (var expedition in Rundown.Expeditions)
            {
                foreach (var log in expedition.logs)
                {
                    if (!m_allLogs.Contains(log.id))
                        m_allLogs.Add(log.id);
                }
            }
        }
    }
}
