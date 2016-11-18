using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OneV.IISTools;
using Microsoft.Web.Administration;
using System.IO;

namespace IISTools
{
    class Program
    {


        [STAThread]
        static int Main(string[] args)
        {
            try
            {
                Console.WriteLine("开始执行IISTools工具");
                int iisVersion = IISBaseConfig.GetIISVersion();

                ConfigArgs argsObj = new ConfigArgs(args);
                CommandType exType = argsObj.GetExecuteType();
                ConfigArgsModel argsModel = argsObj.Parse();
                argsModel.ExecuteType = exType;
                if (exType == CommandType.PrintHelper)
                {
                    Console.WriteLine(PrintHelper.Text());
                    return 0;
                }

                if (iisVersion < 7)
                {
                    argsModel.User32Pool = false;
                }


                IISBaseConfig iis = iisVersion >= 7 ? (IISBaseConfig)new IISConfigEx(argsModel) : (IISBaseConfig)new IISConfig(argsModel);
                iis.CheckParams();
                if (exType == CommandType.CreateWebSite)
                {
                    iis.CreateWebSite();

                }
                if (exType == CommandType.CreateVirtualDir)
                {
                    iis.CreateVirtualDir();
                }
                if (exType == CommandType.Del)
                {
                    iis.RemoveDir();
                }
            }
            catch (CustomException e)
            {
                Console.WriteLine(e.Msg);
                return e.Code;
            }
            Console.WriteLine("命令执行成功");
            return 1;
        }
    }
}
