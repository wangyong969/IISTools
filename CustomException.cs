using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OneV.IISTools
{
    public class CustomException : Exception
    {
        public int Code { get; set; }
        public string Msg { get; set; }
        public CustomException(int code, string msg)
            : base(msg)
        {
            this.Code = code;
            this.Msg = msg;
        }
        public CustomException(string msg)
            : base(msg)
        {
            this.Code = -2;
            this.Msg = msg;
        }
    }

}
