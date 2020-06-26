using System;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Net;
using Serilog;

namespace PsqlDotnet
{

    public class PostgisManager : IDisposable
    {
        public string DownloadPostgisWindows = "http://download.osgeo.org/postgis/windows/pg10/postgis-bundle-pg10-3.0.1x64.zip";
        public PostgresqlAppManager Manager { get; protected set; }

        //Downlaoder entity
        public WebClient client = new WebClient();
        public void Dispose() => client.Dispose();

        public PostgisManager(PostgresqlAppManager manager) => Manager = manager;

        public void InstallPostgis()
        {
            Log.Information("Installation PostGIS");
            var os = Environment.OSVersion;
            if (os.Platform == PlatformID.Unix)
            {
                DownloadLinux();
            }
            else if (os.Platform == PlatformID.Win32NT)
            {
                DownloadWindows();
            }            
        }

        public string ActivatePostgisSql() => "CREATE EXTENSION postgis; CREATE EXTENSION postgis_raster;";

        //TODO: Linux installation
        protected void DownloadLinux()
        {
            throw new NotImplementedException();
        }

        protected void DownloadWindows()
        {
            Log.Information("Downloading Postgis");
            var postgisDir = Path.Combine(Manager.RootFolder, "postgis");
            using (var stream = client.OpenRead(DownloadPostgisWindows))
            {
                PostgresqlAppManager.UnzipFromStream(stream, postgisDir);
            }
            postgisDir = Directory.GetDirectories(postgisDir)[0];            
            foreach (var dir in Directory.GetDirectories(postgisDir))
            {
                DirectoryCopy(dir, Path.Combine(Manager.RootFolder, "pgsql", dir.Split(Path.DirectorySeparatorChar).Last()), true);                
            }
            Log.Information("Done");
        }

        //TODO: Utils 
        public static void DirectoryCopy( string sourceDirName, string destDirName, bool copySubDirs)
        {
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);
            DirectoryInfo[] dirs = dir.GetDirectories();

            // If the source directory does not exist, throw an exception.
            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            // If the destination directory does not exist, create it.
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }


            // Get the file contents of the directory to copy.
            FileInfo[] files = dir.GetFiles();

            foreach (FileInfo file in files)
            {
                // Create the path to the new copy of the file.
                string temppath = Path.Combine(destDirName, file.Name);

                // Copy the file.
                file.CopyTo(temppath, true);
            }

            // If copySubDirs is true, copy the subdirectories.
            if (copySubDirs)
            {

                foreach (DirectoryInfo subdir in dirs)
                {
                    // Create the subdirectory.
                    string temppath = Path.Combine(destDirName, subdir.Name);

                    // Copy the subdirectories.
                    DirectoryCopy(subdir.FullName, temppath, copySubDirs);
                }
            }
        }
    }

}