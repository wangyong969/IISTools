using System;
using System.DirectoryServices;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using Microsoft.Win32;
using System.ServiceProcess;
using System.Text;
//using System.Security.Permissions;

namespace OneV.IISTools
{
    public class IISConfig : IISBaseConfig
    {
        /// <summary>
        /// Initializes a new instance of the IISWebsite class.
        /// </summary>
        /// <param name="configArgsModel"></param>
        public IISConfig(ConfigArgsModel configArgsModel)
            : base(configArgsModel)
        {

        }

        /// <summary>
        /// 创建web站点
        /// </summary>
        public override void CreateWebSite()
        {
            DirectoryEntry server = FindExistSite();
            if (server != null)
            {
                UpdateServer(server, Model.WebDir);
            }
            else
                InternalCreateSite();

            //if (configArgs.IsRepair)
            //{
            //    FixIIS();
            //}
        }

        /// <summary>
        /// 更新站点为carpa3配置，如果找不到站点，则在默认站点下找指定的虚拟目录
        /// </summary>
        public override void UpdateWebSiteOrVDir()
        {
            DirectoryEntry parent = NewDirectoryEntry(@"IIS://localhost/W3SVC");
            bool updated = false;
            foreach (DirectoryEntry server in parent.Children)
            {
                if (!EqualClassName(server, "IIsWebServer"))
                {
                    continue;
                }

                DirectoryEntry root = FindChild(server, "ROOT");
                if (root == null || !EqualClassName(root, "IIsWebVirtualDir"))
                    continue;
                updated |= CheckUpdateVDir(root);

                foreach (DirectoryEntry vdirObj in root.Children)
                {
                    if (EqualClassName(vdirObj, "IIsWebVirtualDir"))
                    {
                        updated |= CheckUpdateVDir(vdirObj);
                    }
                }
            }

            if (!updated)
            {
                throw new CustomException("找不到需要更新站点或虚拟目录！");
            }

            //if (configArgs.IsRepair)
            //{
            //    FixIIS();
            //}
        }

        public void EnableSvcExtAspnet()//类似于设置ISAPI
        {
            DirectoryEntry entry = NewDirectoryEntry(@"IIS://localhost/W3SVC");
            foreach (string elmentName in entry.Properties.PropertyNames)
            {
                if (!elmentName.Equals("WebSvcExtRestrictionList"))
                {
                    continue;
                }
                PropertyValueCollection valueCollection = entry.Properties[elmentName];
                for (int i = 0; i < valueCollection.Count; i++)
                {
                    if (valueCollection[i].ToString().ToLower().IndexOf("asp.net") == -1)
                    {
                        continue;
                    }
                    valueCollection[i] = string.Format("1{0}", valueCollection[i].ToString().Substring(1));
                }
                break;
            }
            entry.CommitChanges();
        }

        /// <summary>
        /// 在默认站点下创建虚拟目录
        /// </summary>
        public override void CreateVirtualDir()
        {
            DirectoryEntry root = NewDirectoryEntry(@"IIS://localhost/W3SVC/1/ROOT");
            InternalCreateVirtualDir(root, Model.VirtualName, Model.WebDir, Model.VirtualName);
        }

        /// <summary>
        /// 删除指定目录绑定的虚拟目录和站点
        /// </summary>
        public override void RemoveDir()
        {
            DirectoryEntry Parent = NewDirectoryEntry(@"IIS://localhost/W3SVC");

            bool deleted = false;
            foreach (DirectoryEntry server in Parent.Children)
            {
                if (!EqualClassName(server, "IIsWebServer"))
                {
                    continue;
                }

                DirectoryEntry root = FindChild(server, "ROOT", "IIsWebVirtualDir");
                if (root == null)
                {
                    continue;
                }

                if (IsBindingVDir(root))
                {
                    if (!server.Properties.Contains("NotDeletable") || !(bool)server.Properties["NotDeletable"][0])
                    {
                        DeleteDirectoryEntry(root);
                        DeleteDirectoryEntry(server);

                        deleted = true;
                        continue;
                    }
                }

                foreach (DirectoryEntry vdirObj in root.Children)
                {
                    if (EqualClassName(vdirObj, "IIsWebVirtualDir"))
                    {
                        deleted |= CheckRemoveVDir(vdirObj);
                    }
                }
            }

            if (!deleted)
            {
                throw new CustomException("找不到需要删除站点或虚拟目录！");
            }
        }

        #region 私有成员

        private bool ExtExists(string ext, PropertyValueCollection oa)
        {
            bool exists = false;
            foreach (string o in oa)
            {
                if (o.IndexOf(ext) == 0)
                    exists = true;
            }
            return exists;
        }

        private DirectoryEntry FindExistSite()
        {
            DirectoryEntry parent = NewDirectoryEntry(@"IIS://localhost/W3SVC");

            foreach (DirectoryEntry child in parent.Children)
            {
                if (EqualClassName(child, "IIsWebServer") &&
                    string.Compare(child.Properties["ServerComment"][0] as string, Model.HostUrl, true) == 0)
                {
                    return child;
                }
            }
            return null;
        }

        private DirectoryEntry FindExistSiteByIndex(int index)
        {
            try
            {
                DirectoryEntry server = NewDirectoryEntry(@"IIS://localhost/W3SVC/" + index.ToString());
                return string.IsNullOrEmpty(server.SchemaClassName) ? null : server;
            }
            catch (COMException)
            {
                return null;
            }
        }

        private int GetNewSiteIndex()
        {
            int index = 1;
            const int MaxIndexNumber = 10000;
            while (FindExistSiteByIndex(index) != null && index < MaxIndexNumber) index++;
            if (index >= MaxIndexNumber)
            {
                throw new CustomException("创建站点失败！");
            }
            return index;
        }

        protected override void EnableDotNetExtension()
        {
            try
            {
                DirectoryEntry webserviceObj = NewDirectoryEntry("IIS://localhost/W3SVC");
                webserviceObj.Invoke("EnableWebServiceExtension", new object[] { string.Format("ASP.NET {0}", Model.FrameworkVersion) });
                webserviceObj.CommitChanges();
            }
            catch (Exception) //xp没有扩展，会报错
            {
                //Console.WriteLine("设置扩展错误，可能没有扩展！");
                //Console.WriteLine(e.Message); 
            }
        }

        private DirectoryEntry FindChild(DirectoryEntry parent, string name)
        {
            foreach (DirectoryEntry o in parent.Children)
            {
                if (string.Compare(o.Name, name, true) == 0)
                {
                    return o;
                }
            }
            return null;
        }

        private DirectoryEntry FindChild(DirectoryEntry parent, string name, string schemaClassName)
        {
            try
            {
                parent.RefreshCache();
                return parent.Children.Find(name, schemaClassName);
            }
            catch { }
            return null;
        }

        private void SetPropValue(PropertyValueCollection collection, object value)
        {
            if (null == collection)
                return;
            try
            {
                if (collection.Capacity > 0)
                    collection[0] = value;
                else
                    collection.Add(value);
            }
            catch
            { }
        }

        void CreateAppPool(string AppPoolName)
        {
            try
            {
                DirectoryEntry newpool;
                DirectoryEntry apppools = new DirectoryEntry("IIS://localhost/W3SVC/AppPools");
                newpool = apppools.Children.Add(AppPoolName, "IIsApplicationPool");
                newpool.CommitChanges();
            }
            catch (Exception e)
            {
            }
        }

        void AssignAppPool(DirectoryEntry newvdir, string AppPoolName)
        {
            try
            {
                object[] param = { 0, AppPoolName, true };
                newvdir.Invoke("Appcreate3", param);
            }
            catch (Exception e)
            {
            }
        }

        private void UpgradeVirtualDir(DirectoryEntry vdirObj, string path, string appName, bool createDir)
        {
            try
            {
                vdirObj.RefreshCache();
            }
            catch { }

            // 检查是否已经存在
            //SetPropValue(vdirObj.Properties["DefaultDoc"], "Default.gspx,Default.aspx,Default.htm,Default.asp,index.htm"); //默认文档

            //如果只是更新，不会指定path
            if (!string.IsNullOrEmpty(path))
            {
                SetPropValue(vdirObj.Properties["Path"], path);
            }
            string newAppName = string.IsNullOrEmpty(Model.WebName) ? Model.VirtualName : Model.WebName;
            if (!string.IsNullOrEmpty(newAppName))
            {
                newAppName += Model.FrameworkVersion;
            }
            else
            {
                newAppName = vdirObj.Name + Model.FrameworkVersion;
            }
            CreateAppPool(newAppName);//一个应用程序一个应用程序池   2013-11-21  ljb
            //SetPropValue(vdirObj.Properties["AppFriendlyName"], newAppName);
            AssignAppPool(vdirObj, newAppName);
            SetPropValue(vdirObj.Properties["AppFriendlyName"], newAppName);
            SetPropValue(vdirObj.Properties["AccessScript"], true);
            SetPropValue(vdirObj.Properties["AccessExecute"], false); // 默认为true，必须去掉“可执行文件”才能下载Silverlight.exe
            SetPropValue(vdirObj.Properties["AspEnableParentPaths"], true);
            if (createDir)
                vdirObj.Invoke("AppCreate", new object[] { true });
            vdirObj.CommitChanges();

            PropertyValueCollection propValues = vdirObj.Properties["ScriptMaps"];
            if (!ExtExists(".ajax", propValues))
            {
                DirectoryEntry pool = new DirectoryEntry("IIS://localhost/W3SVC/AppPools/Classic .NET AppPool");
                string isapi64 = GetIsapiDll(true);
                //判定当前IIS是否支持64位且正好设置为只支持64位    2012-04-06
                if (File.Exists(isapi64) && pool != null && !Convert.ToBoolean(pool.Properties["enable32BitAppOnWin64"].Value))
                {
                    AddIsapiHandler(propValues, isapi64);
                }
                else
                {
                    string isapi = GetIsapiDll(false);
                    AddIsapiHandler(propValues, isapi);
                }
                vdirObj.CommitChanges();
                /*
                    选 脚本引擎, 以及 检查文件是否存在
                       ".asp,C:\\WINDOWS\\system32\\inetsrv\\asp.dll,5,GET,HEAD,POST,TRACE"
                    选 脚本引擎, 不选 检查文件是否存在
                      ".ashx,C:\\WINDOWS\\Microsoft.NET\\Framework\\v2.0.50727\\aspnet_isapi.dll,1,GET,HEAD,POST,DEBUG"
                 */
            }

            SetMimeType(vdirObj, ".xap", "application/x-silverlight");
            SetMimeType(vdirObj, ".exe", "application/octet-stream"); // 避免建的站点无法下载 .exe 文件

            if (path != "")
            {
                SetDirAccessRights(path, FileSystemRights.Read);
                SetDirAccessRights("users", path, FileSystemRights.Read);
                SetDirAccessRights(Path.Combine(path, "data"), FileSystemRights.Write);
            }
            CheckASPStateService(); //检查aspstate服务
            EnableSvcExtAspnet();
            try
            {
                RegisterVirtualDirToDotNet("w3svc/" + GetVdirPath(vdirObj));
            }
            catch { }
        }

        private string GetIsapiDll(bool is64bit)
        {
            return Path.Combine(GetDotNetPath(is64bit), "aspnet_isapi.dll"); // 如 C:\WINDOWS\Microsoft.NET\Framework\v2.0.50727\aspnet_isapi.dll
        }

        private void AddIsapiHandler(PropertyValueCollection propValues, string isapi)
        {
            //1修改成5，保证设置-version参数为4.0的情况下时，可以指定asp.net为4.0 2013-11-07 李久彬
            //propValues.Add(".ajax," + isapi + ",1,GET,HEAD,POST,DEBUG");
            propValues.Add(".ajax," + isapi + ",5,GET,HEAD,POST,DEBUG");
            propValues.Add(".gspx," + isapi + ",5,GET,HEAD,POST,DEBUG");
            if (IISBaseConfig.GetIISVersion() == 6)
            {
                //propValues.Add(string.Format("*,{0},4,全部", isapi));//修改为0，表示默认“确认文件是否存在”没有勾选  2013-11-12
                propValues.Add(string.Format("*,{0},0,全部", isapi));
            }
        }

        private static void SetMimeType(DirectoryEntry vdirObj, string newExtension, string newMimeType)
        {
            PropertyValueCollection propValues = vdirObj.Properties["MimeMap"];
            object exists = null;
            const string cMimeType = "MimeType";
            const string cExtension = "Extension";
            foreach (object value in propValues)
            {
                if (newExtension == GetObjectProp(value, cExtension))
                    exists = value;
            }

            if (exists != null)
            {
                if (newMimeType == GetObjectProp(exists, cMimeType))
                {
                    return;
                }
                propValues.Remove(exists);
            }

            object newObj = CreateObject("MimeMap");
            SetObjectProp(newObj, cExtension, newExtension);
            SetObjectProp(newObj, cMimeType, newMimeType);
            propValues.Add(newObj);
            vdirObj.CommitChanges();
        }

        private static object CreateObject(string progID)
        {
            Type typeFromProgID = null;
            try
            {
                typeFromProgID = Type.GetTypeFromProgID(progID);
            }
            catch
            {
            }
            if (typeFromProgID == null)
            {
                throw new InvalidOperationException(string.Format("创建对象“{0}”失败", progID));
            }
            return Activator.CreateInstance(typeFromProgID);
        }

        private static string GetObjectProp(object obj, string name)
        {
            return ((string)obj.GetType().InvokeMember(name, BindingFlags.GetProperty, null, obj, null)).ToLowerInvariant();
        }

        private static void SetObjectProp(object obj, string name, string value)
        {
            obj.GetType().InvokeMember(name, BindingFlags.SetProperty, null, obj, new object[] { value });
        }

        private DirectoryEntry InternalCreateVirtualDir(DirectoryEntry parent, string virtualDirName, string path, string appName)
        {
            DirectoryEntry NewVirtualDir = FindChild(parent, virtualDirName);

            bool haveVirtualDir = NewVirtualDir != null;
            if (!haveVirtualDir)
            {
                NewVirtualDir = parent.Children.Add(virtualDirName, "IIsWebVirtualDir");
                NewVirtualDir.CommitChanges();
                parent.CommitChanges();
                UpgradeVirtualDir(NewVirtualDir, path, appName, !haveVirtualDir);
            }
            else
            {
                NewVirtualDir.RefreshCache();
                UpgradeVirtualDir(NewVirtualDir, path, appName, !haveVirtualDir);
            }

            return NewVirtualDir;
        }

        private string GetServerBindings()
        {
            string s = ":" + Model.Port + ":";
            if (Model.Port == "80")
            {
                s += Model.HostUrl;
            }
            return s;
        }

        /// <summary>
        /// 调试的时候打印对象使用
        /// </summary>
        /// <param name="entry"></param>
        private void PrintEntry(DirectoryEntry entry)
        {
            try
            {
                Console.WriteLine("o:{0}:t:{1} path:{2}", entry.Name, entry.SchemaClassName, entry.Path);
                Console.WriteLine("childs:");
                foreach (DirectoryEntry child in entry.Children)
                {
                    Console.WriteLine("     o:{0}:t:{1}", child.Name, child.SchemaClassName);
                }

                Console.WriteLine("propertes:");
                foreach (PropertyValueCollection prop in entry.Properties)
                {
                    Console.WriteLine("     p:{0}", prop.PropertyName);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private string GetVdirPath(DirectoryEntry vdirObj)
        {
            if (!EqualClassName(vdirObj, "IIsWebVirtualDir"))
            {
                throw new Exception("只能获取虚拟目录的路径！");
            }
            string path = vdirObj.Name;
            vdirObj = vdirObj.Parent;
            while (!EqualClassName(vdirObj, "IIsWebServer"))
            {
                path = vdirObj.Name + "/" + path;
                vdirObj = vdirObj.Parent;
            }
            path = vdirObj.Name + "/" + path;
            return path;
        }

        private void InternalCreateSite()
        {
            if (Environment.OSVersion.Version.Major == 5 &&
                Environment.OSVersion.Version.Minor == 1) //如果要判断vista 和xp64会比较麻烦，先不管
                throw new CustomException("xp系统不支持多站点！");
            DirectoryEntry Parent = NewDirectoryEntry(@"IIS://localhost/W3SVC");
            DirectoryEntry server = Parent.Children.Add(GetNewSiteIndex().ToString(), "IIsWebServer");

            server.Properties["ServerBindings"].Add(GetServerBindings());
            server.Properties["ServerComment"][0] = Model.HostUrl;
            server.CommitChanges();
            InternalCreateVirtualDir(server, "ROOT", Model.WebDir, "默认应用程序");
        }

        private void UpdateServer(DirectoryEntry server, string virtualDir)
        {
            server.Properties["ServerBindings"][0] = GetServerBindings();
            server.Properties["ServerComment"][0] = Model.HostUrl;
            server.CommitChanges();
            InternalCreateVirtualDir(server, "ROOT", virtualDir, "默认应用程序");
        }

        private bool CheckUpdateVDir(DirectoryEntry vdirObj)
        {
            if (!IsBindingVDir(vdirObj))
                return false;
            UpgradeVirtualDir(vdirObj, "", "默认应用程序", false);
            return true;
        }

        private bool IsBindingVDir(DirectoryEntry vdirObj)
        {
            if (!EqualClassName(vdirObj, "IIsWebVirtualDir"))
            {
                return false;
            }
            string vdir = vdirObj.Properties.Contains("Path") ? vdirObj.Properties["Path"][0] as string : "";
            return !string.IsNullOrEmpty(vdir) && DirEqual(vdir, Model.WebDir);
        }

        private bool CheckRemoveVDir(DirectoryEntry vdirObj)
        {
            if (!IsBindingVDir(vdirObj))
            {
                return false;
            }

            DeleteDirectoryEntry(vdirObj);

            return true;
        }

        private void DeleteDirectoryEntry(DirectoryEntry entry)
        {
            // 获取上级目录的DirectoryEntry对象
            DirectoryEntry rootEntry = entry.Parent;
            // 删除参数
            object[] objParams = new object[2]; ;
            objParams[0] = entry.SchemaClassName;
            objParams[1] = entry.Name;
            rootEntry.Invoke("Delete", objParams);
            rootEntry.CommitChanges();
        }

        private bool EqualClassName(DirectoryEntry entry, string className)
        {
            try
            {
                entry.RefreshCache();
            }
            catch { }
            return string.Compare(entry.SchemaClassName, className, true) == 0;
        }

        private DirectoryEntry NewDirectoryEntry(string path)
        {
            try
            {
                DirectoryEntry entry = new DirectoryEntry(path);
                entry.RefreshCache();
                return entry;
            }
            catch
            {
                //先不管错误的问题
                throw;
            }
        }
        #endregion
    }
}
