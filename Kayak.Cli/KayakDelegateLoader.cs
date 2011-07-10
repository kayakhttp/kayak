using System;
using Gate;
using System.IO;
using System.Reflection;
using System.Linq;

namespace Kayak.Cli
{
    interface IKayakDelegate : ISchedulerDelegate
    {
        void OnStart(IScheduler scheduler, string[] args);
    }

    interface IKayakDelegateLoader
    {
        IKayakDelegate Load(string configurationString);
    }

    class KayakDelegateLoader : IKayakDelegateLoader
    {
        public IKayakDelegate Load(string configurationString)
        {
            var locator = new DefaultConfigurationLocator();
            var typeAndMethodName = locator.Locate(
                configurationString, 
                a => new[] { a.GetName().Name + ".KayakDelegate" }, 
                null);

            if (typeAndMethodName == null)
                return null;

            var type = typeAndMethodName.Item1;
            var method = typeAndMethodName.Item2;

            // we're always looking for a type.
            if (method != null)
                return null;

            if (!typeof(IKayakDelegate).IsAssignableFrom(type))
                return null;

            IKayakDelegate del = null;

            try
            {
                del = (IKayakDelegate)Activator.CreateInstance(type);
            }
            catch (Exception e)
            {
                return null;
            }

            return del;
        }
    }
}
