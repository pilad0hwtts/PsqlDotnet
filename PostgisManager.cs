using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using Serilog;

namespace PsqlDotnet {

    public class PostgisManager {
        public PostgresqlAppManager Manager { get; protected set; }

        //Downlaoder entity
        public WebClient client = new WebClient ();

        public PostgisManager (PostgresqlAppManager manager) => Manager = manager;

        public void InstallPostgis () {
            var os = Environment.OSVersion;
            if (os.Platform == PlatformID.Unix) {
                DownloadLinux ();
            } else if (os.Platform == PlatformID.Win32NT) {
                DownloadWindows ();
            }
        }

        //TODO: Linux installation
        protected void DownloadLinux () {
            throw new NotImplementedException ();
        }

        
        protected void DownloadWindows () {
            throw new NotImplementedException ();
        }
    }

}