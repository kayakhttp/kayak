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
        string assembliesPath;

        public KayakDelegateLoader(string assembliesPath) 
        {
            this.assembliesPath = assembliesPath;
        }

        public IKayakDelegate Load(string configurationString)
        {
            var assemblies = Directory.GetFiles(assembliesPath, "*.dll")
                .Concat(Directory.GetFiles(assembliesPath, "*.exe"))
                .Select(s => Assembly.LoadFrom(s));

            var locator = new DefaultConfigurationLocator();
            var typeAndMethodName = locator.Locate(
                configurationString, 
                assemblies, a => new[] { a.GetName().Name + ".KayakDelegate" }, 
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
