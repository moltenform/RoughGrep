﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TrivialBehind;

namespace RoughGrep
{

    public static class Logic
    {
        public static string WorkDir = null;
        public static string RgExtraArgs = "";
        static List<string> Lines = new List<string>();
        public static BindingList<string> DirHistory = new BindingList<string>();
        public static BindingList<string> SearchHistory = new BindingList<string>();

        public static void InitApp()
        {
            var extraArgs = string.Join(" ", Environment.GetCommandLineArgs().Skip(1));
            Logic.RgExtraArgs = extraArgs == "" ? "-i " : extraArgs + " ";
            Logic.WorkDir = Directory.GetCurrentDirectory();
            TrivialBehinds.RegisterBehind<MainFormUi, MainFormBehind>();
        }
        public static Action Debounce(int delayms, Action action)
        {
            Stopwatch sw = new Stopwatch();
            return () =>
            {
                var runIt = !sw.IsRunning ? true : sw.ElapsedMilliseconds > delayms;
                if (runIt)
                {
                    action();
                    sw.Restart();
                } else
                {
                    ;
                    // skipping;
                }
            };
        }
        public static void StartSearch(MainFormUi ui)
        {
            var text = ui.searchTextBox.Text;
            if (text == null || text.Trim().Length == 0)
            {
                return;
            }
            WorkDir = ui.dirSelector.Text;
            if (!Directory.Exists(WorkDir))
            {
                ui.previewBox.Text = $"Directory does not exist: '{WorkDir}'";
                return;
            }
            text = text.Replace("\"", "\\\"");
            var p = new Process();

            AssignStartInfo(p.StartInfo, "rg.exe", $"{RgExtraArgs}--heading -M 200 -n \"{text}\"");
            p.StartInfo.RedirectStandardOutput = true;
            ui.previewBox.Text = $"{p.StartInfo.Arguments} [{WorkDir}]";

            ui.resultBox.Clear();
            Lines.Clear();
            p.EnableRaisingEvents = true;
            Action updateRows = () => ui.resultBox.Lines = Lines.ToArray();
            var toFlush = new List<string>();
            var flushlock = new Object();


            Action doFlush = () =>
            {
                var fl = toFlush;
                toFlush = new List<string>();
                ui.resultBox.AppendText(string.Join("\r\n", fl) + "\r\n");
            };
            Action debouncedFlush = Debounce(100, doFlush);

            p.OutputDataReceived += (o,ev) =>            
            {
                if (ev.Data == null)
                {
                    return;
                }
                lock (flushlock)
                {
                    Lines.Add(ev.Data);
                    toFlush.Add(ev.Data);
                    ui.resultBox.Invoke(debouncedFlush);
                }
            };
            p.Exited += (o, ev) =>
            {
                ui.resultBox.Invoke(doFlush);
                //Action a = () => ui.resultBox.Lines = Lines.ToArray();
                //ui.resultBox.Invoke(a);

            };
            p.Start();
            p.BeginOutputReadLine();
            PrependIfNew(Logic.DirHistory, WorkDir);
            PrependIfNew(Logic.SearchHistory, text);
            ui.dirSelector.SelectedIndex = 0;
            ui.searchTextBox.SelectedIndex = 0;
        }
        static void PrependIfNew<T>(IList<T> coll, T entry ) where T: IComparable<T>
        {
            if (coll.Count > 0 && coll.ElementAt(0).CompareTo(entry) == 0)
            {
                return;
            }
            coll.Insert(0, entry);
        }

        public static void AssignStartInfo(ProcessStartInfo psi, string fname, string arguments)
        {
            psi.FileName = fname;
            psi.Arguments = arguments;
            psi.WorkingDirectory = WorkDir;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
        }
        public static ProcessStartInfo CreateStartInfo(string fname, string arguments)
        {
            var psi = new ProcessStartInfo();
            AssignStartInfo(psi, fname, arguments);
            return psi;
        }

        public static (string, int) LookupFileAtLine(int lineNumber)
        {
            if (lineNumber > Lines.Count - 1)
            {
                return (null, 0);
            }
            var split = Lines[lineNumber].Split(':');
            var resLineNum = 0;
            if (split.Length > 1)
            {
                Int32.TryParse(split[0], out resLineNum);
            } 

            for (var idx = lineNumber;  idx >= 0; idx--)
            {
                var linetext = Lines[idx];
                if (linetext.Length == 0)
                {
                    continue;
                }            
                if (char.IsDigit(linetext[0]))
                {
                    continue;
                }
                return (Path.Combine(WorkDir, Lines[idx]), resLineNum);
            }
            return (null, 0);
        }
    }
}
