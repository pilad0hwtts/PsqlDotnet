using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

using System.Net;
using System.IO;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using Serilog;
using System.Diagnostics;


namespace PsqlDotnet
{

    //TODO: Нужен Utils или что то такое
    static class AdvancedProcess
    {
        //process is ready to run
        public static Process StartProcessWithFullLogging(this Process process)
        {
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;                                   
            
            process.Start();
            string procesName = process.ProcessName;
            process.OutputDataReceived += (a, b) => Log.Information("[{processName}] {data}", procesName, b.Data);
            process.ErrorDataReceived += (a, b) => Log.Warning("[{processName}] {data}", procesName, b.Data);
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
            Log.Information("Running {filename} {args} , pid:{pid} ", process.StartInfo.FileName, process.StartInfo.Arguments, process.Id);

            return process;

        }

        public static bool WaitForSuccessfulEnd(this Process process)
        {
            process.WaitForExit();
            if (process.ExitCode == 0)
            {
                Log.Information("{processName} had finished without any error", process.StartInfo.FileName);
                return true;
            }
            else
            {
                Log.Error("{processName} returns {code}", process.StartInfo.FileName, process.ExitCode);
                return false;
            }
        }


    }


    //TODO: Название не подходит
    public class PostgresqlAppManager : IDisposable
    {
        public class User : IDisposable {
            string name;
            System.Security.SecureString password;
            public void Dispose() => password.Dispose();
            
            public User(string name, System.Security.SecureString password) {
                this.name = name;
                this.password = password;
            }            
            public void ApplyUserData(ProcessStartInfo info) {
                info.UserName = name;
                info.Password = password;
            }            
        }        
        protected User user = null;
        protected void AplyUserData(ProcessStartInfo info) {
            if (user != null) 
                user.ApplyUserData(info);
        }
            
        


        public bool IsInstalled
        {
            get => Directory.Exists(RootFolder) 
            && Directory.Exists(Path.Combine(RootFolder, "data")) 
            && Directory.Exists(Path.Combine(RootFolder, "log")) 
            && Directory.Exists(Path.Combine(RootFolder, "pgsql"));
        }
        public bool IsRunning => Process.GetProcessesByName("postgres").Any();
        public string SupportedVersion {get; set; } = "10.13";
        
        public string DefaultUsername {get; set; } = "postgres";
        public string DefaultPassword {get; set; } = "postgres";
        public string DownloadUrlLinux {get; set; } = "https://sbp.enterprisedb.com/getfile.jsp?fileid=12574";
        public string DownloadUrlWindows {get; set; } = "https://sbp.enterprisedb.com/getfile.jsp?fileid=12546";
        public string RootFolder { get; protected set; }
        //TODO: Downlaoder Entity or something
        protected WebClient DownloadClient { get; set; } = new WebClient();
        public PostgresqlAppManager(string rootFolder) => RootFolder = rootFolder;
        public PostgresqlAppManager(string rootFolder, User user) : this(rootFolder) => this.user = user;

        public void RunPostgres()
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(RootFolder, "pgsql", "bin", "pg_ctl"),
                Arguments = $"start -D \"{Path.Combine(RootFolder, "data")}\" -l \"{Path.Combine(RootFolder, "log", "log.txt")}\" -W",        
                WindowStyle =ProcessWindowStyle.Hidden,        
                CreateNoWindow = true
            };
            AplyUserData(processInfo);
            var pg_ctl = new Process();
            pg_ctl.StartInfo = processInfo;
            pg_ctl.Start();
            pg_ctl.WaitForExit();
        }


        public void StopPostgres()
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(RootFolder, "pgsql", "bin", "pg_ctl"),
                Arguments = $"stop -D \"{Path.Combine(RootFolder, "data")}\" -W",
                UseShellExecute = false
            };
            AplyUserData(processInfo);
            var pg_ctl = new Process();
            pg_ctl.StartInfo = processInfo;
            pg_ctl.StartProcessWithFullLogging().WaitForSuccessfulEnd();
        }

        public void RestartPostgres()
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(RootFolder, "pgsql", "bin", "pg_ctl"),
                Arguments = $"restart -D \"{Path.Combine(RootFolder, "data")}\" -l \"{Path.Combine(RootFolder, "log", "log.txt")}\" -W",
                UseShellExecute = false
            };
            AplyUserData(processInfo);
            var pg_ctl = new Process();
            pg_ctl.StartInfo = processInfo;
            pg_ctl.StartProcessWithFullLogging().WaitForSuccessfulEnd();
        }

        //Full reinstalling of postgres
        public void InstallPostgreSql()
        {

            if (Directory.Exists(RootFolder))
                Directory.Delete(RootFolder, recursive: true);

            Directory.CreateDirectory(RootFolder);
            Directory.CreateDirectory(Path.Combine(RootFolder, "data"));
            Directory.CreateDirectory(Path.Combine(RootFolder, "log"));

            var os = Environment.OSVersion;
            Log.Information("Installing PostgreSQL {SupportedVersion} for operating system {VersionString} in directory {RootFolder}", SupportedVersion, os.VersionString, RootFolder);
            if (os.Platform == PlatformID.Unix)
                DownloadLinux();
            else
                DownloadWindows();


            var passwordFile = Path.Combine(RootFolder, "password.file");
            using (var stream = new StreamWriter(File.Open(Path.Combine(RootFolder, passwordFile), FileMode.CreateNew, FileAccess.Write)))
            {
                stream.Write("postgres");
            }
            var processInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(RootFolder, "pgsql", "bin", "initdb"),
                Arguments = $"-U postgres -A password -E utf8 --pwfile \"{passwordFile}\" -D \"{Path.Combine(RootFolder, "data")}",
                WorkingDirectory = Path.Combine(RootFolder, "pgsql", "bin"),
                UseShellExecute = false,          
            };
            AplyUserData(processInfo);

            var process = new Process {
                StartInfo = processInfo
            };
            process.StartProcessWithFullLogging().WaitForSuccessfulEnd();
        }
        protected void DownloadWindows()
        {
            try
            {
                using (var rawStream = DownloadClient.OpenRead(DownloadUrlWindows))
                {
                    UnzipFromStream(rawStream, RootFolder);
                }
            }
            catch (Exception e)
            {
                Log.Fatal(e.Message);
            }
        }
        protected void DownloadLinux()
        {
            try
            {
                var archivePath = RootFolder + ".tar.gz";
                DownloadClient.DownloadFile(new Uri(DownloadUrlLinux), archivePath);

                //8(-_-)8
                var processInfo = new ProcessStartInfo
                {
                    FileName = "/bin/tar",
                    Arguments = $"-xf {archivePath} -C {RootFolder}"
                };
                AplyUserData(processInfo);
                var process = new Process { 
                    StartInfo = processInfo
                }.StartProcessWithFullLogging().WaitForSuccessfulEnd();
                File.Delete(archivePath);

            }
            catch (Exception e)
            {
                Log.Fatal(e.Message);
            }
        }
        public static void UnzipFromStream(Stream zipStream, string outFolder)
        {
            using (var zipInputStream = new ZipInputStream(zipStream))
            {
                while (zipInputStream.GetNextEntry() is ZipEntry zipEntry)
                {
                    var entryFileName = zipEntry.Name;

                    var buffer = new byte[4096];

                    var fullZipToPath = Path.Combine(outFolder, entryFileName);
                    var directoryName = Path.GetDirectoryName(fullZipToPath);
                    if (directoryName.Length > 0)
                        Directory.CreateDirectory(directoryName);

                    if (Path.GetFileName(fullZipToPath).Length == 0)
                    {
                        continue;
                    }

                    using (FileStream streamWriter = File.Create(fullZipToPath))
                    {
                        StreamUtils.Copy(zipInputStream, streamWriter, buffer);
                    }
                }
            }
        }

        public void Dispose()
        {
            DownloadClient.Dispose();
            if (user != null) 
                user.Dispose();
        }


    }

}