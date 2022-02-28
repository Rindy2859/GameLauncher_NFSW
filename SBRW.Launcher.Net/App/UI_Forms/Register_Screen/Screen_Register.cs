﻿using SBRW.Launcher.App.Classes.Auth;
using SBRW.Launcher.App.Classes.LauncherCore.Client.Auth;
using SBRW.Launcher.App.Classes.LauncherCore.Global;
using SBRW.Launcher.App.Classes.LauncherCore.Lists;
using SBRW.Launcher.App.Classes.LauncherCore.Logger;
using SBRW.Launcher.App.Classes.LauncherCore.Support;
using SBRW.Launcher.App.Classes.SystemPlatform.Unix;
using SBRW.Launcher.Core.Cache;
using SBRW.Launcher.Core.Discord.RPC_;
using SBRW.Launcher.Core.Extension.Api_;
using SBRW.Launcher.Core.Extension.Hash_;
using SBRW.Launcher.Core.Extension.Logging_;
using SBRW.Launcher.Core.Extension.Validation_;
using SBRW.Launcher.Core.Extension.Web_;
using SBRW.Launcher.Core.Theme;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Net.Cache;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace SBRW.Launcher.App.UI_Forms.Register_Screen
{
    public partial class Screen_Register : Form
    {
        private static bool IsRegisterScreenOpen { get; set; }
        private bool Ticket_Required { get; set; }

        private void Button_Register_Click(object sender, EventArgs e)
        {
            Refresh();

            List<string> registerErrors = new List<string>();

            if (string.IsNullOrWhiteSpace(Input_Email.Text))
            {
                registerErrors.Add("Please enter your e-mail.");
                Picture_Input_Email.Image = Image_Other.Text_Border_Email_Error;
            }
            else if (!Is_Email.Valid(Input_Email.Text))
            {
                registerErrors.Add("Please enter a valid e-mail address.");
                Picture_Input_Email.Image = Image_Other.Text_Border_Email_Error;
            }

            if (string.IsNullOrWhiteSpace(Input_Ticket.Text) && Ticket_Required)
            {
                registerErrors.Add("Please enter your ticket.");
                Picture_Input_Ticket.Image = Image_Other.Text_Border_Ticket_Error;
            }

            if (string.IsNullOrWhiteSpace(Input_Password.Text))
            {
                registerErrors.Add("Please enter your password.");
                Picture_Input_Password.Image = Image_Other.Text_Border_Password_Error;
            }

            if (string.IsNullOrWhiteSpace(Input_Password_Confirm.Text))
            {
                registerErrors.Add("Please confirm your password.");
                Picture_Input_Password_Confirm.Image = Image_Other.Text_Border_Password_Error;
            }

            if (Input_Password_Confirm.Text != Input_Password.Text)
            {
                registerErrors.Add("Passwords don't match.");
                Picture_Input_Password_Confirm.Image = Image_Other.Text_Border_Password_Error;
            }

            if (!CheckBox_Rules_Agreement.Checked)
            {
                registerErrors.Add("You have not agreed to the Terms of Service.");
                CheckBox_Rules_Agreement.ForeColor = Color_Text.S_Error;
            }

            if (registerErrors.Count == 0)
            {
                bool allowReg = false;

                string Email;
                string Password;

                switch (Authentication.HashType(Launcher_Value.Launcher_Select_Server_JSON.Server_Authentication_Version ?? string.Empty))
                {
                    case AuthHash.H10:
                        Email = Input_Email.Text.ToString();
                        Password = Input_Email.Text.ToString();
                        break;
                    case AuthHash.H11:
                        Email = Input_Email.Text.ToString();
                        Password = Hashes.Hash_String(0, Input_Password.Text.ToString()).ToLower();
                        break;
                    case AuthHash.H12:
                        Email = Input_Email.Text.ToString();
                        Password = Hashes.Hash_String(1, Input_Password.Text.ToString()).ToLower();
                        break;
                    case AuthHash.H13:
                        Email = Input_Email.Text.ToString();
                        Password = Hashes.Hash_String(2, Input_Password.Text.ToString()).ToLower();
                        break;
                    case AuthHash.H20:
                        Email = Hashes.Hash_String(0, Input_Email.Text.ToString()).ToLower();
                        Password = Hashes.Hash_String(0, Input_Password.Text.ToString()).ToLower();
                        break;
                    case AuthHash.H21:
                        Email = Hashes.Hash_String(1, Input_Email.Text.ToString()).ToLower();
                        Password = Hashes.Hash_String(1, Input_Password.Text.ToString()).ToLower();
                        break;
                    case AuthHash.H22:
                        Email = Hashes.Hash_String(2, Input_Email.Text.ToString()).ToLower();
                        Password = Hashes.Hash_String(2, Input_Password.Text.ToString()).ToLower();
                        break;
                    default:
                        Log.Error("HASH TYPE: Unknown Hash Standard was Provided");
                        return;
                }

                try
                {
                    string[] regex = new Regex(@"([0-9A-Z]{5})([0-9A-Z]{35})").Split(Password.ToUpper());

                    Uri URLCall = new Uri("https://api.pwnedpasswords.com/range/" + regex[1]);
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

                    String serverReply = null;
                    try
                    {
                        serverReply = Client.DownloadString(URLCall);
                    }
                    catch (WebException Error)
                    {
                        API_Core.StatusCodes(URLCall.GetComponents(UriComponents.HttpRequestUrl, UriFormat.SafeUnescaped),
                            Error, (HttpWebResponse)Error.Response);
                    }
                    catch (Exception Error)
                    {
                        LogToFileAddons.OpenLog("Register", string.Empty, Error, string.Empty, true);
                    }
                    finally
                    {
                        if (Client != null)
                        {
                            Client.Dispose();
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(serverReply))
                    {
                        String verify = regex[2];

                        string[] hashes = serverReply.Split('\n');
                        foreach (string hash in hashes)
                        {
                            var splitChecks = hash.Split(':');
                            if (splitChecks[0] == verify)
                            {
                                var passwordCheckReply = MessageBox.Show(null, "Password used for registration has been breached " + Convert.ToInt32(splitChecks[1]) +
                                    " times, you should consider using a different one.\n\nAlternatively you can use the unsafe password anyway." +
                                    "\nWould you like to continue to use it?", "GameLauncher", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                                if (passwordCheckReply == DialogResult.Yes)
                                {
                                    allowReg = true;
                                }
                                else
                                {
                                    allowReg = false;
                                }
                            }
                            else
                            {
                                allowReg = true;
                            }
                        }
                    }
                    else
                    {
                        allowReg = true;
                    }
                }
                catch
                {
                    allowReg = true;
                }

                if (allowReg)
                {
                    Tokens.Clear();

                    Tokens.IPAddress = Launcher_Value.Launcher_Select_Server_Data.IPAddress;
                    Tokens.ServerName = ServerListUpdater.ServerName("Register");

                    Authentication.Client("Register", Launcher_Value.Launcher_Select_Server_JSON.Server_Authentication_Post, Email, Password, Ticket_Required ? Input_Ticket.Text : null);

                    if (!String.IsNullOrWhiteSpace(Tokens.Success))
                    {
                        DialogResult Success = MessageBox.Show(null, Tokens.Success, "GameLauncher", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                        if (Success == DialogResult.OK)
                        {
                            Close();
                        }
                    }
                    else
                    {
                        MessageBox.Show(null, Tokens.Error, "GameLauncher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    var message = "There were some errors while registering. Please fix them:\n\n";

                    foreach (var error in registerErrors)
                    {
                        message += "• " + error + "\n";
                    }

                    MessageBox.Show(null, message, "GameLauncher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void Greenbutton_hover_MouseEnter(object sender, EventArgs e)
        {
            Button_Register.BackgroundImage = Image_Button.Green_Hover;
        }

        private void Greenbutton_MouseLeave(object sender, EventArgs e)
        {
            Button_Register.BackgroundImage = Image_Button.Green;
        }

        private void Greenbutton_hover_MouseUp(object sender, EventArgs e)
        {
            Button_Register.BackgroundImage = Image_Button.Green_Hover;
        }

        private void Greenbutton_click_MouseDown(object sender, EventArgs e)
        {
            Button_Register.BackgroundImage = Image_Button.Green_Click;
        }

        private void CheckBox_Rules_Agreement_CheckedChanged(object sender, EventArgs e)
        {
            CheckBox_Rules_Agreement.ForeColor = Color_Text.L_Five;
        }

        private void Input_Email_TextChanged(object sender, EventArgs e)
        {
            Picture_Input_Email.Image = Image_Other.Text_Border_Email;
        }

        private void Input_Ticket_TextChanged(object sender, EventArgs e)
        {
            Picture_Input_Ticket.Image = Image_Other.Text_Border_Ticket;
        }

        private void Input_Password_Confirm_TextChanged(object sender, EventArgs e)
        {
            Picture_Input_Password_Confirm.Image = Image_Other.Text_Border_Password;
        }

        private void Input_Password_TextChanged(object sender, EventArgs e)
        {
            Picture_Input_Password.Image = Image_Other.Text_Border_Password;
        }

        private void Graybutton_click_MouseDown(object sender, EventArgs e)
        {
            Button_Cancel.BackgroundImage = Image_Button.Grey_Click;
        }

        private void Graybutton_hover_MouseEnter(object sender, EventArgs e)
        {
            Button_Cancel.BackgroundImage = Image_Button.Grey_Hover;
        }

        private void Graybutton_MouseLeave(object sender, EventArgs e)
        {
            Button_Cancel.BackgroundImage = Image_Button.Grey;
        }

        private void Graybutton_hover_MouseUp(object sender, EventArgs e)
        {
            Button_Cancel.BackgroundImage = Image_Button.Grey_Hover;
        }

        private void Button_Cancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void SetVisuals()
        {
            /*******************************/
            /* Set Window Name              /
            /*******************************/

            Text = "Register - SBRW Launcher: v" + Application.ProductVersion;

            /*******************************/
            /* Set Initial position & Icon  /
            /*******************************/

            FunctionStatus.CenterParent(this);

            /*******************************/
            /* Set Font                     /
            /*******************************/

            float MainFontSize = UnixOS.Detected() ? 9f : 9f * 96f / CreateGraphics().DpiY;
            float SecondaryFontSize = UnixOS.Detected() ? 8f : 8f * 96f / CreateGraphics().DpiY;
            Font = new Font(FormsFont.Primary(), SecondaryFontSize, FontStyle.Regular);

            /* Registering Panel */
            Input_Email.Font = new Font(FormsFont.Primary(), MainFontSize, FontStyle.Regular);
            Input_Password.Font = new Font(FormsFont.Primary(), MainFontSize, FontStyle.Regular);
            Input_Password_Confirm.Font = new Font(FormsFont.Primary(), MainFontSize, FontStyle.Regular);
            Input_Ticket.Font = new Font(FormsFont.Primary(), MainFontSize, FontStyle.Regular);
            CheckBox_Rules_Agreement.Font = new Font(FormsFont.Primary_Bold(), MainFontSize, FontStyle.Bold);
            Button_Register.Font = new Font(FormsFont.Primary_Bold(), MainFontSize, FontStyle.Bold);
            Button_Cancel.Font = new Font(FormsFont.Primary_Bold(), MainFontSize, FontStyle.Bold);
            Label_Information_Window.Font = new Font(FormsFont.Primary_Bold(), MainFontSize, FontStyle.Bold);

            /********************************/
            /* Set Theme Colors & Images     /
            /********************************/

            /* Set Background with Transparent Key */
            BackgroundImage = Image_Background.Registration;
            TransparencyKey = Color_Screen.BG_Registration;

            Label_Information_Window.ForeColor = Color_Text.L_Five;

            Input_Email.BackColor = Color_Winform_Other.Input;
            Input_Email.ForeColor = Color_Text.L_Five;
            Picture_Input_Email.Image = Image_Other.Text_Border_Email;

            Picture_Input_Password.Image = Image_Other.Text_Border_Password;
            Input_Password.BackColor = Color_Winform_Other.Input;
            Input_Password.ForeColor = Color_Text.L_Five;

            Picture_Input_Password_Confirm.Image = Image_Other.Text_Border_Password;
            Input_Password_Confirm.BackColor = Color_Winform_Other.Input;
            Input_Password_Confirm.ForeColor = Color_Text.L_Five;

            Picture_Input_Ticket.Image = Image_Other.Text_Border_Ticket;
            Input_Ticket.BackColor = Color_Winform_Other.Input;
            Input_Ticket.ForeColor = Color_Text.L_Five;

            CheckBox_Rules_Agreement.ForeColor = Color_Winform.Warning_Text_Fore_Color;

            Button_Register.BackgroundImage = Image_Button.Green;
            Button_Register.ForeColor = Color_Text.L_Seven;

            Button_Cancel.BackgroundImage = Image_Button.Grey;
            Button_Cancel.ForeColor = Color_Text.L_Five;

            /********************************/
            /* Events                        /
            /********************************/

            Input_Email.TextChanged += new EventHandler(Input_Email_TextChanged);
            Input_Password.TextChanged += new EventHandler(Input_Password_TextChanged);
            Input_Password_Confirm.TextChanged += new EventHandler(Input_Password_Confirm_TextChanged);
            Input_Ticket.TextChanged += new EventHandler(Input_Ticket_TextChanged);
            CheckBox_Rules_Agreement.CheckedChanged += new EventHandler(CheckBox_Rules_Agreement_CheckedChanged);

            Button_Register.MouseEnter += Greenbutton_hover_MouseEnter;
            Button_Register.MouseLeave += Greenbutton_MouseLeave;
            Button_Register.MouseUp += Greenbutton_hover_MouseUp;
            Button_Register.MouseDown += Greenbutton_click_MouseDown;
            Button_Register.Click += Button_Register_Click;

            Button_Cancel.MouseEnter += new EventHandler(Graybutton_hover_MouseEnter);
            Button_Cancel.MouseLeave += new EventHandler(Graybutton_MouseLeave);
            Button_Cancel.MouseUp += new MouseEventHandler(Graybutton_hover_MouseUp);
            Button_Cancel.MouseDown += new MouseEventHandler(Graybutton_click_MouseDown);
            Button_Cancel.Click += new EventHandler(Button_Cancel_Click);

            /********************************/
            /* Functions                     /
            /********************************/

            Label_Information_Window.Text = "REGISTER ON \n" + ServerListUpdater.ServerName("Register").ToUpper();
            Ticket_Required = Launcher_Value.Launcher_Select_Server_JSON.Server_Registration_Token;
            /* Show Ticket Box if its Required  */
            Input_Ticket.Visible = Ticket_Required;
            Picture_Input_Ticket.Visible = Ticket_Required;
        }

        public static void OpenScreen()
        {
            if (IsRegisterScreenOpen || Application.OpenForms["Screen_Register"] != null)
            {
                if (Application.OpenForms["Screen_Register"] != null) { Application.OpenForms["Screen_Register"].Activate(); }
            }
            else
            {
                try { new Screen_Register().ShowDialog(); }
                catch (Exception Error)
                {
                    string ErrorMessage = "Register Screen Encountered an Error";
                    LogToFileAddons.OpenLog("Register Screen", ErrorMessage, Error, "Exclamation", false);
                }
            }
        }

        public Screen_Register()
        {
            IsRegisterScreenOpen = true;
            InitializeComponent();
            SetVisuals();
            Presence_Launcher.Status("Register", ServerListUpdater.ServerName("Register"));
            this.Closing += (x, y) =>
            {
                Presence_Launcher.Status("Idle Ready", null);
                IsRegisterScreenOpen = false;
                GC.Collect();
            };
        }
    }
}
