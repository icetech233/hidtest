#region License
/* Copyright 2017-2018 James F. Bellinger <http://software.seekye.com/hidsharp>

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
using System.Runtime.InteropServices;
using System.Text;

namespace HidSharp.Platform.Windows
{
    sealed class WinSerialDevice : SerialDevice
    {
        string _path, _id;
        string _fileSystemName;
        string _friendlyName;
        bool _usb;
        int _vendorID;
        int _productID;
        object _objManufacturer;
        object _objProduct;
        object _objSerialNumber;

        protected override DeviceStream OpenDeviceDirectly(OpenConfiguration openConfig)
        {
            var stream = new WinSerialStream(this);
            stream.Init(DevicePath);
            return stream;
        }

        internal static WinSerialDevice TryCreate(string deviceID, string portName, string fileSystemName, string friendlyName)
        {
            var sd = new WinSerialDevice() { _path = portName, _id = deviceID, _fileSystemName = fileSystemName, _friendlyName = friendlyName };
            if (WinDeviceID.TryMatchUsb(deviceID, out sd._vendorID, out sd._productID)) { sd._usb = true; } else { sd._vendorID = -1; sd._productID = -1; }
            return sd;
        }

        public override string GetFileSystemName()
        {
            return _fileSystemName;
        }

        public override string GetFriendlyName()
        {
            return _friendlyName;
        }

        public override string GetManufacturer(GetStringFlags flags)
        {
            if (_usb) { return NativeMethods.GetUsbString(this, _id, ref _objManufacturer, flags, NativeMethods.UsbStringType.Manufacturer); }
            return base.GetManufacturer(flags);
        }

        public override string GetProductName(GetStringFlags flags)
        {
            if (_usb) { return NativeMethods.GetUsbString(this, _id, ref _objProduct, flags, NativeMethods.UsbStringType.Product); }
            return base.GetProductName(flags);
        }

        public override unsafe string GetSerialNumber(GetStringFlags flags)
        {
            if (_usb) { return NativeMethods.GetUsbString(this, _id, ref _objSerialNumber, flags, NativeMethods.UsbStringType.SerialNumber); }
            return base.GetSerialNumber(flags);
        }

        public override string DevicePath
        {
            get { return _path; }
        }

        public override int VendorID
        {
            get { return _vendorID; }
        }

        public override int ProductID
        {
            get { return _productID; }
        }
    }
}
