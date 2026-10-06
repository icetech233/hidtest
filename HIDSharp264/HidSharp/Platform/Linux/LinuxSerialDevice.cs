#region License
/* Copyright 2017-2018 James F. Bellinger <http://software.seekye.com/hidsharp>[

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
using System.IO;
using System.Linq;
using System.Reflection;

namespace HidSharp.Platform.Linux
{
    sealed class LinuxSerialDevice : SerialDevice
    {
        string _portName;
        string _manufacturer, _productName, _serialNumber;
        int _vid, _pid;

        protected override DeviceStream OpenDeviceDirectly(OpenConfiguration openConfig)
        {
            return new LinuxSerialStream(this);
        }

        internal static object[] GetSerialDeviceKeys()
        {
            try
            {
                return Directory.GetFiles("/dev/").Where(name =>
                    name.StartsWith("/dev/ttyACM") || name.StartsWith("/dev/ttyUSB")
                    ).Cast<object>().ToArray();
            }
            catch
            {
                return new object[0];
            }
        }

        internal static IntPtr GetUDevDeviceFromPortName(IntPtr udev, string portName)
        {
            if (portName.StartsWith("/dev/"))
            {
                string sysname = portName.Substring("/dev/".Length);
                IntPtr device = NativeMethodsLibudev.Instance.udev_device_new_from_subsystem_sysname(udev, "tty", sysname);
                return device;
            }

            return IntPtr.Zero;
        }

        internal static LinuxSerialDevice TryCreate(string portName)
        {
            string manufacturer = null, productName = null, serialNumber = null;
            int vendorID = -1, productID = -1;

            IntPtr udev = NativeMethodsLibudev.Instance.udev_new();
            if (IntPtr.Zero != udev)
            {
                try
                {
                    IntPtr device = GetUDevDeviceFromPortName(udev, portName);
                    if (device != IntPtr.Zero)
                    {
                        try
                        {
                            if (NativeMethodsLibudev.Instance.udev_device_get_is_initialized(device) > 0)
                            {
                                IntPtr parent = NativeMethodsLibudev.Instance.udev_device_get_parent_with_subsystem_devtype(device, "usb", "usb_device");
                                if (IntPtr.Zero != parent)
                                {
                                    manufacturer = NativeMethodsLibudev.Instance.udev_device_get_sysattr_value(parent, "manufacturer");
                                    productName = NativeMethodsLibudev.Instance.udev_device_get_sysattr_value(parent, "product");
                                    serialNumber = NativeMethodsLibudev.Instance.udev_device_get_sysattr_value(parent, "serial");

                                    string idVendor = NativeMethodsLibudev.Instance.udev_device_get_sysattr_value(parent, "idVendor");
                                    string idProduct = NativeMethodsLibudev.Instance.udev_device_get_sysattr_value(parent, "idProduct");

                                    int vid, pid;
                                    if (NativeMethods.TryParseHex(idVendor, out vid) &&
                                        NativeMethods.TryParseHex(idProduct, out pid))
                                    {
                                        vendorID = vid;
                                        productID = pid;
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

            return new LinuxSerialDevice()
            {
                _portName = portName,
                _manufacturer = manufacturer,
                _productName = productName,
                _serialNumber = serialNumber,
                _vid = vendorID,
                _pid = productID
            };
        }

        public override string GetFileSystemName()
        {
            return _portName;
        }

        public override bool HasImplementationDetail(Guid detail)
        {
            return base.HasImplementationDetail(detail) || detail == ImplementationDetail.Linux;
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

        public override string DevicePath
        {
            get { return _portName; }
        }

        public override int VendorID
        {
            get { return _vid; }
        }

        public override int ProductID
        {
            get { return _pid; }
        }
    }
}
