using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OneV.IISTools
{


    /// <summary>
    /// 获取参数信息[传入的参数只能为string]
    /// </summary>
    public class ConfigArgsModel
    {
        /// <summary>
        /// 物理路径
        /// </summary>
        [ArgsSort(1)]
        public string WebDir { get; set; }

        public CommandType ExecuteType { get; set; }


        /// <summary>
        /// 网站名称
        /// </summary>
        [ArgsSort("-name", 1)]
        public string WebName { get; set; }

        /// <summary>
        /// 虚拟目录别名
        /// </summary>
        [ArgsSort("-vname", 1)]
        public string VirtualName { get; set; }

        /// <summary>
        /// 是否经典模式
        /// </summary>
        [ArgsSort("-classic")]
        public bool IsClassic { get; set; }



        /// <summary>
        /// 是否使用32位应用程序
        /// </summary>
        [ArgsSort("-enable32pool")]
        public bool User32Pool { get; set; }
        /// <summary>
        /// 端口
        /// </summary>
        [ArgsSort("-port", 1)]
        public string Port { get; set; }




        private string frameworkVersion = "v4.0.30319";
        /// <summary>
        /// .net框架
        /// </summary>
        [ArgsSort("-v", 1)]
        public string FrameworkVersion
        {
            get
            {
                return frameworkVersion;
            }
            set
            {
                if (value != null)
                {
                    frameworkVersion = value;
                }
            }
        }


        /// <summary>
        /// 绑定的主机名,多个主机名用“,”分开
        /// </summary>
        [ArgsSort("-host", 1)]
        public string HostUrl { get; set; }
    }
}
