#region License
/* Copyright 2022 James F. Bellinger <http://software.seekye.com/hidsharp>

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

using System.Globalization;
using System.Text.RegularExpressions;

namespace HidSharp.Platform.Windows
{
    static class WinDeviceID
    {
        const string HidRegex = @"^HID\\VID_([0-9A-F]{4})&PID_([0-9A-F]{4})";
        const string UsbRegex = @"^USB\\VID_([0-9A-F]{4})&PID_([0-9A-F]{4})";

        public static bool TryMatchHid(string deviceID, out int vendorID, out int productID)
        {
            return TryMatch(HidRegex, deviceID, out vendorID, out productID);
        }

        public static bool TryMatchUsb(string deviceID, out int vendorID, out int productID)
        {
            return TryMatch(UsbRegex, deviceID, out vendorID, out productID);
        }

        static bool TryMatch(string regex, string deviceID, out int vendorID, out int productID)
        {
            var m = Regex.Match(deviceID, regex);
            if (m.Success && m.Groups.Count == 3)
            {
                ushort vid, pid;
                if (ushort.TryParse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out vid))
                {
                    if (ushort.TryParse(m.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out pid))
                    {
                        vendorID = vid; productID = pid;
                        return true;
                    }
                }
            }

            vendorID = 0; productID = 0;
            return false;
        }
    }
}
