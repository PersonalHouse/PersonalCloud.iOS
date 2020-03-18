using System;

namespace Unishare.Apps.DarwinMobile
{
    public class BarcodeDetectedEventArgs : EventArgs
    {
        public string Barcode { get; }

        public BarcodeDetectedEventArgs(string barcode) => Barcode = barcode;
    }
}
