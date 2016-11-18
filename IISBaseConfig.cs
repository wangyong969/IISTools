using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Security.AccessControl;
using Microsoft.Win32;
using System.ServiceProcess;

namespace OneV.IISTools
{
    public abstract class IISBaseConfig
    {
        protected ConfigArgsModel Model;

        public IISBaseConfig(ConfigArgsModel configArgsModel)
        {
            this.Model = configArgsModel;
            EnableDotNetExtension();
            int port;
            if (string.IsNullOrEmpty(configArgsModel.Port) || !int.TryParse(configArgsModel.Port, out port))
            {
                configArgsModel.Port = "80";
            }
            else
            {
                configArgsModel.Port = port.ToString();
            }
        }

        /// <summary>
        /// 设置目录对iis可写
        /// </summary>
        public void SetVDirWritable()
        {
            //SetDirAccessRights(configArgs.WritableDir, FileSystemRights.Write);
        }

        public static int GetIISVersion()
        {
            string path = Path.Combine(Path.GetDirectoryName(Environment.SystemDirectory), @"system32\inetsrv");
            string filename = Path.Combine(path, "inetinfo.exe");
            if (!File.Exists(filename))
            {
                filename = Path.Combine(path, "w3wp.exe"); // IIS7
            }
            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(filename);

            return versionInfo.FileMajorPart;
        }

        protected void SetDirAccessRights(string dir, FileSystemRights rights)
        {
            string account = GetIISVersion() > 5 ? "NETWORK SERVICE" : "aspnet";

            SetDirAccessRights(account, dir, rights);
        }

        protected void SetDirAccessRights(string account, string dir, FileSystemRights rights)
        {
            if (Directory.Exists(dir))
            {
                AddFileSecurity(dir, account, rights, AccessControlType.Allow);
            }
        }

        // Adds an ACL entry on the specified file for the specified account.
        public void AddFileSecurity(string fileName, string account,
            FileSystemRights rights, AccessControlType controlType)
        {
            DirectorySecurity fSecurity = Directory.GetAccessControl(fileName);

            FileSystemAccessRule fs = new FileSystemAccessRule(
                    account, rights,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None,
                    controlType);
            fSecurity.AddAccessRule(fs);

            // Set the new access settings.
            Directory.SetAccessControl(fileName, fSecurity);
        }

        private const string fixIISCommand = @"msdtc -resetlog
                                                net start msdtc
                                                iisreset
                                                cd /d %windir%\system32\inetsrv
                                                rundll32 wamreg.dll, CreateIISPackage
                                                regsvr32 asptxn.dll /s
                                                iisreset /restart
                                                ";
        /// <summary>
        /// 修复IIS有时候可能出现的500错误
        /// </summary>
        protected void FixIIS()
        {
            if (Environment.OSVersion.Version.Major != 5 || Environment.OSVersion.Version.Minor != 1)
            {
                return;
            }
            RunCMD(fixIISCommand);
        }
        /// <summary>
        /// 执行CMD命令
        /// </summary>
        private static void RunCMD(string cmd)
        {
            using (Process p = new Process())
            {
                p.StartInfo.FileName = "cmd.exe";
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.CreateNoWindow = true;
                p.Start();
                string strOutput = string.Empty;
                p.StandardInput.WriteLine(cmd);
                p.StandardInput.WriteLine("exit");
                strOutput = p.StandardOutput.ReadToEnd();
                Console.WriteLine(strOutput);
                p.WaitForExit();
            }
        }

        protected string GetDotNetPath()
        {
            return GetDotNetPath(false);
        }

        protected string GetDotNetPath(bool is64bit)
        {
            string postfix64 = is64bit ? "64" : string.Empty;
            return Path.Combine("%systemroot%",//Path.GetDirectoryName(Environment.SystemDirectory),
                string.Format(@"Microsoft.NET\Framework{1}\{0}\", Model.FrameworkVersion, postfix64));
        }

        protected void CheckASPStateService()
        {
            const string userRoot = "HKEY_LOCAL_MACHINE";
            const string keyName = userRoot + "\\" + @"system\CurrentControlSet\services\aspnet_state";
            object startType = Registry.GetValue(keyName, "Start", null);

            if (startType != null && (int)startType != 2)
            {
                Registry.SetValue(keyName, "Start", 2);
            }

            ServiceController sc = new ServiceController("aspnet_state");
            sc.Refresh();
            if ((sc.Status.Equals(ServiceControllerStatus.Stopped)) ||
                (sc.Status.Equals(ServiceControllerStatus.StopPending)))
            {
                sc.Start();
            }
        }

        private string AppendSlash(string dir)
        {
            if (string.IsNullOrEmpty(dir))
                return "";
            switch (dir[dir.Length - 1])
            {
                case '\\':
                case '/':
                    return dir;
                default:
                    return dir + "\\";
            }
        }

        protected bool DirEqual(string dir1, string dir2)
        {
            if (string.IsNullOrEmpty(dir1) || string.IsNullOrEmpty(dir2))
            {
                throw new Exception("目录不能为空！");
            }
            dir1 = AppendSlash(dir1).ToLower();
            dir2 = AppendSlash(dir2).ToLower();
            if (dir1.Length != dir2.Length)
                return false;

            for (int i = 0; i < dir1.Length; i++)
            {
                char c1 = dir1[i];
                char c2 = dir2[i];
                if (c1 == c2)
                    continue;

                switch (c1)
                {
                    case '\\':
                    case '/':
                        break;
                    default:
                        return false;
                }
                switch (c2)
                {
                    case '\\':
                    case '/':
                        continue;
                    default:
                        return false;
                };
            }
            return true;
        }

        protected void RegisterVirtualDirToDotNet(string virtualDir)
        {
            Process process = new Process();
            process.StartInfo.FileName = GetDotNetPath() + "aspnet_regiis.exe";
            process.StartInfo.Arguments = "-sn  " + virtualDir + " -norestart";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            if (!string.IsNullOrEmpty(output))
            {
                Console.WriteLine(output);
            }
            process.Close();

        }

        public abstract void CreateWebSite();

        public abstract void CreateVirtualDir();

        public abstract void UpdateWebSiteOrVDir();

        public abstract void RemoveDir();

        protected virtual void EnableDotNetExtension()
        {

        }

        /// <summary>
        /// 检查IIS执行中所需的参数
        /// </summary>
        public virtual void CheckParams() { }

    }
}
