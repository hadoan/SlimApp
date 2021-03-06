using System.Collections.Generic;
using System.Reflection;

namespace SlimApp.EntityFrameworkCore.Utils
{
    internal static partial class DateTimePropertyInfoHelper
    {
        internal class EntityDateTimePropertiesInfo
        {
            public List<PropertyInfo> DateTimePropertyInfos { get; set; }

            public List<string> ComplexTypePropertyPaths { get; set; }

            public EntityDateTimePropertiesInfo()
            {
                DateTimePropertyInfos = new List<PropertyInfo>();
                ComplexTypePropertyPaths = new List<string>();
            }
        }
    }
}
