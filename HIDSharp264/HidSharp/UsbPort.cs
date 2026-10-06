namespace HidSharp
{
    public abstract class UsbPort
    {
        public override string ToString()
        {
            return string.Format("{0} port {1}", HubDevicePath, PortNumber);
        }

        public abstract string HubDevicePath
        {
            get;
        }

        public abstract int PortNumber
        {
            get;
        }
    }
}
