using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogLibrary
{
    public static class ListExtensions
    {
        public static System.Collections.Generic.List<T> toManaged<T>(this Il2CppSystem.Collections.Generic.List<T> il2cppList)
        {
            System.Collections.Generic.List<T> managedList = new(il2cppList.Count);
            for (int i = 0; i < il2cppList.Count; i++)
            {
                managedList.Add(il2cppList[i]);
            }
            return managedList;
        }
    }
}
