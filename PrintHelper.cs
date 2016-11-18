using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OneV.IISTools
{
    public class PrintHelper
    {
        public static string Text()
        {

            string help =

               "\r\n\r\n格式：[必选参数一]  [网站的物理路径] {参数}\r\n\r\n" +
               "[必选参数一] ：\n" +
               "    执行命名的类型；\n" +
               "        -vir为创建/更新虚拟目录\n" +
               "        -web为创建/更新网站\n" +
               "        -del删除指定网站物理路径下的所有Web站点；\n" +
               "        -?为显示帮助文件；\n\n" +

               "[网站的物理路径]：\n" +
               "    必须。网站的物理路径，路径必须真实存在\n\n" +

               "{参数}：\n" +
               "-name  网站名称 \n" +
               "  （创建/或更新网站的网站名称，-name参数在-web类型时为必选）\n" +
               "   例如：-name webSite \n" +

               "-vname 虚拟目录别名\n" +
               "  （创建/更新虚拟目录的别名,-vname在-vir类型时为必选）\n" +
               "   例如：-vname test\n" +

               "-host  绑定的域名\n" +
               "  （创建/更新网站时，所需要绑定的域名列表，多个域名用“，”分隔）\n" +
               "   例如：-host www.xxx.com,www.abc.com \n" +

               "-port 端口\n" +
               "  （创建/更新网站时，绑定的端口； 不设置，则默认为80）\n" +

               "-classic\n" +
               "   指定在IIS7及以上版本创建经典模式的应用程序池，默认为集成模式\n" +

               "-enable32pool \n" +
               "   指定在IIS7及以上版本是否使用32位应用程序\n" +

               "-v .net版本\n" +
               "   指定IIS中应用程序版本的.net版本，默认为v4.0.30319，可切换为v2.0.50727";

            return help;
        }

    }
}
