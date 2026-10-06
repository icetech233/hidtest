#region License
/* Copyright 2021 James F. Bellinger <http://software.seekye.com/hidsharp>

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

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text;

// Ported over from my USBRemover project...
namespace HidSharp.Platform.Windows
{
    static class WinDeviceUninstall
    {
#if !NETSTANDARD
        public static void UninstallDevice(int vendorID, int productID, DeviceUninstallOptions options)
        {
            Throw.If.Null(options);
            if (vendorID < 0 || vendorID > ushort.MaxValue) { Utility.HidSharpDiagnostics.PerformStrictCheck(false, "Vendor ID out of range."); return; }
            if (productID < 0 || productID > ushort.MaxValue) { Utility.HidSharpDiagnostics.PerformStrictCheck(false, "Product ID out of range."); return; }

            string vid = vendorID.ToString("X").PadLeft(4, '0');
            string pid = productID.ToString("X").PadLeft(4, '0');

            // First, let's remove USB and HID entries for this Vendor ID and Product ID.
            string[] searchPrefixes = new[]
                {
                    string.Format(@"USB\VID_{0}&PID_{1}", vid, pid),
                    string.Format(@"HID\VID_{0}&PID_{1}", vid, pid),
                };

            var di = NativeMethods.SetupDiGetClassDevs(new Guid("{A5DCBF10-6530-11D2-901F-00C04FB951ED}"), null, IntPtr.Zero, NativeMethods.DIGCF.AllClasses);
            if (di.IsValid)
            {
                try
                {
                    var did = new NativeMethods.SP_DEVINFO_DATA();
                    did.Size = Marshal.SizeOf(did);

                    for (int i = 0; NativeMethods.SetupDiEnumDeviceInfo(di, i, ref did); i++)
                    {
                        string deviceID;
                        if (0 == NativeMethods.CM_Get_Device_ID(did.DevInst, out deviceID))
                        {
                            if (searchPrefixes.Any(searchPrefix => deviceID.StartsWith(searchPrefix)))
                            {
                                if (NativeMethods.SetupDiCallClassInstaller(NativeMethods.DIF_REMOVE, di, ref did))
                                {
                                    options.LogInfo(string.Format("Removed {0}.",
                                        deviceID
                                        ));
                                }
                                else
                                {
                                    options.LogError(string.Format("Failed to remove {0}: {1}",
                                        deviceID,
                                        new Win32Exception(Marshal.GetLastWin32Error()).Message
                                        ));
                                }
                            }
                        }
                    }
                }
                finally
                {
                    NativeMethods.SetupDiDestroyDeviceInfoList(di);
                }
            }

            // Now, let's remove the usbflags entry.
            var usbflagsKey = @"SYSTEM\CurrentControlSet\Control\usbflags";
            try
            {
                using (var usbFlagsEnumKey = Registry.LocalMachine.OpenSubKey(usbflagsKey, true))
                {
                    var subkeys = usbFlagsEnumKey.GetSubKeyNames();
                    foreach (var subkey in subkeys)
                    {
                        if (subkey.Length != 12) { continue; }
                        if (subkey.Substring(0, 8) != vid + pid) { continue; }

                        var fullKey = string.Format("HKEY_LOCAL_MACHINE\\{0}\\{1}", usbflagsKey, subkey);

                        try
                        {
                            usbFlagsEnumKey.DeleteSubKey(subkey);
                        }
                        catch (Exception e)
                        {
                            options.LogError(string.Format("Failed to delete {0}: {1}", fullKey, e.Message));
                            continue;
                        }

                        options.LogInfo(string.Format("Deleted {0}.", fullKey));
                    }
                }
            }
            catch (Exception e)
            {
                options.LogError(string.Format("Failed to open HKEY_LOCAL_MACHINE\\{0} for writing: {1}", usbflagsKey, e.Message));
            }
        }
#endif
    }
}
