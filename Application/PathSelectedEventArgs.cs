using System;

namespace NSPersonalCloud.DarwinMobile
{
    public class PathSelectedEventArgs : EventArgs
    {
        public string Path { get; }

        public PathSelectedEventArgs(string selectedPath) => Path = selectedPath;
    }
}
