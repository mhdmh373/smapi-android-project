using System;
using System.Collections.Generic;
using System.Diagnostics;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace MeasureMod
{
    /// <summary>
    /// مود قياس يعمل على الجهاز: milestones + تجميدات + ذاكرة + قائمة مودات.
    /// يستخدم واجهات SMAPI العامة فقط (IModRegistry وIDataHelper وأحداث GameLoop).
    /// ملاحظة صريحة: لا يمكن لمود قياس زمن دخول (Entry) مود آخر — نرصد
    /// المراحل والأعراض (تجميد/ذاكرة) لا توزيع اللوم على مود معين.
    /// </summary>
    internal class ModEntry : Mod
    {
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Dictionary<string, double> milestones = new Dictionary<string, double>();
        private readonly List<string> memSamples = new List<string>();

        private DateTime lastTick = DateTime.UtcNow;
        private long tickCount;
        private int freezeCount;
        private double freezeTotalSec;
        private double maxGapSec;
        private int seconds;
        private bool launched;

        public override void Entry(IModHelper helper)
        {
            Mark("mod-entry");
            helper.Events.GameLoop.GameLaunched += (s, e) => { Mark("game-launched"); OnLaunched(); };
            helper.Events.GameLoop.SaveLoaded += (s, e) => Mark("save-loaded");
            helper.Events.GameLoop.DayStarted += (s, e) => Mark("day-started");
            helper.Events.GameLoop.ReturnedToTitle += (s, e) => { Mark("back-to-title"); WriteReport("title"); };
            helper.Events.GameLoop.UpdateTicked += OnTick;
            helper.Events.GameLoop.OneSecondUpdateTicked += OnSecond;

            helper.ConsoleCommands.Add(
                "measure",
                "measure [report|mods|reset] - show this session's performance report.",
                OnCommand);
        }

        private void Mark(string name)
        {
            if (!milestones.ContainsKey(name))
                milestones[name] = Math.Round(clock.Elapsed.TotalSeconds, 1);
        }

        private void OnLaunched()
        {
            launched = true;
            lastTick = DateTime.UtcNow;
            SampleMem("launch");
        }

        private void OnTick(object sender, UpdateTickedEventArgs e)
        {
            try
            {
                DateTime now = DateTime.UtcNow;
                double gap = (now - lastTick).TotalSeconds;
                lastTick = now;
                tickCount++;
                if (gap > maxGapSec)
                    maxGapSec = Math.Round(gap, 2);
                // أي فجوة فوق ثانيتين = تجميد (عمل ثقيل على خيط الواجهة)
                if (launched && gap > 2.0)
                {
                    freezeCount++;
                    freezeTotalSec = Math.Round(freezeTotalSec + gap, 1);
                    if (freezeCount <= 5)
                        Monitor.Log($"Measure: freeze #{freezeCount} lasted {gap:F1}s (tick #{tickCount}).", LogLevel.Warn);
                }
            }
            catch { }
        }

        private void OnSecond(object sender, OneSecondUpdateTickedEventArgs e)
        {
            try
            {
                seconds++;
                if (seconds % 30 == 0)
                    SampleMem(seconds + "s");
            }
            catch { }
        }

        private void SampleMem(string tag)
        {
            try
            {
                long gc = GC.GetTotalMemory(false) / 1048576;
                long ws = -1;
                try { ws = Process.GetCurrentProcess().WorkingSet64 / 1048576; } catch { }
                memSamples.Add($"{tag}: GC={gc}MB WS={(ws < 0 ? "?" : ws + "MB")}");
                if (memSamples.Count > 40)
                    memSamples.RemoveAt(0);
            }
            catch { }
        }

        private void OnCommand(string name, string[] args)
        {
            string sub = args != null && args.Length > 0 ? args[0].ToLowerInvariant() : "report";
            if (sub == "mods")
            {
                PrintMods();
                return;
            }
            if (sub == "reset")
            {
                milestones.Clear();
                memSamples.Clear();
                tickCount = 0;
                freezeCount = 0;
                freezeTotalSec = 0;
                maxGapSec = 0;
                clock.Restart();
                Mark("mod-entry");
                Monitor.Log("Measure: counters reset.", LogLevel.Info);
                return;
            }
            PrintReport();
            WriteReport("manual");
        }

        private void PrintReport()
        {
            Monitor.Log("Measure report (seconds since mod entry):", LogLevel.Info);
            foreach (KeyValuePair<string, double> m in milestones)
                Monitor.Log($"  {m.Key}: {m.Value}s", LogLevel.Info);
            Monitor.Log($"  ticks: {tickCount}, freezes>2s: {freezeCount} (total {freezeTotalSec}s), max gap: {maxGapSec}s", LogLevel.Info);
            foreach (string s in memSamples)
                Monitor.Log("  mem " + s, LogLevel.Info);
        }

        private void PrintMods()
        {
            int i = 0;
            foreach (IModInfo mod in Helper.ModRegistry.GetAll())
            {
                i++;
                string name = "?";
                string ver = "?";
                string id = "?";
                try
                {
                    if (mod.Manifest != null)
                    {
                        name = mod.Manifest.Name ?? "?";
                        id = mod.Manifest.UniqueID ?? "?";
                        if (mod.Manifest.Version != null)
                            ver = mod.Manifest.Version.ToString();
                    }
                }
                catch { }
                Monitor.Log($"  {i}) {name} {ver} [{id}]", LogLevel.Info);
            }
            Monitor.Log($"Measure: {i} mods loaded.", LogLevel.Info);
        }

        private void WriteReport(string reason)
        {
            try
            {
                Dictionary<string, object> data = new Dictionary<string, object>();
                data["reason"] = reason;
                data["milestones"] = milestones;
                data["ticks"] = tickCount;
                data["freezes"] = freezeCount;
                data["freezeTotalSec"] = freezeTotalSec;
                data["maxGapSec"] = maxGapSec;
                data["mem"] = memSamples;
                List<string> mods = new List<string>();
                foreach (IModInfo mod in Helper.ModRegistry.GetAll())
                {
                    string name = "?";
                    string ver = "?";
                    string id = "?";
                    try
                    {
                        if (mod.Manifest != null)
                        {
                            name = mod.Manifest.Name ?? "?";
                            id = mod.Manifest.UniqueID ?? "?";
                            if (mod.Manifest.Version != null)
                                ver = mod.Manifest.Version.ToString();
                        }
                    }
                    catch { }
                    mods.Add($"{name} {ver} [{id}]");
                }
                data["mods"] = mods;
                Helper.Data.WriteJsonFile("measure-report.json", data);
            }
            catch { }
        }
    }
}
