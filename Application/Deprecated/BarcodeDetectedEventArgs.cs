using System;

namespace NSPersonalCloud.DarwinMobile
{
    public class BarcodeDetectedEventArgs : EventArgs
    {
        public string Barcode { get; }

        public BarcodeDetectedEventArgs(string barcode) => Barcode = barcode;
    }
}
