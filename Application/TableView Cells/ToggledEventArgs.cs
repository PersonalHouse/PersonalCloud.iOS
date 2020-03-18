using System;

namespace Unishare.Apps.DarwinMobile
{
    public class ToggledEventArgs : EventArgs
    {
        public bool On { get; }

        public ToggledEventArgs(bool state)
        {
            On = state;
        }
    }
}
