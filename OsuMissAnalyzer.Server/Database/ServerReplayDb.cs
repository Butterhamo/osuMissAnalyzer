using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BMAPI.v1;
using Newtonsoft.Json.Linq;
using ReplayAPI;

namespace OsuMissAnalyzer.Server.Database
{
    public class ServerReplayDb
    {
        private readonly OsuApi api;
        string serverFolder;
        public ServerReplayDb(OsuApi api, string serverFolder)
        {
            this.api = api;
            this.serverFolder = serverFolder;
        }
        private static Dictionary<string, int> modValues = new Dictionary<string, int>
        {
            ["NF"] = (int)Mods.NoFail,
            ["EZ"] = (int)Mods.Easy,
            ["TD"] = (int)Mods.TouchDevice,
            ["HD"] = (int)Mods.Hidden,
            ["HR"] = (int)Mods.HardRock,
            ["SD"] = (int)Mods.SuddenDeath,
            ["DT"] = (int)Mods.DoubleTime,
            ["HT"] = (int)Mods.HalfTime,
            ["NC"] = (int)Mods.NightCore,
            ["FL"] = (int)Mods.FlashLight,
            ["PF"] = (int)Mods.Perfect,
        };
        public async Task<Replay> GetReplayFromOnlineId(string onlineId, string mods, Beatmap beatmap)
        {
            string file = Path.Combine(serverFolder, "replays", $"{onlineId}.osr");
            Replay replay = null;
            if (!File.Exists(file))
            {
                Logger.Log(Logging.ReplaysCacheMiss);
                await Logger.WriteLine("replay not found, downloading...");
                var data = await api.DownloadReplayFromId(onlineId);
                if (data != null)
                {
                    replay = new Replay();
                    replay.Mods = (Mods)Enumerable.Range(0, mods.Length / 2)
                                .Select(i => mods.Substring(i * 2, 2))
                                .Select(s => modValues[s]).Sum();
                    replay.headerLoaded = true;
                    using (MemoryStream ms = new MemoryStream())
                    using (BinaryWriter bw = new BinaryWriter(ms))
                    {
                        bw.WriteNullableString(string.Empty);
                        bw.Write(DateTime.UtcNow.Ticks);
                        bw.Write(data.Length);
                        bw.Write(data);
                        bw.Write(ulong.Parse(onlineId));
                        ms.Seek(0, SeekOrigin.Begin);
                        using (BinaryReader reader = new BinaryReader(ms))
                        {
                            replay.replayReader = reader;
                            replay.Load();
                        }
                    }
                }
                replay.Save(file);
            }
            else
            {
                replay = new Replay(file);
                Logger.Log(Logging.ReplaysCacheHit);
            }
            return replay;
        }
        public async Task<Replay> GetReplayFromScore(JToken score, Beatmap beatmap)
        {
            string file = Path.Combine(serverFolder, "replays", $"{(string)score["best_id"]}.osr");
            Replay replay = null;
            if (!File.Exists(file))
            {

                Logger.Log(Logging.ReplaysCacheMiss);
                await Logger.WriteLine("replay not found, downloading...");
                var replayDownload = api.DownloadReplayFromId((string)score["best_id"]);

                replay = new Replay();
                replay.GameMode = (GameModes)((int)score["mode_int"]);
                replay.MapHash = beatmap.BeatmapHash;
                replay.PlayerName = (string)score["user"]["username"];
                // r.ReplayHash = 
                replay.Count300 = (ushort)score["statistics"]["count_300"];
                replay.Count100 = (ushort)score["statistics"]["count_100"];
                replay.Count50 = (ushort)score["statistics"]["count_50"];
                replay.CountGeki = (ushort)score["statistics"]["count_geki"];
                replay.CountKatu = (ushort)score["statistics"]["count_katu"];
                replay.CountMiss = (ushort)score["statistics"]["count_miss"];
                replay.TotalScore = (uint)score["score"];
                replay.MaxCombo = (ushort)score["max_combo"];
                replay.IsPerfect = (bool)score["perfect"];
                replay.Mods = (Mods)score["mods"].Select(m => modValues[(string)m]).Sum();
                replay.headerLoaded = true;
                using (MemoryStream ms = new MemoryStream())
                using (BinaryWriter bw = new BinaryWriter(ms))
                {
                    bw.WriteNullableString(string.Empty);
                    bw.Write(DateTime.Parse((string)score["created_at"]).ToUniversalTime().Ticks);
                    byte[] data = await replayDownload;
                    bw.Write(data.Length);
                    bw.Write(data);
                    bw.Write((ulong)score["best_id"]);
                    ms.Seek(0, SeekOrigin.Begin);
                    using (BinaryReader reader = new BinaryReader(ms))
                    {
                        replay.replayReader = reader;
                        replay.Load();
                    }
                }
                replay.Save(file);
            }
            else
            {
                replay = new Replay(file);
                Logger.Log(Logging.ReplaysCacheHit);
            }
            return replay;
        }
    }
}