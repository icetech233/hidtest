#region License
/* Copyright 2025 James F. Bellinger <http://software.seekye.com/hidsharp>

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
using System.Linq;
using System.Text;
using System.Threading;

namespace HidSharp.Utility
{
    public static class HidSharpLibrary
    {
        /// <summary>
        /// Normally, HIDSharp starts a thread to watch for device connection and disconnection.
        /// This is the thread that <see cref="DeviceList.Changed"/> events are raised on.
        /// It cleans up this thread on program exit or AppDomain unload.
        /// 
        /// If, however, your program exits in a way HIDSharp does not know about, you may call
        /// this method to request it to clean up.
        /// 
        /// This function will return immediately, but shutdown may still take a small amount of time.
        /// </summary>
        /// <returns>A wait handle that will be signaled when shutdown is complete.</returns>
        public static WaitHandle ManualShutdown()
        {
            if (!Platform.HidManager.Loaded)
            {
                // Platform.HidSelector.Instance starts us up.
                // We don't want to do that just to call ManualShutdown().
                return new ManualResetEvent(true);
            }

            var manager = Platform.HidSelector.Instance;
            manager.ManualShutdown();
            return manager.ExitEvent;
        }
    }
}
