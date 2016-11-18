using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OneV.IISTools
{
    /// <summary>
    /// 执行类型
    /// </summary>
    public enum CommandType
    {
        /// <summary>
        /// 打印帮助文件
        /// </summary>
        [ArgsSort("/?")]
        PrintHelper,
        /// <summary>
        /// 创建网站
        /// </summary>
        [ArgsSort("-web")]
        CreateWebSite,

        /// <summary>
        /// 创建虚拟目录
        /// </summary>
        [ArgsSort("-vir")]
        CreateVirtualDir,

        [ArgsSort("-del")]
        Del

    }
}
