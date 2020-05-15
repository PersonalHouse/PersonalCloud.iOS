using System;

namespace NSPersonalCloud.DarwinMobile
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
