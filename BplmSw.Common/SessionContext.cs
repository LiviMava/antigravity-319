using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BplmSw.Common
{
    public static class SessionContext
    {
        public static string Token { get; set; }
        public static string Puid { get; set; }
        public static string ObjectType { get; set; }
        public static string IsWeb { get; set; }

        public static string UserId { get; set; }
        public static string UserName { get; set; }
    }
}