using System;
using System.DirectoryServices;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using Microsoft.Win32;
using System.ServiceProcess;
using Microsoft.Web.Administration;
using System.Management;
using System.Collections.Generic;
//using System.Security.Permissions;

namespace OneV.IISTools
{
    public class IISConfigEx : IISBaseConfig
    {
        private const string String_DefaultDocument = "system.webServer/defaultDocument";
        private const string String_Handlers = "system.webServer/handlers";
        private const string String_StaticContent = "system.webServer/staticContent";
        private const string String_IsapiRestriction = "system.webServer/security/isapiCgiRestriction";
        private const string String_DefaultAppPool = "Classic .NET AppPool";
        private ServerManager manager = new ServerManager();

        private string webName;
        private string virtualName;

        /// <summary>
        /// Initializes a new instance of the IISWebsite class.
        /// </summary>
        /// <param name="configArgsModel"></param>
        public IISConfigEx(ConfigArgsModel configArgsModel)
            : base(configArgsModel)
        {
            virtualName = string.Format("/{0}", string.IsNullOrWhiteSpace(Model.VirtualName) ? "" : Model.VirtualName.TrimStart('/'));
            webName = Model.WebName;
            SetIsapiRestriction();
        }

        public override void CheckParams()
        {
            if (string.IsNullOrWhiteSpace(Model.WebDir))
            {
                throw new CustomException(-2, "����ָ����վ����Ŀ¼");
            }
            else
            {
                string virtualDir = Path.GetFullPath(Model.WebDir);
                if (!Directory.Exists(virtualDir))
                {
                    throw new CustomException(-2, string.Format("Ŀ¼�����ڣ�{0}", virtualDir));
                }
            }
            CheckCreateVirtualParams();
            CheckCreateNewWebParams();
        }


        private void CheckCreateVirtualParams()
        {
            if (Model.ExecuteType != CommandType.CreateVirtualDir) return;

            if (string.IsNullOrWhiteSpace(Model.VirtualName))
            {
                throw new CustomException(-2, "����Ŀ¼����Ϊ��");
            }
        }


        private void CheckCreateNewWebParams()
        {
            if (Model.ExecuteType != CommandType.CreateWebSite) return;

            if (string.IsNullOrWhiteSpace(Model.WebName))
            {
                throw new CustomException(-2, "��վ���Ʋ���Ϊ��");
            }

        }






        /// <summary>
        /// ����webվ��
        /// </summary>
        public override void CreateWebSite()
        {
            Site site = FindExistSite();
            if (site != null)
            {
                UpdateServer(site, Model.WebDir);
            }
            else
                InternalCreateSite();
            manager.CommitChanges();
        }

        /// <summary>
        /// ����վ��Ϊcarpa3���ã�����Ҳ���վ�㣬����Ĭ��վ������ָ��������Ŀ¼
        /// </summary>
        public override void UpdateWebSiteOrVDir()
        {
            if (manager.Sites.Count == 0)
            {
                throw new CustomException("վ������Ϊ0���޷����վ����£�");
            }
            bool updated = false;
            //����վ�������Ŀ¼ 2014-01-22  ljb
            foreach (Site currentSite in manager.Sites)
            {
                foreach (Application application in currentSite.Applications)
                {
                    foreach (VirtualDirectory vDir in application.VirtualDirectories)
                    {
                        if (string.Compare(vDir.PhysicalPath, Model.WebDir, true) == 0)
                        {
                            string poolName = application.Path.TrimStart('/');
                            poolName = string.IsNullOrEmpty(poolName) ? currentSite.Name : poolName;
                            updated |= CheckUpdateVDir(poolName, application);
                        }
                    }
                }
            }

            if (!updated)
            {
                throw new CustomException("�Ҳ�����Ҫ����վ�������Ŀ¼��");
            }

            //if (configArgs.IsRepair)
            //{
            //    FixIIS();
            //}

            manager.CommitChanges();
        }

        /// <summary>
        /// ��Ĭ��վ���´�������Ŀ¼
        /// </summary>
        public override void CreateVirtualDir()
        {
            if (manager.Sites.Count == 0)
            {
                return;
            }

            Site site = string.IsNullOrEmpty(webName) || manager.Sites[webName] == null
                ? manager.Sites[0] : manager.Sites[Model.WebName];
            InternalCreateVirtualDir(site, virtualName, Model.WebDir, virtualName);

            manager.CommitChanges();
        }

        /// <summary>
        /// ɾ��ָ��Ŀ¼�󶨵�����Ŀ¼��վ��
        /// </summary>
        public override void RemoveDir()
        {

            bool deleted = false;
            int childWeb = 0;
            bool isRootDel = false;
            for (int i = manager.Sites.Count - 1; i >= 0; i--)
            {
                Site site = manager.Sites[i];
                for (int j = site.Applications.Count - 1; j >= 0; j--)
                {
                    Application application = site.Applications[j];

                    if (!application.Path.ToLower().Equals("/"))
                    {
                        if (!IsBindingVDir(application))
                        {
                            childWeb++; //����Ŀ¼��ͳ��
                            continue;
                        }
                        deleted = true;
                        site.Applications.RemoveAt(j);
                    }
                    else
                    {
                        isRootDel = IsBindingVDir(application);
                    }
                }
                if (isRootDel && childWeb <= 0) //û������Ŀ¼������²������Ƴ���վ��
                {
                    manager.Sites.Remove(site);
                }
            }
            manager.CommitChanges();
            if (!deleted)
            {
                throw new CustomException("�Ҳ�����Ҫɾ��վ�������Ŀ¼��");
            }
        }

        #region ˽�г�Ա

        private bool ExtExists(string ext, ConfigurationElementCollection handlerCollection, out int index)
        {
            bool exists = false;
            index = -1;
            foreach (ConfigurationElement element in handlerCollection)
            {
                if (string.Compare(element.Schema.Name, "add", true) != 0
                    || string.Compare(element["path"].ToString(), ext, true) != 0)
                {
                    continue;
                }
                index = handlerCollection.IndexOf(element);
                exists = true;
                break;
            }
            return exists;
        }

        /// <summary>
        /// ����IIS�Ƿ����Webվ��
        /// </summary>
        /// <returns></returns>
        private Site FindExistSite()
        {
            foreach (Site site in manager.Sites)
            {
                if (string.Compare(site.Name, Model.WebName, true) == 0)
                {
                    return site;
                }
            }
            return null;
        }

        private int GetNewSiteIndex()
        {
            int index = manager.Sites.Count;
            const int MaxIndexNumber = 10000;

            List<long> list = new List<long>();
            foreach (Site site in manager.Sites)
            {
                list.Add(site.Id);
            }
            for (int i = 1; i < MaxIndexNumber; i++)
            {
                if (!list.Contains(i))
                {
                    index = i;
                    break;
                }
            }
            if (index >= MaxIndexNumber)
            {
                throw new CustomException("����վ��ʧ�ܣ�");
            }

            return index;
        }

        private Application FindChild(Site parent, string name)
        {
            foreach (Application o in parent.Applications)
            {
                if (string.Compare(o.Path, name, true) == 0)
                {
                    return o;
                }
            }
            return null;
        }

        private void UpgradeVirtualDir(Application vdirObj, string path, string appName, bool createDir)
        {

            Configuration config = vdirObj.GetWebConfiguration();
            //ע�͵Ĳ���ͨ��Web.configֱ�����ƣ�
            //ConfigurationElementCollection defaultDocumentCollection = config.GetSection(String_DefaultDocument).GetChildElement("files").GetCollection();
            //defaultDocumentCollection.Add(defaultDocumentCollection.CreateElement("clear"));
            //string[] defaultDocs = new string[] { "Default.gspx", "Default.aspx", "Default.htm", "Default.asp", "index.htm" };
            //foreach (string doc in defaultDocs)
            //{
            //    ConfigurationElement element = defaultDocumentCollection.CreateElement("add");
            //    element.SetAttributeValue("value", doc);
            //    defaultDocumentCollection.Add(element);
            //}

            //SetIsapi("*.ajax", config, vdirObj.ApplicationPoolName);
            //SetIsapi("*.gspx", config, vdirObj.ApplicationPoolName);

            SetMimeType(config, ".xap", "application/x-silverlight");
            SetMimeType(config, ".exe", "application/octet-stream"); // ���⽨��վ���޷����� .exe �ļ�
            if (path != "")
            {
                SetDirAccessRights(path, FileSystemRights.Read);
                SetDirAccessRights("users", path, FileSystemRights.Read);
                SetDirAccessRights(Path.Combine(path, "data"), FileSystemRights.Write);
            }
            CheckASPStateService(); //���aspstate����
        }

        private void SetIsapi(string ext, Configuration config, string poolName)
        {
            ConfigurationElementCollection handlerCollection = config.GetSection(String_Handlers).GetCollection();
            int index;
            string isapi64 = GetIsapiDll(true);
            ApplicationPool pool = manager.ApplicationPools[poolName];
            string isapi = File.Exists(isapi64) && pool != null && !pool.Enable32BitAppOnWin64 ? isapi64 : GetIsapiDll(false);
            if (!ExtExists(ext, handlerCollection, out index))
            {
                //�ж���ǰIIS�Ƿ�֧��64λ����������Ϊֻ֧��64λ    2012-04-06
                AddIsapiHandler(ext, isapi, handlerCollection);
            }
            else
            {
                SetIsapiConfig(ext, isapi, handlerCollection[index]);
            }
        }

        //����IIS������ISAPI��CGI���� 
        private void SetIsapiRestriction()
        {
            Configuration config = manager.GetApplicationHostConfiguration();
            ConfigurationSection isapiCgiRestrictionSection = config.GetSection("system.webServer/security/isapiCgiRestriction");
            ConfigurationElementCollection isapiCgiRestrictionCollection = isapiCgiRestrictionSection.GetCollection();
            foreach (ConfigurationElement element in isapiCgiRestrictionCollection)
            {
                ConfigurationAttributeCollection attributes = element.Attributes;
                if (attributes["path"].Value.ToString().ToLower().IndexOf(Model.FrameworkVersion.ToLower()) != -1)
                {
                    attributes["allowed"].Value = true;
                }
            }
        }

        private string GetIsapiDll(bool is64bit)
        {
            return Path.Combine(GetDotNetPath(is64bit), "aspnet_isapi.dll"); // �� C:\WINDOWS\Microsoft.NET\Framework\v2.0.50727\aspnet_isapi.dll
        }

        private static void AddIsapiHandler(string ext, string isapi, ConfigurationElementCollection handlerCollection)
        {
            ConfigurationElement element = handlerCollection.CreateElement("add");
            SetIsapiConfig(ext, isapi, element);
            handlerCollection.AddAt(0, element);
        }

        private static void SetIsapiConfig(string ext, string isapi, ConfigurationElement element)
        {
            element.SetAttributeValue("verb", "GET,HEAD,POST,DEBUG");
            element.SetAttributeValue("path", ext);
            element.SetAttributeValue("scriptProcessor", isapi);
            element.SetAttributeValue("name", string.Format("AboMapperCustom-{0}", ext.GetHashCode()));
            element.SetAttributeValue("modules", "IsapiModule");
            element.SetAttributeValue("requireAccess", "Script");
            element.SetAttributeValue("responseBufferLimit", 0);
        }

        private static void SetMimeType(Configuration config, string newExtension, string newMimeType)
        {
            /*
                <remove fileExtension=".xap" />
                <mimeMap fileExtension=".xap" mimeType="application/x-silverlight" />
             */
            ConfigurationElementCollection defaultDocumentStaticContent = config.GetSection(String_StaticContent).GetCollection();

            ConfigurationElement addExists = null;
            foreach (ConfigurationElement element in defaultDocumentStaticContent)
            {
                string temp = element.Schema.Name.ToLower();
                if (temp != "add" || string.Compare(element.Attributes["fileExtension"].Value.ToString(), newExtension, true) != 0)
                {
                    continue;
                }
                addExists = element;
                break;
            }

            if (addExists != null)
            {
                if (string.Compare(addExists.Attributes["mimeType"].Value.ToString(), newMimeType, true) == 0)
                {
                    return;
                }
                addExists.Attributes["mimeType"].Value = newMimeType;
            }
        }

        private static void SetObjectProp(object obj, string name, string value)
        {
            obj.GetType().InvokeMember(name, BindingFlags.SetProperty, null, obj, new object[] { value });
        }

        private void InternalCreateVirtualDir(Site parent, string virtualDirName, string path, string appName)
        {
            Application newVirtualDir = FindChild(parent, virtualDirName);

            string poolName = parent.Name;
            if (virtualDirName.Length > 1)
            {
                poolName = virtualDirName.Substring(1);
            }
            bool createDir = false;
            if (newVirtualDir == null)
            {
                createDir = true;
                newVirtualDir = parent.Applications.Add(virtualDirName, path);
            }
            GetApplicationPool(poolName, newVirtualDir);
            manager.CommitChanges();
            UpgradeVirtualDir(newVirtualDir, path, appName, createDir);
        }

        private void GetApplicationPool(string poolName, Application newVirtualDir)
        {
            ApplicationPool element = (ApplicationPool)manager.ApplicationPools[poolName];
            if (element == null)
            {
                element = manager.ApplicationPools.CreateElement();
                element.Name = poolName;
                manager.ApplicationPools.Add(element);
            }
            if (Model.FrameworkVersion.ToLower().StartsWith("v4"))
            {
                element.ManagedRuntimeVersion = "v4.0";
            }
            else
            {
                element.ManagedRuntimeVersion = "v2.0";
            }
            element.Enable32BitAppOnWin64 = Model.User32Pool;// || !(File.Exists(GetIsapiDll(true)));
            element.ManagedPipelineMode = Model.IsClassic ? ManagedPipelineMode.Classic : ManagedPipelineMode.Integrated;
            newVirtualDir.ApplicationPoolName = element.Name;
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

        private void InternalCreateSite()
        {
            Site site = manager.Sites.CreateElement();
            site.Name = Model.WebName;
            site.Id = GetNewSiteIndex();
            //�˿ں�������������
            string httpBindName = Model.HostUrl;
            string[] names = httpBindName == null ? new string[] { } : httpBindName.Split(',');
            foreach (string name in names)
            {
                if (!string.IsNullOrEmpty(name))
                {
                    AppandBinding("http", string.Format("*:{0}:{1}", string.IsNullOrEmpty(Model.Port) ? "80" : Model.Port, name), site);
                }
            }
            manager.Sites.Add(site);
            InternalCreateVirtualDir(site, "/", Model.WebDir, "Ĭ��Ӧ�ó���");
        }

        private static void AppandBinding(string protocol, string bindingInfo, Site site)
        {
            Binding binding = site.Bindings.CreateElement();
            binding.Protocol = protocol;
            binding.BindingInformation = bindingInfo;
            site.Bindings.Add(binding);
        }

        private void UpdateServer(Site site, string virtualDir)
        {
            site.Bindings.Clear();
            //�˿ں�������������
            string httpBindName = Model.HostUrl;
            string[] names = httpBindName == null ? new string[] { } : httpBindName.Split(',');
            foreach (string name in names)
            {
                if (!string.IsNullOrEmpty(name))
                {
                    AppandBinding("http", string.Format("*:{0}:{1}", string.IsNullOrEmpty(Model.Port) ? "80" : Model.Port, name), site);
                }
            }
            InternalCreateVirtualDir(site, "/", virtualDir, "Ĭ��Ӧ�ó���");
        }

        private bool CheckUpdateVDir(string poolName, Application application)
        {
            if (!IsBindingVDir(application))
                return false;
            //UpgradeVirtualDir(application, "/", "Ĭ��Ӧ�ó���", false);
            UpgradeVirtualDir(application, application.VirtualDirectories[0].PhysicalPath, "Ĭ��Ӧ�ó���", false);
            GetApplicationPool(poolName, application);
            return true;
        }

        private bool IsBindingVDir(Application application)
        {
            string vdir = application.VirtualDirectories.Count != 0 ? application.VirtualDirectories[0].PhysicalPath : "";
            return !string.IsNullOrEmpty(vdir) && DirEqual(vdir, Model.WebDir);
        }

        private bool EqualClassName(Application entry, string className)
        {
            return string.Compare(entry.Schema.Name, className, true) == 0;
        }

        private Site NewApplicationSite(string siteName)
        {
            Site site = manager.Sites[siteName];
            if (site == null)
            {
                site = manager.Sites.CreateElement("siteName");
            }
            return site;
        }
        #endregion
    }
}
