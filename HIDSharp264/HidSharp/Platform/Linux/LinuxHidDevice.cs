#region License
/* Copyright 2012-2015, 2017, 2024 James F. Bellinger <http://software.seekye.com/hidsharp>

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
using System.IO;
using System.Runtime.InteropServices;
using HidSharp.Exceptions;

namespace HidSharp.Platform.Linux
{
    sealed class LinuxHidDevice : HidDevice
    {
        object _getInfoLock;
        string _manufacturer;
        string _productName;
        string _serialNumber;
        byte[] _reportDescriptor;
        int _vid, _pid, _version;
        int _maxInput, _maxOutput, _maxFeature;
        bool _reportsUseID;
        string _path, _fileSystemName;
        string _hubPath;
        int _hubPortNumber;

        LinuxHidDevice()
        {
            _getInfoLock = new object();
        }

        internal static LinuxHidDevice TryCreate(string path)
        {
            var d = new LinuxHidDevice() { _path = path };

            IntPtr udev = NativeMethodsLibudev.Instance.udev_new();
            if (IntPtr.Zero != udev)
            {
                try
                {
                    IntPtr device = NativeMethodsLibudev.Instance.udev_device_new_from_syspath(udev, d._path);
                    if (device != IntPtr.Zero)
                    {
                        try
                        {
                            string devnode = NativeMethodsLibudev.Instance.udev_device_get_devnode(device);
                            if (devnode != null)
                            {
                                d._fileSystemName = devnode;

                                if (NativeMethodsLibudev.Instance.udev_device_get_is_initialized(device) > 0)
                                {
                                    IntPtr parent = NativeMethodsLibudev.Instance.udev_device_get_parent_with_subsystem_devtype(device, "usb", "usb_device");
                                    if (IntPtr.Zero != parent)
                                    {
                                        string manufacturer = NativeMethodsLibudev.Instance.udev_device_get_sysattr_value(parent, "manufacturer");
                                        string productName = NativeMethodsLibudev.Instance.udev_device_get_sysattr_value(parent, "product");
                                        string serialNumber = NativeMethodsLibudev.Instance.udev_device_get_sysattr_value(parent, "serial");
                                        string idVendor = NativeMethodsLibudev.Instance.udev_device_get_sysattr_value(parent, "idVendor");
                                        string idProduct = NativeMethodsLibudev.Instance.udev_device_get_sysattr_value(parent, "idProduct");
                                        string bcdDevice = NativeMethodsLibudev.Instance.udev_device_get_sysattr_value(parent, "bcdDevice");

                                        int vid, pid, version;
                                        if (NativeMethods.TryParseHex(idVendor, out vid) &&
                                            NativeMethods.TryParseHex(idProduct, out pid) &&
                                            NativeMethods.TryParseHex(bcdDevice, out version))
                                        {
                                            d._vid = vid;
                                            d._pid = pid;
                                            d._version = version;
                                            d._manufacturer = manufacturer;
                                            d._productName = productName;
                                            d._serialNumber = serialNumber;

                                            // While we're here, let's check about the USB hub it's attached to.
                                            IntPtr hub = NativeMethodsLibudev.Instance.udev_device_get_parent(parent); // _with_subsystem_devtype(parent, "usb", "usb_device");
                                            if (hub != IntPtr.Zero)
                                            {
                                                string hubPath = NativeMethodsLibudev.Instance.udev_device_get_syspath(hub);
                                                if (hubPath != null)
                                                {
                                                    string bDeviceClass = NativeMethodsLibudev.Instance.udev_device_get_sysattr_value(hub, "bDeviceClass");
                                                    if (bDeviceClass == "09") // USB hub
                                                    {
                                                        int hubPort;
                                                        var hubPortStr = NativeMethodsLibudev.Instance.udev_device_get_sysattr_value(parent, "devpath");
                                                        if (hubPortStr != null)
                                                        {
                                                            hubPortStr = hubPortStr.Substring(hubPortStr.LastIndexOf('.') + 1);
                                                            if (int.TryParse(hubPortStr, out hubPort) && hubPort >= 0)
                                                            {
                                                                d._hubPath = hubPath;
                                                                d._hubPortNumber = hubPort;
                                                            }
                                                        }
                                                    }
                                                }
                                            }

                                            return d;
                                        }
                                    }
                                }
                            }
                        }
                        finally
                        {
                            NativeMethodsLibudev.Instance.udev_device_unref(device);
                        }
                    }
                }
                finally
                {
                    NativeMethodsLibudev.Instance.udev_unref(udev);
                }
            }

            return null;
        }

        public override string[] GetSerialPorts()
        {
            var serialDeviceKeys = LinuxSerialDevice.GetSerialDeviceKeys();

            var ports = new List<string>();

            IntPtr udev = NativeMethodsLibudev.Instance.udev_new();
            if (IntPtr.Zero != udev)
            {
                try
                {
                    IntPtr device = NativeMethodsLibudev.Instance.udev_device_new_from_syspath(udev, _path);
                    if (device != IntPtr.Zero)
                    {
                        try
                        {
                            if (NativeMethodsLibudev.Instance.udev_device_get_is_initialized(device) > 0)
                            {
                                IntPtr parent = NativeMethodsLibudev.Instance.udev_device_get_parent_with_subsystem_devtype(device, "usb", "usb_device");
                                if (IntPtr.Zero != parent)
                                {
                                    IntPtr enumerate = NativeMethodsLibudev.Instance.udev_enumerate_new(udev);
                                    if (IntPtr.Zero != enumerate)
                                    {
                                        try
                                        {
                                            if (0 == NativeMethodsLibudev.Instance.udev_enumerate_add_match_parent(enumerate, parent) &&
                                                0 == NativeMethodsLibudev.Instance.udev_enumerate_add_match_subsystem(enumerate, "tty") &&
                                                0 == NativeMethodsLibudev.Instance.udev_enumerate_scan_devices(enumerate))
                                            {
                                                IntPtr entry;
                                                for (entry = NativeMethodsLibudev.Instance.udev_enumerate_get_list_entry(enumerate); entry != IntPtr.Zero;
                                                     entry = NativeMethodsLibudev.Instance.udev_list_entry_get_next(entry))
                                                {
                                                    string syspath = NativeMethodsLibudev.Instance.udev_list_entry_get_name(entry);
                                                    if (syspath != null)
                                                    {
                                                        foreach (string serialPortName in serialDeviceKeys)
                                                        {
                                                            IntPtr serialUDev = LinuxSerialDevice.GetUDevDeviceFromPortName(udev, serialPortName);

                                                            if (serialUDev != IntPtr.Zero)
                                                            {
                                                                string devnode = NativeMethodsLibudev.Instance.udev_device_get_devnode(serialUDev);

                                                                if (devnode != null)
                                                                {
                                                                    ports.Add(serialPortName);
                                                                }
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
                            }
                        }
                        finally
                        {
                            NativeMethodsLibudev.Instance.udev_device_unref(device);
                        }
                    }
                }
                finally
                {
                    NativeMethodsLibudev.Instance.udev_unref(udev);
                }
            }

            return ports.ToArray();
        }

        sealed class LinuxUsbHub : UsbPort
        {
            string _devicePath; int _portNumber;

            public LinuxUsbHub(string devicePath, int portNumber)
            {
                _devicePath = devicePath; _portNumber = portNumber;
            }

            public override string HubDevicePath
            {
                get { return _devicePath; }
            }

            public override int PortNumber
            {
                get { return _portNumber; }
            }
        }

        LinuxUsbHub _hub;
        public override UsbPort GetUsbPort()
        {
            if (_hub != null) { return _hub; }

            if (_hubPath != null)
            {
                _hub = new LinuxUsbHub(_hubPath, _hubPortNumber);
                return _hub;
            }

            return base.GetUsbPort();
        }

        protected override DeviceStream OpenDeviceDirectly(OpenConfiguration openConfig)
        {
            RequiresGetInfo();

            var stream = new LinuxHidStream(this);
            try { stream.Init(_path); return stream; }
            catch { stream.Close(); throw; }
        }

        public override string GetManufacturer(GetStringFlags flags)
        {
            if (_manufacturer == null) { throw DeviceException.CreateIOException(this, "Unnamed manufacturer."); }
            return _manufacturer;
        }

        public override string GetProductName(GetStringFlags flags)
        {
            if (_productName == null) { throw DeviceException.CreateIOException(this, "Unnamed product."); }
            return _productName;
        }

        public override string GetSerialNumber(GetStringFlags flags)
        {
            if (_serialNumber == null) { throw DeviceException.CreateIOException(this, "No serial number."); }
            return _serialNumber;
        }

        public override int GetMaxInputReportLength()
        {
            RequiresGetInfo();
            return _maxInput;
        }

        public override int GetMaxOutputReportLength()
        {
            RequiresGetInfo();
            return _maxOutput;
        }

        public override int GetMaxFeatureReportLength()
        {
            RequiresGetInfo();
            return _maxFeature;
        }

        public override byte[] GetRawReportDescriptor()
        {
            RequiresGetInfo();
            return (byte[])_reportDescriptor.Clone();
        }

        bool TryParseReportDescriptor(out Reports.ReportDescriptor parser, out byte[] reportDescriptor)
        {
            parser = null; reportDescriptor = null;

            int handle;
            try { handle = LinuxHidStream.DeviceHandleFromPath(_path, this, NativeMethods.oflag.NONBLOCK); }
            catch (FileNotFoundException) { throw DeviceException.CreateIOException(this, "Failed to read report descriptor."); }

            try
            {
                uint descsize;
                if (NativeMethods.ioctl(handle, NativeMethods.HIDIOCGRDESCSIZE, out descsize) < 0) { return false; }
                if (descsize > NativeMethods.HID_MAX_DESCRIPTOR_SIZE) { return false; }

                var desc = new NativeMethods.hidraw_report_descriptor() { size = descsize };
                if (NativeMethods.ioctl(handle, NativeMethods.HIDIOCGRDESC, ref desc) < 0) { return false; }

                Array.Resize(ref desc.value, (int)descsize);
                parser = new Reports.ReportDescriptor(desc.value);
                reportDescriptor = desc.value; return true;
            }
            finally
            {
                NativeMethods.Retry(() => NativeMethods.close(handle));
            }
        }

        void RequiresGetInfo()
        {
            lock (_getInfoLock)
            {
                if (_reportDescriptor != null) { return; }

                Reports.ReportDescriptor parser; byte[] reportDescriptor;
                if (!TryParseReportDescriptor(out parser, out reportDescriptor))
                {
                    throw DeviceException.CreateIOException(this, "Failed to read report descriptor.");
                }

                _maxInput = parser.MaxInputReportLength;
                _maxOutput = parser.MaxOutputReportLength;
                _maxFeature = parser.MaxFeatureReportLength;
                _reportsUseID = parser.ReportsUseID;
                _reportDescriptor = reportDescriptor;
            }
        }

        public override string GetFileSystemName()
        {
            return _fileSystemName;
        }

        public override bool HasImplementationDetail(Guid detail)
        {
            return base.HasImplementationDetail(detail) || detail == ImplementationDetail.Linux || detail == ImplementationDetail.HidrawApi;
        }

        public override string DevicePath
        {
            get { return _path; }
        }

        public override int VendorID
        {
            get { return _vid; }
        }

        public override int ProductID
        {
            get { return _pid; }
        }

        public override int ReleaseNumberBcd
        {
            get { return _version; }
        }

        internal bool ReportsUseID
        {
            get { return _reportsUseID; }
        }
    }
}
