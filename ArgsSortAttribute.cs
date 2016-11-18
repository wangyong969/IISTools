using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace OneV.IISTools
{

    /// <summary>
    /// 解析传入的参数(只支付bool与String类型);
    /// </summary>

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Enum | AttributeTargets.Field)]
    public class ArgsSortAttribute : Attribute
    {

        private int argsIndex = -1;
        /// <summary>
        /// 参数固定的位置
        /// </summary>
        public int ArgsIndex
        {
            get
            {
                return argsIndex;
            }
            set { argsIndex = value; }
        }
        public string ArgsFormat { get; set; }

        private int valueIndex = 0;
        /// <summary>
        /// 设置参数在当前位置之后增加的取值位置
        /// </summary>
        public int ValueIndex
        {
            get
            {
                return valueIndex;
            }
            set { valueIndex = value; }
        }


        /// <summary>
        /// 获取对应索引中的Value的数据
        /// </summary>
        /// <param name="index"></param>
        public ArgsSortAttribute(int index)
            : this(index, "", 0)
        {
        }
        public ArgsSortAttribute(string argsFormat)
            : this(-1, argsFormat, 0)
        {
        }
        public ArgsSortAttribute(int index, int valueIndex)
            : this(index, "", valueIndex)
        {
        }
        public ArgsSortAttribute(string argsFormat, int valueIndex)
            : this(-1, argsFormat, valueIndex)
        {
        }

        /// <summary>
        /// 描述参数需要的相关信息
        /// </summary>
        /// <param name="index">固定参数所在的位置</param>
        /// <param name="argsFormat">输入的参数</param>
        /// <param name="isValue">获取参数之后的值的位置</param>
        public ArgsSortAttribute(int index, string argsFormat, int valueIndex)
        {
            this.ArgsIndex = index;
            this.ArgsFormat = argsFormat;
            this.ValueIndex = valueIndex;

        }

        private static Hashtable cachedEnum = Hashtable.Synchronized(new Hashtable());
        private static readonly object syncRoot = new object();

    }



}
