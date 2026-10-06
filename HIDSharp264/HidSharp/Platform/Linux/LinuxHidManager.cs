#region License
/* Copyright 2012, 2017-2018 James F. Bellinger <http://software.seekye.com/hidsharp>

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

      http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing,
   software distributed under the License is distributed on an
   "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
   KIND, either express or implied.  See the License for the
   specific language governing permissions and limitations
   under the License. */
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace HidSharp.Platform.Linux
{
    sealed class LinuxHidManager : HidManager
    {
        protected override SystemEvents.EventManager CreateEventManager()
        {
            return new SystemEvents.LinuxEventManager();
        }

        static object _evfdLock = new object();
        static int _evfd = -1;
        unsafe static void Terminate(object sender, EventArgs e)
        {
            lock (_evfdLock)
            {
                int evfd = _evfd;
                if (evfd < 0) { return; }

                byte* value = stackalloc byte[8];
                value[0] = 1; // The actual value doesn't matter here, as long as it's not zero.

                IntPtr bytesWritten = NativeMethods.Retry(() => NativeMethods.write(evfd, (IntPtr)value, (UIntPtr)8));
                RunAssert(bytesWritten == (IntPtr)8, "We should never get here! eventfd did not behave as expected.");
            }
        }

        public override void ManualShutdown()
        {
            Terminate(null, null);
        }

        protected override unsafe void Run(Action readyCallback)
        {
            IntPtr udev = NativeMethodsLibudev.Instance.udev_new();
            RunAssert(udev != IntPtr.Zero, "HidSharp udev_new failed.");

            try
            {
                IntPtr monitor = NativeMethodsLibudev.Instance.udev_monitor_new_from_netlink(udev, "udev");
                RunAssert(monitor != IntPtr.Zero, "HidSharp udev_monitor_new_from_netlink failed.");

                try
                {
                    int ret;

                    ret = NativeMethodsLibudev.Instance.udev_monitor_filter_add_match_subsystem_devtype(monitor, "tty", null); // Serial
                    RunAssert(ret >= 0, "HidSharp udev_monitor_failed_add_match_subsystem_devtype (tty) failed.");

                    ret = NativeMethodsLibudev.Instance.udev_monitor_filter_add_match_subsystem_devtype(monitor, "hid", null); // USB HID
                    RunAssert(ret >= 0, "HidSharp udev_monitor_failed_add_match_subsystem_devtype (hid) failed.");

                    ret = NativeMethodsLibudev.Instance.udev_monitor_enable_receiving(monitor);
                    RunAssert(ret >= 0, "HidSharp udev_monitor_enable_receiving failed.");

                    int fd = NativeMethodsLibudev.Instance.udev_monitor_get_fd(monitor);
                    RunAssert(fd >= 0, "HidSharp udev_monitor_get_fd failed.");

                    int evfd = NativeMethods.eventfd();
                    RunAssert(evfd >= 0, "HidSharp eventfd failed.");
                    _evfd = evfd;

                    var pollfds = stackalloc NativeMethods.pollfd[2];
                    pollfds[0].fd = fd;
                    pollfds[0].events = NativeMethods.pollev.IN;
                    pollfds[1].fd = evfd;
                    pollfds[1].events = NativeMethods.pollev.IN;

                    if (AppDomain.CurrentDomain.IsDefaultAppDomain())
                    {
                        AppDomain.CurrentDomain.ProcessExit += Terminate;
                    }
                    else
                    {
                        AppDomain.CurrentDomain.DomainUnload += Terminate;
                    }

                    readyCallback();

                    try
                    {
                        while (true)
                        {
                            do { ret = NativeMethods.poll(pollfds, (IntPtr)2, -1); } while (NativeMethods.ShouldRetry(ret));
                            if (ret < 0) { break; }

                            if (ret > 0)
                            {
                                // Did we get a device change event?
                                if (0 != (pollfds[0].revents & (NativeMethods.pollev.ERR | NativeMethods.pollev.HUP | NativeMethods.pollev.NVAL))) { break; }
                                if (0 != (pollfds[0].revents & NativeMethods.pollev.IN))
                                {
                                    IntPtr device = NativeMethodsLibudev.Instance.udev_monitor_receive_device(monitor);
                                    if (device != null)
                                    {
                                        NativeMethodsLibudev.Instance.udev_device_unref(device);

                                        DeviceList.Local.RaiseChanged();
                                    }
                                }

                                // Did we get a shutdown request?
                                if (0 != (pollfds[1].revents & (NativeMethods.pollev.ERR | NativeMethods.pollev.HUP | NativeMethods.pollev.NVAL))) { break; }
                                if (0 != (pollfds[1].revents & NativeMethods.pollev.IN))
                                {
                                    break;
                                }
                            }
                        }
                    }
                    finally
                    {
                        lock (_evfdLock)
                        {
                            NativeMethods.Retry(() => NativeMethods.close(_evfd)); _evfd = -1;
                        }
                    }
                }
                finally
                {
                    NativeMethodsLibudev.Instance.udev_monitor_unref(monitor);
                }
            }
            finally
            {
                NativeMethodsLibudev.Instance.udev_unref(udev);
            }
        }

        protected override object[] GetBleDeviceKeys()
        {
            return new object[0];
        }

        protected override object[] GetHidDeviceKeys()
        {
            return GetDeviceKeys("hidraw");
        }

        protected override object[] GetSerialDeviceKeys()
        {
          //return GetDeviceKeys("tty"); // TODO: Find proper DevicePaths by enumerating tty.
            return LinuxSerialDevice.GetSerialDeviceKeys();
        }

        object[] GetDeviceKeys(string subsystem)
        {
            var paths = new List<string>();

            IntPtr udev = NativeMethodsLibudev.Instance.udev_new();
            if (IntPtr.Zero != udev)
            {
                try
                {
                    IntPtr enumerate = NativeMethodsLibudev.Instance.udev_enumerate_new(udev);
                    if (IntPtr.Zero != enumerate)
                    {
                        try
                        {
                            if (0 == NativeMethodsLibudev.Instance.udev_enumerate_add_match_subsystem(enumerate, subsystem) &&
                                0 == NativeMethodsLibudev.Instance.udev_enumerate_scan_devices(enumerate))
                            {
                                IntPtr entry;
                                for (entry = NativeMethodsLibudev.Instance.udev_enumerate_get_list_entry(enumerate); entry != IntPtr.Zero;
                                     entry = NativeMethodsLibudev.Instance.udev_list_entry_get_next(entry))
                                {
                                    string syspath = NativeMethodsLibudev.Instance.udev_list_entry_get_name(entry);

                                    if (syspath != null)
                                    {
                                        IntPtr device = NativeMethodsLibudev.Instance.udev_device_new_from_syspath(udev, syspath);
                                        if (device != null)
                                        {
                                            try
                                            {
                                                if (NativeMethodsLibudev.Instance.udev_device_get_is_initialized(device) > 0)
                                                {
                                                    paths.Add(syspath);
                                                }
                                            }
                                            finally
                                            {
                                                NativeMethodsLibudev.Instance.udev_device_unref(device);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        finally
                        {
                            NativeMethodsLibudev.Instance.udev_enumerate_unref(enumerate);
                        }
                    }
                }
                finally
                {
                    NativeMethodsLibudev.Instance.udev_unref(udev);
                }
            }

            return paths.Cast<object>().ToArray();
        }

        protected override bool TryCreateBleDevice(object key, out Device device)
        {
            throw new NotImplementedException();
        }

        protected override bool TryCreateHidDevice(object key, out Device device)
        {
            device = LinuxHidDevice.TryCreate((string)key);
            return device != null;
        }

        protected override bool TryCreateSerialDevice(object key, out Device device)
        {
            device = LinuxSerialDevice.TryCreate((string)key); return true;
        }

        public override string FriendlyName
        {
            get
            {
                var instance = NativeMethodsLibudev.Instance;
                return "Linux hidraw (" + (instance != null ? instance.FriendlyName : "?") + ")";
            }
        }

        public override bool IsSupported
        {
            get
            {
                if (Environment.OSVersion.Platform == PlatformID.Unix)
                {
                    try
                    {
                        string sysname; Version release;
                        if (NativeMethods.uname(out sysname, out release))
                        {
                            if (sysname == "Linux" && release >= new Version(2, 6, 36))
                            {
                                if (NativeMethodsLibudev.Instance != null)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {

                    }
                    finally
                    {

                    }
                }

                return false;
            }
        }
    }
}
