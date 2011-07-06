using System;
using Gate;

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
        readonly Func<string, Tuple<Type, string>> configurationLoader;

        public KayakDelegateLoader() 
            : this(s => DefaultConfigurationLoader.GetTypeAndMethodNameForConfigurationString(s)) { }
        public KayakDelegateLoader(Func<string, Tuple<Type, string>> configurationLoader)
        {
            this.configurationLoader = configurationLoader;
        }

        public IKayakDelegate Load(string configurationString)
        {
            // okay, i see why the gate configuration loader
            // creates a default string and loads that. because you're going to need it anyway.
            // so, looks like really we just want to customize the MakeDelegate behavoir.

            // so for now it requires you give it a type, in an assembly (which can be inferred from the type)
            //
            // ack. k.

            if (string.IsNullOrEmpty(configurationString))
                return null;

            var typeAndMethodName = configurationLoader(configurationString);

            if (typeAndMethodName == null)
                return null;

            var type = typeAndMethodName.Item1;
            var methodName = typeAndMethodName.Item2;

            // we're always looking for a type.
            if (!string.IsNullOrEmpty(methodName))
                return null;

            if (!typeof(IKayakDelegate).IsAssignableFrom(type))
                return null;

            return (IKayakDelegate)Activator.CreateInstance(type);
        }
    }
}
