﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32.TaskScheduler;

namespace handsoff
{
    class App : ApplicationContext
    {
        private const string HID_GUID = "{745a17a0-74d3-11d0-b6fe-00a0c90f57da}";

        private NotifyIcon appIcon;
        private ContextMenuStrip appMenu;
        private ToolStripMenuItem controlledDeviceMenuItem;
        private ToolStripMenuItem launchOnStartupMenuItem;
        private ToolStripMenuItem aboutMenuItem;
        private ToolStripMenuItem quitMenuItem;
        private List<Device> devices;
        private bool displayTutorial;

        public App()
        {
            /* TODO:
             *  - Error handling (mostly for device manager actions, make them fail silently)
             *   - "try" low-level operations
             *   - don't delete/modify stuff that doesn't exist
             *   - verify that objects are valid before executing actions on them
             *  - Restore LeanDeploy
             */

            Application.ApplicationExit += OnApplicationExit;

            displayTutorial = String.IsNullOrWhiteSpace(controlledDeviceID);

            InitializeComponent();
            appIcon.Visible = true;

            DisplayHelp();
        }

        private void InitializeComponent()
        {
            appIcon = new NotifyIcon();

            appIcon.Text = Application.ProductName;
            appIcon.MouseClick += OnAppClick;

            appMenu = new ContextMenuStrip();

            controlledDeviceMenuItem = new ToolStripMenuItem();
            launchOnStartupMenuItem = new ToolStripMenuItem();
            aboutMenuItem = new ToolStripMenuItem();
            quitMenuItem = new ToolStripMenuItem();
            appMenu.SuspendLayout();

            appMenu.Items.AddRange(new ToolStripItem[] { controlledDeviceMenuItem, launchOnStartupMenuItem, aboutMenuItem, new ToolStripSeparator(), quitMenuItem });

            controlledDeviceMenuItem.Text = "Controlled device";
            controlledDeviceMenuItem.DropDown.Opening += OnControlledDeviceOpening;
            UpdateDevicesList();

            launchOnStartupMenuItem.Text = "Launch on startup";
            launchOnStartupMenuItem.Checked = launchOnStartup;
            launchOnStartupMenuItem.MouseDown += OnStartupClick;

            aboutMenuItem.Text = "About...";
            aboutMenuItem.MouseDown += OnAboutClick;

            quitMenuItem.Text = "Quit";
            quitMenuItem.MouseDown += OnQuitClick;

            appMenu.ResumeLayout(false);
            appIcon.ContextMenuStrip = appMenu;

            UpdateIcon();
        }

        private void UpdateDevicesList()
        {
            ListDevices();

            DetectControlledDevice();

            controlledDeviceMenuItem.DropDown.Items.Clear();

            foreach(Device device in devices) 
            {
                ToolStripMenuItem deviceMenuItem = new ToolStripMenuItem(device.name);
                deviceMenuItem.Name = device.instancePath;
                deviceMenuItem.MouseDown += OnDeviceClick;

                if (device.instancePath == controlledDeviceID) {
                    deviceMenuItem.Checked = true;
                }

                controlledDeviceMenuItem.DropDown.Items.Add(deviceMenuItem);
            }
        }

        private void DetectControlledDevice()
        {
            if (String.IsNullOrWhiteSpace(controlledDeviceID))
            {
                Device defaultDevice = devices.FirstOrDefault(s => (s.name.Contains("touch") && (s.name.Contains("screen") || s.name.Contains("display"))));

                if (defaultDevice != null) {
                    controlledDeviceID = defaultDevice.instancePath;
                }
            }
        }

        private void ListDevices()
        {
            devices = new List<Device>();

            try
            {
                ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_PnPEntity Where ClassGuid = '" + HID_GUID + "'");

                foreach (ManagementObject queryObj in searcher.Get())
                {
                    devices.Add(new Device((String)queryObj["DeviceID"], (String)queryObj["Name"]/* + " (" + ((String)queryObj["Status"] == "OK" ? "Enabled" : "Disabled") + ")"*/));
                }

                if (devices.FirstOrDefault(s => s.instancePath == controlledDeviceID) == null) 
                {
                    controlledDeviceID = "";
                }

                devices.Sort();

            }
            catch (ManagementException e)
            {
                MessageBox.Show("An error occurred while querying for WMI data: " + e.Message);
            }

        }

        private static bool IsDeviceEnabled(string deviceID)
        {
            bool returnValue = false;

            if (!String.IsNullOrWhiteSpace(deviceID)) 
            {
                try
                {
                    ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_PnPEntity Where DeviceID = '" + deviceID.Replace("\\", "\\\\") + "'");

                    foreach (ManagementObject queryObj in searcher.Get())
                    {
                        if ((String)queryObj["Status"] == "OK")
                        {
                            returnValue = true;
                        }
                        break;
                    }
                }
                catch (ManagementException e)
                {
                    Console.WriteLine("An error occurred while querying for WMI data: " + e.Message);
                }
            }

            return returnValue;
        }

        private static void EnableDevice(string deviceID, bool enabled = true)
        {
            if (!String.IsNullOrWhiteSpace(deviceID))
            {
                try
                {
                    DisableHardware.DisableDevice(n => n.ToUpperInvariant().Contains(deviceID), !enabled);
                }
                catch (ApplicationException e)
                {
                    Console.WriteLine("An error occurred while enabling/disabling a device: " + e.Message);
                }
            }
        }

        private static bool ToggleDevice(string deviceID)
        {
            bool deviceEnabled = !IsDeviceEnabled(deviceID);

            EnableDevice(deviceID, deviceEnabled);

            return deviceEnabled;
        }

        private void DisplayHelp()
        {
            if (String.IsNullOrWhiteSpace(controlledDeviceID))
            {
                appIcon.ShowBalloonTip(5, Application.ProductName, "Touchscreen not found. Click or tap here to select the controlled device manually.", ToolTipIcon.Warning);
            }
            else if (displayTutorial)
            {
                appIcon.ShowBalloonTip(5, Application.ProductName, "Simply click or tap this icon to toggle your touchscreen. Right-click for options.", ToolTipIcon.Info);
                displayTutorial = false;
            }
        }

        private void UpdateIcon()
        {
            if (String.IsNullOrWhiteSpace(controlledDeviceID))
            {
                appIcon.Icon = new Icon(Properties.Resources.icon_warn, SystemInformation.SmallIconSize);
            }
            else if (IsDeviceEnabled(controlledDeviceID))
            {
                appIcon.Icon = new Icon(Properties.Resources.icon_on, SystemInformation.SmallIconSize);
            }
            else
            {
                appIcon.Icon = new Icon(Properties.Resources.icon_off, SystemInformation.SmallIconSize);
            }
        }

        private void ShowAppMenu()
        {
            MethodInfo mi = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
            mi.Invoke(appIcon, null);
        }

        private void OnAppClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) 
            {
                if (String.IsNullOrWhiteSpace(controlledDeviceID))
                {
                    ShowAppMenu();
                }
                else
                {
                    ToggleDevice(controlledDeviceID);
                    UpdateIcon();
                }
            }
        }

        private void OnControlledDeviceOpening(object sender, CancelEventArgs e)
        {
            UpdateDevicesList();
        }

        private void OnStartupClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                launchOnStartup = !launchOnStartupMenuItem.Checked;
                launchOnStartupMenuItem.Checked = launchOnStartup;
            }
        }

        private void OnAboutClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                appMenu.Close();
                aboutMenuItem.Enabled = false;

                Version version = new Version(Application.ProductVersion);
                int year = DateTime.Now.Year;

                MessageBox.Show(Application.ProductName + " " + version.Major + "." + version.Minor + " © 2014" + (year > 2014 ? " - " + year : "") + " Abdelmadjid Hammou.\nAll Rights Reserved.", "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
                aboutMenuItem.Enabled = true;
            }
        }

        private void OnQuitClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Application.Exit();
            }
        }

        private void OnDeviceClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                controlledDeviceID = ((ToolStripMenuItem)sender).Name;
                DisplayHelp();
            }
        }

        private void OnApplicationExit(object sender, EventArgs e)
        {
            //Cleanup so that the icon will be removed when the application is closed
            appIcon.Visible = false;
        }

        public static void OnUninstall()
        {
            launchOnStartup = false;
        }

        public static void OnProcessesKilled()
        {
            TrayIconBuster.TrayIconBuster.RemovePhantomIcons();
        }

        private string controlledDeviceID
        {
            get
            {
                return Properties.Settings.Default.controlledDevice;
            }

            set
            {
                Properties.Settings defaultSettings = Properties.Settings.Default;

                defaultSettings.controlledDevice = value;
                defaultSettings.Save();

                UpdateIcon();
            }
        }

        private static bool launchOnStartup
        {
            get
            {
                bool returnValue = false;

                TaskService taskService = new TaskService();

                if (taskService.GetTask(Application.ProductName) != null)
                {
                    returnValue = true;
                }

                return returnValue;
            }

            set
            {
                TaskService taskService = new TaskService();

                if (value)
                {
                    TaskDefinition taskDefinition = taskService.NewTask();
                    taskDefinition.RegistrationInfo.Description = "Launches " + Application.ProductName + " on startup.";

                    taskDefinition.Triggers.Add(new LogonTrigger());

                    taskDefinition.Principal.RunLevel = TaskRunLevel.Highest;

                    taskDefinition.Settings.DisallowStartIfOnBatteries = false;
                    taskDefinition.Settings.StopIfGoingOnBatteries = false;
                    taskDefinition.Settings.AllowHardTerminate = false;
                    taskDefinition.Settings.ExecutionTimeLimit = TimeSpan.Zero;

                    taskDefinition.Actions.Add(new ExecAction("\"" + LeanDeploy.installPath + "\""));

                    taskService.RootFolder.RegisterTaskDefinition(Application.ProductName, taskDefinition);
                }
                else
                {
                    if (taskService.GetTask(Application.ProductName) != null)
                    {
                        taskService.RootFolder.DeleteTask(Application.ProductName);
                    }
                }

            }
        }
    }

    public class Device : IComparable<Device>
    {
        public string instancePath { get; set; }
        public string name { get; set; }

        public Device(string _instancePath, string _name)
        {

            instancePath = _instancePath;
            name = _name;
        }

        public int CompareTo(Device other)
        {
            return name.CompareTo(other.name);
        }
    }
}
