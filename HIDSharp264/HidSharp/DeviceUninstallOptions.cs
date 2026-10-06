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

namespace HidSharp
{
    public enum DeviceUninstallLogMessageType
    {
        Error,
        Info
    }

    public delegate void DeviceUninstallLogCallback(DeviceUninstallLogMessageType logType, string message);

    public sealed class DeviceUninstallOptions
    {
        DeviceUninstallLogCallback _logCallback;

        public void Log(DeviceUninstallLogMessageType logType, string message)
        {
            Throw.If.Null(message);
            Utility.HidSharpDiagnostics.Trace(message);
            var cb = _logCallback;
            if (cb != null) { cb(logType, message); }
        }

        public void LogError(string message)
        {
            Log(DeviceUninstallLogMessageType.Error, message);
        }

        public void LogInfo(string message)
        {
            Log(DeviceUninstallLogMessageType.Info, message);
        }

        public void SetLogCallback(DeviceUninstallLogCallback logCallback)
        {
            _logCallback = logCallback;
        }
    }
}
