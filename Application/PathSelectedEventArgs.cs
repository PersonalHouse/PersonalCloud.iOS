using System;

namespace Unishare.Apps.DarwinMobile
{
    public class PathSelectedEventArgs : EventArgs
    {
        public string Path { get; }

        public PathSelectedEventArgs(string selectedPath) => Path = selectedPath;
    }
}
