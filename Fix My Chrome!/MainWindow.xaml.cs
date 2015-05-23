using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Fix_My_Chrome
{
    /// <summary>
    /// Proctorio Inc, Open Source Initiative 2015 https://proctorio.com
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // remove the window icon, clean is key
        protected override void OnSourceInitialized(EventArgs e) { IconHelper.RemoveIcon(this); }

        // delegates for ui update outside of render thread
        public delegate void UpdateProgressCallback(double v);
        public delegate void ShowCheckCallback();
        public delegate void UpdateTitleCallback(string m);
        public delegate void HideChromeCallback();
        public delegate void ShowXCallback();
        public delegate void ShowChromeCallback();
        public delegate void HideCheckCallback();

        // thread to animate and fix chrome
        private void UIThread()
        {
            // reset progressbar
            P.Dispatcher.Invoke(new UpdateProgressCallback(this.UpdateProgress), new object[] { 0 });

            // hang tight
            P.Dispatcher.Invoke(new UpdateTitleCallback(this.UpdateTitle), new object[] { ("hang tight") });

            // sleep a bit
            Thread.Sleep(500);

            // find me some microphones
            P.Dispatcher.Invoke(new UpdateTitleCallback(this.UpdateTitle), new object[] { ("looking for chrome...") });

            // sleep a bit
            Thread.Sleep(500);

            // lets find chrome
            RegistryKey chromePath = null;

            // reset progressbar
            P.Dispatcher.Invoke(new UpdateProgressCallback(this.UpdateProgress), new object[] { 25 });

            // this is easy becuase microsoft tells them to install into a specific registry path
            // HKEY_LOCAL_MACHINE\SOFTWARE\Clients\StartMenuInternet. 
            // MSDN says so http://msdn.microsoft.com/en-us/library/dd203067%28VS.85%29.aspx
            // check 64bit registry path first
            try { chromePath = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Clients\StartMenuInternet\Google Chrome"); } catch { }

            // did we find it on this machine?
            try { if (chromePath == null) chromePath = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Clients\StartMenuInternet\Google Chrome"); } catch { }

            // are we good?
            if(chromePath == null)
            {
                // failure, can't continue
                P.Dispatcher.Invoke(new UpdateTitleCallback(this.UpdateTitle), new object[] { "google chrome not found, please install" });

                // reset progressbar
                P.Dispatcher.Invoke(new UpdateProgressCallback(this.UpdateProgress), new object[] { 0 });

                // hide the pulsing chrome icon
                CheckMark.Dispatcher.Invoke(new HideChromeCallback(this.HideChrome));

                // show failure X
                X.Dispatcher.Invoke(new ShowXCallback(this.ShowX));

                // open download google chrome link
                Process.Start("https://www.google.com/chrome/browser/desktop/index.html#eula");
                return;
            }

            // find the shell open path
            if (chromePath.OpenSubKey("shell") == null || chromePath.OpenSubKey("shell").OpenSubKey("open") == null || chromePath.OpenSubKey("shell").OpenSubKey("open").OpenSubKey("command") == null)
            {
                // failure, can't continue
                P.Dispatcher.Invoke(new UpdateTitleCallback(this.UpdateTitle), new object[] { "google chrome corrupt, reinstall" });

                // reset progressbar
                P.Dispatcher.Invoke(new UpdateProgressCallback(this.UpdateProgress), new object[] { 0 });

                // hide the pulsing chrome icon
                CheckMark.Dispatcher.Invoke(new HideChromeCallback(this.HideChrome));

                // show failure X
                X.Dispatcher.Invoke(new ShowXCallback(this.ShowX));

                // open download google chrome link
                Process.Start("https://www.google.com/chrome/browser/desktop/index.html#eula");
                return;
            }

            // get the chrome path and get all possible version folders
            string version_number = null;
            try
            {
                string application_path = ((string)chromePath.OpenSubKey("shell").OpenSubKey("open").OpenSubKey("command").GetValue(null)).Replace("\"", "").Replace("chrome.exe", "");
                if (Directory.Exists(application_path))
                {
                    // get all possible values
                    string[] subdirectoryEntries = Directory.GetDirectories(application_path);

                    // only take ones where the first character is a digit and sort 
                    version_number = subdirectoryEntries.Select(s => s.Remove(0, application_path.Length)).ToArray().Where(x => Char.IsDigit(x.ToCharArray()[0])).OrderByDescending(y => y).FirstOrDefault();
                }
            }
            catch { }

            // did we find the version number?
            if (string.IsNullOrWhiteSpace(version_number))
            {
                // failure, can't continue
                P.Dispatcher.Invoke(new UpdateTitleCallback(this.UpdateTitle), new object[] { "google chrome corrupt, reinstall" });

                // reset progressbar
                P.Dispatcher.Invoke(new UpdateProgressCallback(this.UpdateProgress), new object[] { 0 });

                // hide the pulsing chrome icon
                CheckMark.Dispatcher.Invoke(new HideChromeCallback(this.HideChrome));

                // show failure X
                X.Dispatcher.Invoke(new ShowXCallback(this.ShowX));

                // open download google chrome link
                Process.Start("https://www.google.com/chrome/browser/desktop/index.html#eula");
                return;
            }

            // chrome found, report version
            P.Dispatcher.Invoke(new UpdateTitleCallback(this.UpdateTitle), new object[] { ("found google chrome " + version_number) });

            // reset progressbar
            P.Dispatcher.Invoke(new UpdateProgressCallback(this.UpdateProgress), new object[] { 50 });

            // sleep a bit
            Thread.Sleep(2000);

            // 1.) fix registry
            P.Dispatcher.Invoke(new UpdateTitleCallback(this.UpdateTitle), new object[] { ("force desktop mode") });

            // sleep a bit
            Thread.Sleep(500);

            try
            {
                // set the reg key to froce this: http://www.techgainer.com/disable-google-chrome-windows-8-mode/
                RegistryKey myKey = Registry.CurrentUser.OpenSubKey(@"Software\Google\Chrome\Metro", true);
                myKey.SetValue("launch_mode", "0", RegistryValueKind.DWord);
            }
            catch { }

            // sleep a bit
            Thread.Sleep(500);

            // finsh out the progress bar
            P.Dispatcher.Invoke(new UpdateProgressCallback(this.UpdateProgress), new object[] { 100 });

            // hide the pulsing chrome icon
            CheckMark.Dispatcher.Invoke(new HideChromeCallback(this.HideChrome));

            // show the checkmark
            CheckMark.Dispatcher.Invoke(new ShowCheckCallback(this.ShowCheck));

            // done
            P.Dispatcher.Invoke(new UpdateTitleCallback(this.UpdateTitle), new object[] { "done fixing chrome" });

            // Zzzz
            Thread.Sleep(2000);

            // reset progressbar
            P.Dispatcher.Invoke(new UpdateProgressCallback(this.UpdateProgress), new object[] { 0 });

            // hide the check mark
            CheckMark.Dispatcher.Invoke(new HideCheckCallback(this.HideCheck));

            // show the pulsing chrome icon
            CheckMark.Dispatcher.Invoke(new ShowChromeCallback(this.ShowChrome));

            // figure out how many chrome processes are open and running
            Process[] chromeInstances = Process.GetProcessesByName("chrome");
            int total = chromeInstances.Length;

            // case where no chrome windows open
            if (total <= 0)
            {
                // indicate chrome restart
                P.Dispatcher.Invoke(new UpdateTitleCallback(this.UpdateTitle), new object[] { "opening chrome..." });

                // open chrome
                Process.Start(@"chrome.exe");
            }
            else
            {
                // indicate chrome restart
                P.Dispatcher.Invoke(new UpdateTitleCallback(this.UpdateTitle), new object[] { "restarting chrome..." });

                // restart all instances of chrome, wait for them to all close
                Process.Start(@"chrome.exe", "http://restart.chrome/");

                // stopwatch for give up plan
                Stopwatch sw = new Stopwatch();
                sw.Start();
                while (true)
                {
                    chromeInstances = Process.GetProcessesByName("chrome");

                    // wait til we reach 2 or less chrome instances
                    // also give up after 45 seconds
                    if (chromeInstances.Length <= 2 || sw.Elapsed.TotalSeconds > 45)
                    {
                        // done
                        break;
                    }
                    else
                    {
                        // make sure the "progress" donut doesnt show less progress over time
                        total = Math.Max(total, chromeInstances.Length);

                        // update th eprogress bar
                        P.Dispatcher.Invoke(new UpdateProgressCallback(this.UpdateProgress), new object[] { Math.Ceiling((((double)total - (double)chromeInstances.Length) / (double)total) * 100) });
                    }

                    // dont spin the cpu
                    Thread.Sleep(100);
                }
            }

            // set to 100% for visual clue
            P.Dispatcher.Invoke(new UpdateProgressCallback(this.UpdateProgress), new object[] { 100 });

            // hide the pulsing chrome icon
            CheckMark.Dispatcher.Invoke(new HideChromeCallback(this.HideChrome));

            // show the check mark
            CheckMark.Dispatcher.Invoke(new ShowCheckCallback(this.ShowCheck));

            // set done and good luck messaging
            P.Dispatcher.Invoke(new UpdateTitleCallback(this.UpdateTitle), new object[] { "done, good luck on your exam!" });

            // let them read it and wait
            Thread.Sleep(5000);

            // kill this app
            Environment.Exit(0);
        }

        // update the progressbar
        private void UpdateProgress(double v) { P.Value = v; }

        // show the checkmark
        private void ShowCheck() { CheckMark.Visibility = Visibility.Visible; }

        // show the x
        private void ShowX() { X.Visibility = Visibility.Visible; }

        // hide the chrome icon
        private void HideChrome() { Chrome.Visibility = Visibility.Hidden; }

        // hide the checkmark
        private void HideCheck() { CheckMark.Visibility = Visibility.Hidden; }

        // show the chrome icon
        private void ShowChrome() { Chrome.Visibility = Visibility.Visible; }

        // title change for progress of events
        private void UpdateTitle(string m) { this.Title = m; }

        // load it up
        private void OnWindowLoaded(object sender, RoutedEventArgs e) { Thread UI = new Thread(new ThreadStart(UIThread)); UI.Start(); }
    }
}
