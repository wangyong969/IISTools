using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace OneV.IISTools
{
    public class ConfigArgs
    {
        public ConfigArgsModel Model { get; internal set; }

        public List<string> Args { get; private set; }

        public ConfigArgs(string[] args)
        {
            Args = args.ToList<string>();
        }

        public CommandType GetExecuteType()
        {
            CommandType exType = CommandType.PrintHelper;
            if (Args.Count < 1)
            {
                throw new CustomException(-2, "缺少必要参数");
            }
            string val = Args[0].ToLower();
            switch (val)
            {
                case "-vir":
                    exType = CommandType.CreateVirtualDir;
                    break;
                case "-web":
                    exType = CommandType.CreateWebSite;
                    break;
                case "-del":
                    exType = CommandType.Del;
                    break;
                case "-?":
                    exType = CommandType.PrintHelper;
                    break;
                default:
                    exType = CommandType.PrintHelper;
                    break;
            }
            return exType;
        }




        public ConfigArgsModel Parse()
        {
            ConfigArgsModel model = new ConfigArgsModel();
            Type t = model.GetType();
            List<ArgsItems> sysItems = new List<ArgsItems>();
            foreach (PropertyInfo proptyinfo in t.GetProperties())
            {
                Type propertyType = proptyinfo.PropertyType;
                bool isBool = (propertyType == typeof(System.Boolean)); //当前属性是否bool值

                object[] obj = proptyinfo.GetCustomAttributes(typeof(ArgsSortAttribute), true);
                if (obj.Length != 1) continue;
                ArgsSortAttribute objAttr = (obj[0] as ArgsSortAttribute);
                string objKeyValue = objAttr.ArgsFormat.ToLower().Trim(); //参数值
                int objKeyIndex = objAttr.ArgsIndex; //参数位置
                int valueIndex = objAttr.ValueIndex;
                if (objKeyIndex > Args.Count - 1)  //当索引超出参数时，跳过
                {
                    continue;
                }

                if (objKeyIndex >= 0) //直接通过索引查找输入的参数
                {
                    string getValue = Args[objKeyIndex + valueIndex];
                    if (IsCheckArgs(getValue))
                    {
                        continue;
                    }
                    proptyinfo.SetValue(model, Convert.ChangeType(getValue, propertyType), null);
                    continue;
                }
                int getKeyIndex = Args.IndexOf(objKeyValue); //判断输入的参数中是否存在当前参数
                if (isBool) //当前实体对像如果Bool值，只需要判断当前参数是否存在
                {
                    proptyinfo.SetValue(model, getKeyIndex >= 0, null);
                    continue;
                }
                if (getKeyIndex < 0) //输入参数中如果不存在此实体的参数，则直接进入下一个参数的转换
                {
                    continue;
                }
                int getValueIndex = getKeyIndex + valueIndex;
                if (getValueIndex > Args.Count - 1)
                {
                    continue;
                }
                string val = Args[getValueIndex];

                if (IsCheckArgs(val))
                {
                    continue;
                }
                proptyinfo.SetValue(model, Convert.ChangeType(val, propertyType), null);
            }

            this.Model = model;
            return this.Model;
        }

        /// <summary>
        /// 检查取出来的值是否是参数格式的
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        private bool IsCheckArgs(string val)
        {
            try
            {
                char[] charItems = val.ToCharArray();
                return (charItems[0].Equals('/') || charItems[0].Equals('-'));
            }
            catch
            {
                return false;
            }

        }











    }
}
