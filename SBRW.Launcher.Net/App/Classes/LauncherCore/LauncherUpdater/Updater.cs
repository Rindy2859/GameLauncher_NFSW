﻿using SBRW.Launcher.App.Classes.InsiderKit;
using SBRW.Launcher.App.Classes.LauncherCore.APICheckers;
using SBRW.Launcher.App.Classes.LauncherCore.Global;
using SBRW.Launcher.App.Classes.LauncherCore.Languages.Visual_Forms;
using SBRW.Launcher.App.Classes.LauncherCore.Logger;
using Newtonsoft.Json;
using SBRW.Launcher.Core.Cache;
using SBRW.Launcher.Core.Extension.Api_;
using SBRW.Launcher.Core.Extension.Logging_;
using SBRW.Launcher.Core.Extension.Validation_.Json_.Newtonsoft_;
using SBRW.Launcher.Core.Extension.Web_;
using SBRW.Launcher.Core.Discord.RPC_;
using SBRW.Launcher.Core.Extra.File_;
using SBRW.Launcher.Core.Proxy.Nancy_;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;
using SBRW.Launcher.App.UI_Forms.Splash_Screen;
using SBRW.Launcher.App.UI_Forms.Update_Popup_Screen;
using SBRW.Launcher.Core.Theme;
using System.Net.Cache;

namespace SBRW.Launcher.App.Classes.LauncherCore.LauncherUpdater
{
    class LauncherUpdateCheck
    {
        public PictureBox status { get; set; }
        public Label text { get; set; }
        public Label description { get; set; }

        public static string CurrentLauncherBuild { get; set; } = Application.ProductVersion;
        public static string LatestLauncherBuild { get; set; }
        public static bool UpgradeAvailable { get; set; }
        private static bool SkipAvailableUpgrade { get; set; }
        public static string VersionJSON { get; set; }
        private static bool ValidJSONDownload { get; set; }
        public static int Revisions { get; set; }
        public static bool UpdatePopupStoppedSplashScreen { get; set; }

        public LauncherUpdateCheck(PictureBox statusImage, Label statusText, Label statusDescription)
        {
            status = statusImage;
            text = statusText;
            description = statusDescription;
        }

        public static void Latest()
        {
            Log.Checking("LAUNCHER UPDATE: Is Version Up to Date or not");
            Presence_Launcher.Status("Start Up", "Checking Latest Launcher Release Information");
            try
            {
                Uri URLCall = new Uri((EnableInsiderBetaTester.Allowed() || EnableInsiderDeveloper.Allowed()) ?
                    URLs.GitHub_Launcher_Beta : URLs.GitHub_Launcher_Stable);
                ServicePointManager.FindServicePoint(URLCall).ConnectionLeaseTimeout = (int)TimeSpan.FromMinutes(1).TotalMilliseconds;
                var Client = new WebClient
                {
                    Encoding = Encoding.UTF8,
                    CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore)
                };

                if (!Launcher_Value.Launcher_Alternative_Webcalls()) 
                { 
                    Client = new WebClientWithTimeout { Encoding = Encoding.UTF8, CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore) }; 
                }
                else
                {
                    Client.Headers.Add("user-agent", "SBRW Launcher " +
                    Application.ProductVersion + " (+https://github.com/SoapBoxRaceWorld/GameLauncher_NFSW)");
                }

                try
                {
                    VersionJSON = Client.DownloadString(URLCall);
                    VisualsAPIChecker.GitHubAPI = true;
                }
                catch (WebException Error)
                {
                    API_Core.StatusCodes(URLCall.GetComponents(UriComponents.HttpRequestUrl, UriFormat.SafeUnescaped),
                        Error, (HttpWebResponse)Error.Response);
                }
                catch (Exception Error)
                {
                    LogToFileAddons.OpenLog("LAUNCHER UPDATE [GITHUB]", string.Empty, Error, string.Empty, true);
                }
                finally
                {
                    if (Client != null)
                    {
                        Client.Dispose();
                    }
                }

                if (Is_Json.Valid(VersionJSON) && VisualsAPIChecker.GitHubAPI)
                {
                    LatestLauncherBuild = (EnableInsiderDeveloper.Allowed() || EnableInsiderBetaTester.Allowed()) ?
                        JsonConvert.DeserializeObject<List<GitHubRelease>>(VersionJSON)[0].TagName :
                        JsonConvert.DeserializeObject<GitHubRelease>(VersionJSON).TagName;
                    Log.Info("LAUNCHER UPDATE: GitHub Latest Version -> " + LatestLauncherBuild);
                    ValidJSONDownload = true;
                }
                else
                {
                    Log.Error("LAUNCHER UPDATE: Failed to retrieve Latest Build information from GitHub");
                    ValidJSONDownload = false;
                }
            }
            catch (Exception Error)
            {
                LogToFileAddons.OpenLog("LAUNCHER UPDATE [GITHUB]", string.Empty, Error, string.Empty, true);
            }

            Log.Completed("LAUNCHER UPDATE: Done");

            if (!UpdateStatusResult())
            {
                Log.Info("FIRST TIME RUN: Moved to Function");
                /* Do First Time Run Checks */
                FunctionStatus.FirstTimeRun();
            }
            else
            {
                if (Presence_Launcher.Running())
                {
                    Presence_Launcher.Stop("Close");
                }

                if (Proxy_Settings.Running())
                {
                    Proxy_Server.Instance.Stop("Force Close");
                }

                Application.Exit();
            }
        }

        private static bool UpdateStatusResult()
        {
            bool StatusUpdate = false;
            if (!string.IsNullOrWhiteSpace(LatestLauncherBuild))
            {
                Revisions = CurrentLauncherBuild.CompareTo(LatestLauncherBuild);

                if (Revisions < 0)
                {
                    Log.Info("LAUNCHER POPUP: Checking if Popup is Required");

                    if (Save_Settings.Live_Data.Update_Version_Skip != LatestLauncherBuild)
                    {
                        FunctionStatus.LoadingComplete = true;
                        Screen_Splash.ThreadStatus("Stop");
                        UpdatePopupStoppedSplashScreen = true;

                        DialogResult UserResult = new Screen_Update_Popup().ShowDialog();

                        if (UserResult == DialogResult.OK)
                        {
                            StatusUpdate = true;
                            string UpdaterPath = Path.Combine(Locations.LauncherFolder, Locations.NameUpdater);
                            if (File.Exists(UpdaterPath))
                            {
                                Process.Start(UpdaterPath, Process.GetCurrentProcess().Id.ToString() + " " +
                                    (EnableInsiderBetaTester.Allowed() || EnableInsiderDeveloper.Allowed() ? "Preview" : "Stable"));
                            }
                            else
                            {
                                Process.Start(@"https://github.com/SoapboxRaceWorld/GameLauncher_NFSW/releases/latest");
                                MessageBox.Show(null, Translations.Database("LauncherUpdateCheck_FS"), "GameLauncher", MessageBoxButtons.OK);
                            }
                        }
                        else if (UserResult == DialogResult.Ignore)
                        {
                            /* Save and Allow Version Update Skip Once user Reaches Main Screen */
                            SkipAvailableUpgrade = true;
                        }
                    }
                    else
                    {
                        Log.Completed("LAUNCHER POPUP: User Saved Skip Version Detected");
                    }
                }
                else
                {
                    Log.Completed("LAUNCHER POPUP: Update to Date");
                }
            }
            else
            {
                Log.Completed("LAUNCHER POPUP: Unable to run Update Popup (Null String)");
            }

            if (!string.IsNullOrWhiteSpace(VersionJSON))
            {
                VersionJSON = string.Empty;
            }

            return StatusUpdate;
        }

        public void ChangeVisualStatus()
        {
            if (!string.IsNullOrWhiteSpace(LatestLauncherBuild))
            {
                if (Revisions > 0)
                {
                    string WhatBuildAmI;
                    if (EnableInsiderDeveloper.Allowed())
                    {
                        WhatBuildAmI = Translations.Database("LauncherUpdateCheck_VS_Insider_Dev");
                    }
                    else if (EnableInsiderBetaTester.Allowed())
                    {
                        WhatBuildAmI = Translations.Database("LauncherUpdateCheck_VS_Insider_Beta");
                    }
                    else
                    {
                        WhatBuildAmI = Translations.Database("LauncherUpdateCheck_VS_Insider_Unofficial");
                    }

                    text.Text = Translations.Database("LauncherUpdateCheck_VS_Insider") + "\n - " + WhatBuildAmI +
                        " " + Translations.Database("LauncherUpdateCheck_VS_Insider_Text_Build");
                    status.BackgroundImage = Image_Icon.Engine_Warning;
                    text.ForeColor = Color_Text.S_Warning;
                    description.Text = Translations.Database("LauncherUpdateCheck_VS_Insider_Text_Stable") + " " + LatestLauncherBuild +
                        "\n" + Translations.Database("LauncherUpdateCheck_VS_Insider_Text_Current") + " " + Application.ProductVersion;

                    if (!string.IsNullOrWhiteSpace(Save_Settings.Live_Data.Update_Version_Skip))
                    {
                        Save_Settings.Live_Data.Update_Version_Skip = String.Empty;
                        Save_Settings.Save();
                        Log.Info("IGNOREUPDATEVERSION: Cleared OLD IgnoreUpdateVersion Build Number. " +
                            "You are currenly using a " + WhatBuildAmI + " Build!");
                    }
                }
                else if (Revisions == 0)
                {
                    text.Text = Translations.Database("LauncherUpdateCheck_VS_Text_No_Update");
                    status.BackgroundImage = Image_Icon.Engine_Good;
                    text.ForeColor = Color_Text.S_Sucess;
                    description.Text = Translations.Database("LauncherUpdateCheck_VS_Text_Version") + " " + Application.ProductVersion;

                    if (Save_Settings.Live_Data.Update_Version_Skip == Application.ProductVersion)
                    {
                        Save_Settings.Live_Data.Update_Version_Skip = String.Empty;
                        Save_Settings.Save();
                        Log.Info("IGNOREUPDATEVERSION: Cleared OLD IgnoreUpdateVersion Build Number. You're now on the Latest Game Launcher!");
                    }
                }
                else
                {
                    text.Text = Translations.Database("LauncherUpdateCheck_VS_Text_Update");
                    status.BackgroundImage = Image_Icon.Engine_Warning;
                    text.ForeColor = Color_Text.S_Warning;
                    description.Text = Translations.Database("LauncherUpdateCheck_VS_Text_Update_New") + " " + LatestLauncherBuild + "\n" +
                        Translations.Database("LauncherUpdateCheck_VS_Insider_Text_Current") + " " + Application.ProductVersion;
                    UpgradeAvailable = true;
                    if (SkipAvailableUpgrade)
                    {
                        Save_Settings.Live_Data.Update_Version_Skip = LatestLauncherBuild;
                        Save_Settings.Save();
                        Log.Info("IGNOREUPDATEVERSION: User had skipped latest Launcher Version!");
                    }
                }
            }
            else if (VisualsAPIChecker.GitHubAPI && !ValidJSONDownload)
            {
                text.Text = Translations.Database("LauncherUpdateCheck_VS_Text_Invalid_JSON");
                status.BackgroundImage = Image_Icon.Engine_Error;
                text.ForeColor = Color_Text.S_Error;
                description.Text = Translations.Database("LauncherUpdateCheck_VS_Text_Version") + " " + Application.ProductVersion;
            }
            else
            {
                text.Text = Translations.Database("LauncherUpdateCheck_VS_Text_Invalid_Error");
                status.BackgroundImage = Image_Icon.Engine_Unknown;
                text.ForeColor = Color_Text.L_Three;
                description.Text = Translations.Database("LauncherUpdateCheck_VS_Text_Version") + " " + Application.ProductVersion;
            }
        }
    }
}
