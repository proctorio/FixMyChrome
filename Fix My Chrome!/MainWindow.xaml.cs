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
using System.Management;
using System.Runtime.InteropServices;

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
        public delegate void MinimizeWindowCallback();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        // minimize every window
        private void MinimizeAll()
        {
            // get the processes
            Process[] processes = Process.GetProcesses();

            // iterate and minimize
            foreach (Process p in processes)
            {
                try
                {
                    // SW_SHOWMINNOACTIVE
                    // minimizes the window and makes sure that it is not active
                    ShowWindow(p.MainWindowHandle, 7);
                }
                catch { }
            }
        }

        /// <summary>
        /// find the chrome install path
        /// </summary>
        /// <param name="chromePath">Registry Key that should have SubKeys that will have the install path within them</param>
        /// <returns>String, the path where Chrome is installed. String.Empty if it cannot be found</returns>
        private string GetChromeInstallPath(RegistryKey chromePath)
        {
            // reset progressbar
            P.Dispatcher.Invoke(new UpdateProgressCallback(this.UpdateProgress), new object[] { 0 });

            // failure, can't continue
            P.Dispatcher.Invoke(new UpdateTitleCallback(this.UpdateTitle), new object[] { "google chrome corrupt, reinstall" });

            // sleep for a moment
            Thread.Sleep(1000);

            // failure, can't continue
            P.Dispatcher.Invoke(new UpdateTitleCallback(this.UpdateTitle), new object[] { "finding chrome uninstaller" });

            // the path that chrome is installed in, then the path to execute our uninstall
            string chrome_installation_path = string.Empty;

            // search for chrome install info
            RegistryKey chrome_install_info = chromePath.OpenSubKey("InstallInfo");
            if (chrome_install_info != null)
            {
                // we have the install info, get a subkey to find the install path
                // these are the ones that we know are good to contain the path that we are looking for
                // and are subkeys to the chrome install registry key
                string[] known_subkeys = new string[] { "HideIconsCommand", "ReinstallCommand", "ShowIconsCommand" };

                // get the keys that we have here
                string[] real_subkeys = chrome_install_info.GetSubKeyNames();

                // iterate over the subkeys
                // we can then find the one that will work
                for (int i = 0; i < real_subkeys.Length; i++)
                {
                    // make sure that this is one that we trust
                    // if it's not, we need to continue to the next one
                    if (!known_subkeys.Contains(real_subkeys[i]))
                        continue;

                    // get the value from the subkey
                    string path = (string)chrome_install_info.GetValue(real_subkeys[i]);
                    if (path.Length > 0)
                    {
                        // split the path, this will break everything into folders
                        string[] split = path.Split('\\');

                        // build the path, removing the last entry
                        // which contains the chrome.exe
                        // we just want the path here
                        chrome_installation_path = string.Join("\\", split.Reverse().Skip(1).Reverse());

                        // remove the first char, it's a quote
                        chrome_installation_path = chrome_installation_path.Substring(1, chrome_installation_path.Length - 1);

                        // make sure that this exists
                        if (Directory.Exists(chrome_installation_path))
                        {
                            // set progressbar to 50%
                            P.Dispatcher.Invoke(new UpdateProgressCallback(this.UpdateProgress), new object[] { 50 });

                            // looks good, let's return
                            return chrome_installation_path;
                        }
                    }
                }
            }

            // if we're here, that means that we couldn't find the subkeys within installinfo
            // we can try another registry key
            if (chromePath.OpenSubKey("shell") == null || chromePath.OpenSubKey("shell").OpenSubKey("open") == null || chromePath.OpenSubKey("shell").OpenSubKey("open").OpenSubKey("command") == null)
            {
                // try again to get the installation path
                chrome_installation_path = ((string)chromePath.OpenSubKey("shell").OpenSubKey("open").OpenSubKey("command").GetValue(null)).Replace("\"", "").Replace("chrome.exe", "");

                // make sure that this exists
                if (Directory.Exists(chrome_installation_path))
                {
                    // set progressbar to 50%
                    P.Dispatcher.Invoke(new UpdateProgressCallback(this.UpdateProgress), new object[] { 50 });

                    // looks good, let's return
                    return chrome_installation_path;
                }
            }

            // if we are here, then the registry didn't work at all
            // let's hope to find chrome in the directory

            // start with the program files
            string program_files = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            // okay we have program files, start looking for chrome
            // check to see if we have a google chrome application folder
            if (Directory.Exists(program_files + @"\Google\Chrome\Application"))
            {
                // set progressbar to 50%
                P.Dispatcher.Invoke(new UpdateProgressCallback(this.UpdateProgress), new object[] { 50 });

                // this folder exists, so this should be the application install path
                return program_files + @"\Google\Chrome\Application";
            }

            // return what we found
            return chrome_installation_path;
        }

        /// <summary>
        /// Uninstall a bad chrome and launch IE to the chrome installation page
        /// </summary>
        /// <param name="chrome_installation_path">the path where chrome is installed</param>
        private void UninstallChrome(string chrome_installation_path)
        {
            // make sure that we got a path
            // if not, we should fall back to something
            if (chrome_installation_path != string.Empty && Directory.Exists(chrome_installation_path))
            {
                // the directory exists, we need to find the latest chrome that is in here
                // this folder should contain folders that have each version
                string[] directories = Directory.GetDirectories(chrome_installation_path);

                // we should have a list of subdirectories
                // find the ones that start with a number (these are version numbers)
                string subpath = directories.Select(x => x.Remove(0, chrome_installation_path.Length + 1)).Where(x => char.IsNumber(x[0])).OrderByDescending(x => int.Parse(x.Split('.')[0])).FirstOrDefault();

                // build the path
                chrome_installation_path = string.Join("\\", chrome_installation_path, subpath, "Installer");

                // need the installer path
                if (Directory.Exists(chrome_installation_path))
                {
                    // check to see if the chrome setup.exe exists
                    if (File.Exists(chrome_installation_path + "\\setup.exe"))
                    {
                        // we can uninstall
                        // but we have to kill any chrome processes that are running
                        // otherwise it will ask us to stop them before continuing
                        Process[] chrome_processes = Process.GetProcesses().Where(x => x.ProcessName.Contains("chrome")).ToArray();

                        // iterate and kill
                        foreach (Process process in chrome_processes)
                        {
                            try
                            {
                                process.Kill();
                            }
                            catch { }
                        }

                        // failure, can't continue
                        P.Dispatcher.Invoke(new UpdateTitleCallback(this.UpdateTitle), new object[] { "launching uninstaller" });

                        // reset progressbar
                        P.Dispatcher.Invoke(new UpdateProgressCallback(this.UpdateProgress), new object[] { 75 });

                        // minimize everything before launching the chrome uninstaller
                        MinimizeAll();

                        // run the uninstall 
                        System.Diagnostics.Process.Start(chrome_installation_path + "\\setup.exe", "--uninstall --multi-install --chrome --system-level");

                        // sleep for a moment
                        Thread.Sleep(1000);

                        // look for chrome again, we should have the uninstall process running
                        Process uninstall_process = Process.GetProcesses().Where(x => x.ProcessName.Contains("chrome")).FirstOrDefault();

                        // we have an uninstall process, hide the 
                        if (uninstall_process != null)
                            P.Dispatcher.Invoke(new MinimizeWindowCallback(this.MinimizeForm));

                        // wait until there is no more chrome to launch IE to the download page 
                        while (Process.GetProcesses().Where(x => x.ProcessName.Contains("chrome")).FirstOrDefault() != null)
                            Thread.Sleep(2500);

                        // launch internet explorer to the chrome download page
                        Process.Start("iexplore.exe", "https://www.google.com/chrome/browser/desktop/index.html#eula");

                        // kill this app
                        Environment.Exit(0);
                    }
                }
            }

            // if we are here, then we have failed to find the chrome to uninstall

            // failure, can't continue
            P.Dispatcher.Invoke(new UpdateTitleCallback(this.UpdateTitle), new object[] { "could not find uninstaller" });

            // reset progressbar
            P.Dispatcher.Invoke(new UpdateProgressCallback(this.UpdateProgress), new object[] { 0 });

            // hide the pulsing chrome icon
            CheckMark.Dispatcher.Invoke(new HideChromeCallback(this.HideChrome));

            // show failure X
            X.Dispatcher.Invoke(new ShowXCallback(this.ShowX));

            // launch internet explorer to the chrome uninstall guide
            Process.Start("iexplore.exe", "https://support.google.com/chrome/answer/95319?hl=en");

            // kill this app
            Environment.Exit(0);
        }

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

            // get the chrome installation path, uninstall chrome
            UninstallChrome(GetChromeInstallPath(chromePath));

            // find the shell open path
            if (chromePath.OpenSubKey("shell") == null || chromePath.OpenSubKey("shell").OpenSubKey("open") == null || chromePath.OpenSubKey("shell").OpenSubKey("open").OpenSubKey("command") == null)
            {
                // get the chrome installation path, uninstall chrome
                UninstallChrome(GetChromeInstallPath(chromePath));
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
                // get the chrome installation path, uninstall chrome
                UninstallChrome(GetChromeInstallPath(chromePath));
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

        // minimize the form so we can see the chrome uninstaller
        private void MinimizeForm() { this.WindowState = WindowState.Minimized; }

        // load it up
        private void OnWindowLoaded(object sender, RoutedEventArgs e) { Thread UI = new Thread(new ThreadStart(UIThread)); UI.Start(); }
    }
}
