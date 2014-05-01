﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace handsoff
{
    class Main : ApplicationContext
    {
        private const string HID_GUID = "{745a17a0-74d3-11d0-b6fe-00a0c90f57da}";

        private NotifyIcon appIcon;
        private ContextMenuStrip appMenu;
        private ToolStripMenuItem controlledDeviceMenuItem;
        private ToolStripMenuItem launchOnStartupMenuItem;
        private ToolStripMenuItem aboutMenuItem;
        private ToolStripMenuItem quitMenuItem;
        private List<Device> devices;

        public Main()
        {
            /* TODO:
             *  - Launch on startup (UAC)
             *  - Clear startup registry entry on uninstall
             *  - Restore LeanDeploy
             *  - Rework firstrun scenario
             *  - Icon state for when no device is selected (yellow triangle warning sign)
             *  - Clear TrayIcon on uninstall
             */

            Application.ApplicationExit += OnApplicationExit;

            InitializeComponent();
            appIcon.Visible = true;

            appIcon.ShowBalloonTip(3, Application.ProductName, "Simply click or tap this icon to toggle your touchscreen.", ToolTipIcon.Info);
        }

        private void UpdateIcon()
        {
            if (IsDeviceEnabled(controlledDeviceID))
            {
                appIcon.Icon = new Icon(Properties.Resources.TouchOn, SystemInformation.SmallIconSize);
            }
            else
            {
                appIcon.Icon = new Icon(Properties.Resources.TouchOff, SystemInformation.SmallIconSize);
            }
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
            updateDevicesList();

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

        private void updateDevicesList()
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
                    devices.Add(new Device((String)queryObj["DeviceID"], (String)queryObj["Name"] + " (" + ((String)queryObj["Status"] == "OK" ? "Enabled" : "Disabled") + ")"));
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

        private bool IsDeviceEnabled(string deviceID)
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

        private void EnableDevice(string deviceID, bool enabled = true)
        {
            if (!String.IsNullOrWhiteSpace(deviceID))
            {
                DisableHardware.DisableDevice(n => n.ToUpperInvariant().Contains(deviceID), !enabled);
            }
        }

        private bool ToggleDevice(string deviceID)
        {
            bool deviceEnabled = !IsDeviceEnabled(deviceID);

            EnableDevice(deviceID, deviceEnabled);

            return deviceEnabled;
        }

        private void OnAppClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) 
            {
                ToggleDevice(controlledDeviceID);
                UpdateIcon();
            }
        }

        private void OnControlledDeviceOpening(object sender, CancelEventArgs e)
        {
            updateDevicesList();
        }

        private void OnStartupClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                launchOnStartup = launchOnStartupMenuItem.Checked = !launchOnStartupMenuItem.Checked;
            }
        }

        private void OnAboutClick(object sender, MouseEventArgs e)
        {
            // TODO: Fix this
            if (e.Button == MouseButtons.Left)
            {
                appMenu.Close();
                aboutMenuItem.Enabled = false;

                Version version = new Version(Application.ProductVersion);

                MessageBox.Show(Application.ProductName + " " + version.Major + "." + version.Minor + " © 2014 Abdelmadjid Hammou. All Rights Reserved.", "About", MessageBoxButtons.OK, MessageBoxIcon.None);
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
            }
        }

        private void OnApplicationExit(object sender, EventArgs e)
        {
            //Cleanup so that the icon will be removed when the application is closed
            appIcon.Visible = false;
        }

        private bool launchOnStartup
        {
            get 
            {
                bool returnValue = false;
                RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run");

                if (registryKey.GetValue(Application.ProductName) != null)
                {
                    returnValue = true;
                }

                registryKey.Close();

                return returnValue;
            }

            set 
            {
                RegistryKey registryKey = Registry.CurrentUser.CreateSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run");

                if (value) 
                {
                    registryKey.SetValue(Application.ProductName, LeanDeploy.installPath);
                } 
                else 
                {
                    registryKey.DeleteValue(Application.ProductName, false);
                }

                registryKey.Close();
            }
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
    }

    public delegate void BasicEvent(Object _e);

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
