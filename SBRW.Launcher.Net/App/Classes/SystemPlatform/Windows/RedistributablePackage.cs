﻿using SBRW.Launcher.App.Classes.LauncherCore.Languages.Visual_Forms;
using SBRW.Launcher.App.Classes.LauncherCore.Lists;
using SBRW.Launcher.App.Classes.LauncherCore.Logger;
using SBRW.Launcher.App.Classes.SystemPlatform.Unix;
using SBRW.Launcher.Core.Cache;
using SBRW.Launcher.Core.Extension.Api_;
using SBRW.Launcher.Core.Extension.Logging_;
using SBRW.Launcher.Core.Extension.Web_;
using SBRW.Launcher.Core.Discord.RPC_;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Windows.Forms;
using SBRW.Launcher.Core.Extension.Registry_;

// based on https://github.com/bitbeans/RedistributableChecker/blob/master/RedistributableChecker/RedistributablePackage.cs
namespace SBRW.Launcher.App.Classes.SystemPlatform.Windows
{
    /// <summary>
    /// Microsoft Visual C++ Redistributable Package Versions
    /// </summary>
    public enum RedistributablePackageVersion
    {
        VC2015to2019x86,
        VC2015to2019x64,
    };

    /// <summary>
    ///	Class to detect installed Microsoft Redistributable Packages.
    /// </summary>
    /// <see cref="//https://stackoverflow.com/questions/12206314/detect-if-visual-c-redistributable-for-visual-studio-2012-is-installed"/>
    public static class RedistributablePackage
    {
        private static string InstalledVersion { get; set; }
        /// <summary>
        /// Check if a Microsoft Redistributable Package is installed.
        /// </summary>
        /// <param name="Redistributable_Version">The package version to detect.</param>
        /// <returns><c>true</c> if the package is installed, otherwise <c>false</c></returns>
        public static bool IsInstalled(RedistributablePackageVersion Redistributable_Version)
        {
            {
                switch (Redistributable_Version)
                {
                    case RedistributablePackageVersion.VC2015to2019x86:
                        InstalledVersion = Registry_Core.Read("Version",
                                Path.Combine("SOFTWARE", "Microsoft", "VisualStudio", "14.0", "VC", "Runtimes", "x86"));
                        goto case RedistributablePackageVersion.VC2015to2019x64;
                    case RedistributablePackageVersion.VC2015to2019x64:
                        if (string.IsNullOrWhiteSpace(InstalledVersion))
                        {
                            InstalledVersion = Registry_Core.Read("Version",
                                Path.Combine("SOFTWARE", "Microsoft", "VisualStudio", "14.0", "VC", "Runtimes", "x64"));
                        }
                        
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(InstalledVersion))
                            {
                                if (InstalledVersion.StartsWith("v"))
                                {
                                    InstalledVersion = InstalledVersion.Trim('v');
                                }

                                if (InstalledVersion.CompareTo("14.20") >= 0)
                                {
                                    return true;
                                }
                                else
                                {
                                    return false;
                                }
                            }
                            else
                            {
                                return false;
                            }
                        }
                        catch (Exception Error)
                        {
                            LogToFileAddons.OpenLog("Redistributable Package", string.Empty, Error, string.Empty, true);
                            return false;
                        }
                        finally
                        {
                            if (!string.IsNullOrWhiteSpace(InstalledVersion))
                            {
                                InstalledVersion = string.Empty;
                            }
                        }
                    default:
                        return false;
                }

            }
        }
    }

    class Redistributable
    {
        public static bool Error_Free { get; set; } = true;
        public static void Check()
        {
            if (!UnixOS.Detected())
            {
                Log.Checking("REDISTRIBUTABLE: Is Installed or Not");
                Presence_Launcher.Status("Start Up", "Checking Redistributable Package Visual Code 2015 to 2019");

                if (!RedistributablePackage.IsInstalled(RedistributablePackageVersion.VC2015to2019x86))
                {
                    if (MessageBox.Show(Translations.Database("Redistributable_VC_32") +
                        "\n\n" + Translations.Database("Redistributable_VC_P2") +
                        "\n\n" + Translations.Database("Redistributable_VC_P3") +
                        "\n\n" + Translations.Database("Redistributable_VC_P4"),
                        Translations.Database("Redistributable_VC_P5"),
                        MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK)
                    {
                        try
                        {
                            Uri URLCall = new Uri("https://aka.ms/vs/16/release/VC_redist.x86.exe");
                            ServicePointManager.FindServicePoint(URLCall).ConnectionLeaseTimeout = (int)TimeSpan.FromMinutes(1).TotalMilliseconds;
                            var Client = new WebClient();

                            if (!Launcher_Value.Launcher_Alternative_Webcalls()) { Client = new WebClientWithTimeout(); }
                            else
                            {
                                Client.Headers.Add("user-agent", "SBRW Launcher " +
                                Application.ProductVersion + " (+https://github.com/SoapBoxRaceWorld/GameLauncher_NFSW)");
                            }

                            try
                            {
                                Client.DownloadFile(URLCall, "VC_redist.x86.exe");
                            }
                            catch (WebException Error)
                            {
                                API_Core.StatusCodes(URLCall.GetComponents(UriComponents.HttpRequestUrl, UriFormat.SafeUnescaped),
                                    Error, (HttpWebResponse)Error.Response);
                            }
                            catch (Exception Error)
                            {
                                LogToFileAddons.OpenLog("REDISTRIBUTABLE", string.Empty, Error, string.Empty, true);
                            }
                            finally
                            {
                                if (Client != null)
                                {
                                    Client.Dispose();
                                }
                            }
                        }
                        catch (Exception Error)
                        {
                            LogToFileAddons.OpenLog("REDISTRIBUTABLE", string.Empty, Error, string.Empty, true);
                        }

                        if (File.Exists("VC_redist.x86.exe"))
                        {
                            try
                            {
                                var proc = Process.Start(new ProcessStartInfo
                                {
                                    Verb = "runas",
                                    Arguments = "/quiet",
                                    FileName = "VC_redist.x86.exe"
                                });
                                proc.WaitForExit((int)TimeSpan.FromMinutes(10).TotalMilliseconds);

                                if (proc == null)
                                {
                                    Error_Free = false;
                                    MessageBox.Show(Translations.Database("Redistributable_VC_P6"),
                                        Translations.Database("Redistributable_VC_P5"), MessageBoxButtons.OK,
                                        MessageBoxIcon.Error);
                                }
                                else if (proc != null)
                                {
                                    if (!proc.HasExited)
                                    {
                                        if (proc.Responding) { proc.CloseMainWindow(); }
                                        else { proc.Kill(); Error_Free = false; }
                                    }

                                    if (proc.ExitCode != 0)
                                    {
                                        Error_Free = false;
                                        Log.Error("REDISTRIBUTABLE INSTALLER [EXIT CODE]: " + proc.ExitCode.ToString() +
                                            " HEX: (0x" + proc.ExitCode.ToString("X") + ")");
                                        MessageBox.Show(Translations.Database("Redistributable_VC_P7") + " " + proc.ExitCode.ToString() +
                                            " (0x" + proc.ExitCode.ToString("X") + ")" +
                                            "\n" + Translations.Database("Redistributable_VC_P8"),
                                            Translations.Database("Redistributable_VC_P5"), MessageBoxButtons.OK,
                                            MessageBoxIcon.Error);
                                    }

                                    proc.Close();
                                }
                            }
                            catch (Exception Error)
                            {
                                LogToFileAddons.OpenLog("REDISTRIBUTABLE x86 Process", string.Empty, Error, string.Empty, true);
                                Error_Free = false;
                                MessageBox.Show(Translations.Database("Redistributable_VC_P9"),
                                    Translations.Database("Redistributable_VC_P5"), MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                            }
                        }
                        else
                        {
                            Error_Free = false;
                            MessageBox.Show(Translations.Database("Redistributable_VC_P10"),
                                Translations.Database("Redistributable_VC_P5"), MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                        }
                    }
                    else
                    {
                        Error_Free = false;
                        MessageBox.Show(Translations.Database("Redistributable_VC_P8"), Translations.Database("Redistributable_VC_P5"), MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
                else
                {
                    Log.Info("REDISTRIBUTABLE: 32-bit 2015-2019 VC++ Redistributable Package is Installed");
                }

                if (Environment.Is64BitOperatingSystem)
                {
                    if (!RedistributablePackage.IsInstalled(RedistributablePackageVersion.VC2015to2019x64))
                    {
                        if (MessageBox.Show(Translations.Database("Redistributable_VC_64") +
                            "\n\n" + Translations.Database("Redistributable_VC_P2") +
                            "\n\n" + Translations.Database("Redistributable_VC_P3") +
                            "\n\n" + Translations.Database("Redistributable_VC_P4"),
                            Translations.Database("Redistributable_VC_P5"),
                            MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK)
                        {
                            try
                            {
                                Uri URLCall = new Uri("https://aka.ms/vs/16/release/VC_redist.x64.exe");
                                ServicePointManager.FindServicePoint(URLCall).ConnectionLeaseTimeout = (int)TimeSpan.FromMinutes(1).TotalMilliseconds;
                                var Client = new WebClient();

                                if (!Launcher_Value.Launcher_Alternative_Webcalls()) { Client = new WebClientWithTimeout(); }
                                else
                                {
                                    Client.Headers.Add("user-agent", "SBRW Launcher " +
                                    Application.ProductVersion + " (+https://github.com/SoapBoxRaceWorld/GameLauncher_NFSW)");
                                }

                                try
                                {
                                    Client.DownloadFile(URLCall, "VC_redist.x64.exe");
                                }
                                catch (WebException Error)
                                {
                                    API_Core.StatusCodes(URLCall.GetComponents(UriComponents.HttpRequestUrl, UriFormat.SafeUnescaped),
                                        Error, (HttpWebResponse)Error.Response);
                                }
                                catch (Exception Error)
                                {
                                    LogToFileAddons.OpenLog("REDISTRIBUTABLE", string.Empty, Error, string.Empty, true);
                                }
                                finally
                                {
                                    if (Client != null)
                                    {
                                        Client.Dispose();
                                    }
                                }
                            }
                            catch (Exception Error)
                            {
                                LogToFileAddons.OpenLog("REDISTRIBUTABLE x64", string.Empty, Error, string.Empty, true);
                            }

                            if (File.Exists("VC_redist.x64.exe"))
                            {
                                try
                                {
                                    var proc = Process.Start(new ProcessStartInfo
                                    {
                                        Verb = "runas",
                                        Arguments = "/quiet",
                                        FileName = "VC_redist.x64.exe"
                                    });

                                    proc.WaitForExit((int)TimeSpan.FromMinutes(10).TotalMilliseconds);

                                    if (proc == null)
                                    {
                                        Error_Free = false;
                                        MessageBox.Show(Translations.Database("Redistributable_VC_P6"),
                                            Translations.Database("Redistributable_VC_P5"), MessageBoxButtons.OK,
                                            MessageBoxIcon.Error);
                                    }
                                    else if (proc != null)
                                    {
                                        if (!proc.HasExited)
                                        {
                                            if (proc.Responding) { proc.CloseMainWindow(); }
                                            else { proc.Kill(); Error_Free = false; }
                                        }

                                        if (proc.ExitCode != 0)
                                        {
                                            Error_Free = false;
                                            Log.Error("REDISTRIBUTABLE INSTALLER [EXIT CODE]: " + proc.ExitCode.ToString() +
                                                " HEX: (0x" + proc.ExitCode.ToString("X") + ")");
                                            MessageBox.Show(Translations.Database("Redistributable_VC_P7") + " " + proc.ExitCode.ToString() +
                                                " (0x" + proc.ExitCode.ToString("X") + ")" +
                                                "\n" + Translations.Database("Redistributable_VC_P8"),
                                                Translations.Database("Redistributable_VC_P5"), MessageBoxButtons.OK,
                                                MessageBoxIcon.Error);
                                        }

                                        proc.Close();
                                    }
                                }
                                catch (Exception Error)
                                {
                                    LogToFileAddons.OpenLog("REDISTRIBUTABLE x64 Process", null, Error, null, true);
                                    Error_Free = false;
                                    MessageBox.Show(Translations.Database("Redistributable_VC_P9"),
                                        Translations.Database("Redistributable_VC_P5"), MessageBoxButtons.OK,
                                        MessageBoxIcon.Error);
                                }
                            }
                            else
                            {
                                Error_Free = false;
                                MessageBox.Show(Translations.Database("Redistributable_VC_P10"),
                                    Translations.Database("Redistributable_VC_P5"), MessageBoxButtons.OK,
                                        MessageBoxIcon.Error);
                            }
                        }
                        else
                        {
                            Error_Free = false;
                            MessageBox.Show(Translations.Database("Redistributable_VC_P8"),
                                Translations.Database("Redistributable_VC_P5"), MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }
                    }
                    else
                    {
                        Log.Info("REDISTRIBUTABLE: 64-bit 2015-2019 VC++ Redistributable Package is Installed");
                    }
                }

                Log.Completed("REDISTRIBUTABLE: Done");
            }

            Log.Info("LIST: Moved to Function");
            /* (Start Process) Sets Up Langauge List */
            LanguageListUpdater.GetList();
        }
    }
}
